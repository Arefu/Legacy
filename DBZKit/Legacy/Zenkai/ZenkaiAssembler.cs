using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace Legacy.Zenkai
{
    /// <summary>
    /// Compiles Zenkai script text (see Zenkai.g4) to VM bytecode.
    ///
    /// Encoding policy for opcode call arguments (Name(a, b, ...)):
    ///   - 0 args:  just Step(index), no pushes.
    ///   - 1 arg:   PushByte if 0&lt;=v&lt;=255, else PushVarint (zigzag).
    ///   - 2+ args: always PushMultiVarint(count) with each value zigzag-encoded.
    /// This matches how the real example scripts in Zenkai-Example-Scripts.md are
    /// authored (single small args -> PushByte, multi-arg calls -> PushMultiVarint),
    /// so disassembling one of those and reassembling reproduces the original bytes.
    /// It is a DETERMINISTIC canonical choice, not a guarantee of exact-byte fidelity
    /// for arbitrary source someone hand-writes differently than a real script would
    /// (e.g. there's no way to ask this assembler for a raw PushVarint(3) instead of
    /// PushByte(3) -- both decode to the same integer 3, only one is canonical here).
    /// </summary>
    public class ZenkaiAssemblerException : Exception
    {
        public ZenkaiAssemblerException(string message) : base(message) { }
    }

    // One squiggle-able syntax problem: 1-based line, 0-based column, and a best-effort
    // token length (1 when the offending token's text isn't available, e.g. EOF).
    public record ZenkaiDiagnostic(int Line, int Column, int Length, string Message);

    public static class ZenkaiAssembler
    {
        private static readonly HashSet<string> JumpFamily = new() { "Jump", "JumpIfFalse", "LoopOrJump" };

        // Syntax-only check for editor squiggles - does not run the semantic/assemble
        // pass (unknown opcodes, arity, label resolution), so it can't tell a compile
        // will fail, only that the text doesn't parse as Zenkai grammar at all.
        public static List<ZenkaiDiagnostic> Validate(string source)
        {
            var diagnostics = new List<ZenkaiDiagnostic>();

            var inputStream = new AntlrInputStream(source);
            var lexer = new ZenkaiLexer(inputStream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new ZenkaiParser(tokens);

            var listener = new CollectingErrorListener(diagnostics);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(listener);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(listener);

            parser.script();
            return diagnostics;
        }

        private class CollectingErrorListener : BaseErrorListener, IAntlrErrorListener<int>
        {
            private readonly List<ZenkaiDiagnostic> _diagnostics;
            public CollectingErrorListener(List<ZenkaiDiagnostic> diagnostics) => _diagnostics = diagnostics;

            public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
            {
                int length = offendingSymbol?.Text?.Length ?? 1;
                _diagnostics.Add(new ZenkaiDiagnostic(line, charPositionInLine, Math.Max(1, length), msg));
            }

            public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
                => _diagnostics.Add(new ZenkaiDiagnostic(line, charPositionInLine, 1, msg));
        }

        public static byte[] Assemble(string source)
        {
            var inputStream = new AntlrInputStream(source);
            var lexer = new ZenkaiLexer(inputStream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new ZenkaiParser(tokens);

            var errors = new List<string>();
            var errorListener = new ThrowingErrorListener(errors);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            var tree = parser.script();
            if (errors.Count > 0)
                throw new ZenkaiAssemblerException("Syntax error(s): " + string.Join("; ", errors));

            return AssembleTree(tree);
        }

        private static byte[] AssembleTree(ZenkaiParser.ScriptContext tree)
        {
            var bytes = new List<byte>();

            // Pass 1: emit bytes, recording label definitions (byte offset) and
            // jump-family fixups (byte offset of the operand byte + target label name).
            var labelOffsets = new Dictionary<string, int>();
            var fixups = new List<(int operandOffset, string label, bool isLoop)>();

            foreach (var stmt in tree.stmt())
            {
                if (stmt.label() is { } label)
                {
                    string name = label.IDENT().GetText();
                    if (labelOffsets.ContainsKey(name))
                        throw new ZenkaiAssemblerException($"Duplicate label '{name}'");
                    labelOffsets[name] = bytes.Count;
                    continue;
                }

                var call = stmt.call();
                string opName = call.IDENT().GetText();
                var args = call.argList()?.arg() ?? Array.Empty<ZenkaiParser.ArgContext>();

                if (JumpFamily.Contains(opName))
                {
                    if (args.Length != 1 || args[0] is not ZenkaiParser.LabelArgContext labelArg)
                        throw new ZenkaiAssemblerException($"{opName} takes exactly one label argument, e.g. {opName}(myLabel)");

                    byte mainOp = opName switch
                    {
                        "Jump" => 0x12,
                        "JumpIfFalse" => 0x13,
                        "LoopOrJump" => 0x1C,
                        _ => throw new ZenkaiAssemblerException("unreachable")
                    };
                    bytes.Add(mainOp);
                    fixups.Add((bytes.Count, labelArg.IDENT().GetText(), opName == "LoopOrJump"));
                    bytes.Add(0); // placeholder, backpatched below
                    continue;
                }

                if (!OpcodeTable.NameMap.TryGetValue(opName, out var op))
                    throw new ZenkaiAssemblerException($"Unknown opcode '{opName}'");

                var intArgs = new List<long>();
                foreach (var a in args)
                {
                    if (a is not ZenkaiParser.IntArgContext intArg)
                        throw new ZenkaiAssemblerException($"{opName}: argument must be an integer, got label '{a.GetText()}'");
                    intArgs.Add(ParseInt(intArg.INT().GetText()));
                }

                if (intArgs.Count != op.Arity)
                    throw new ZenkaiAssemblerException($"{opName} expects {op.Arity} argument(s), got {intArgs.Count}");

                EmitPushes(bytes, intArgs);

                bytes.Add(0x02); // Step
                bytes.Add((byte)op.Index);
            }

            // Pass 2: backpatch jump-family operand bytes now that all labels are known.
            foreach (var (operandOffset, labelName, isLoop) in fixups)
            {
                if (!labelOffsets.TryGetValue(labelName, out int targetOffset))
                    throw new ZenkaiAssemblerException($"Undefined label '{labelName}'");

                // Confirmed semantics (BytecodeVM_OpCodes.md): for Jump/JumpIfFalse, target =
                // (offset right after the operand byte) + signed delta. For LoopOrJump, the
                // decompiled form is `pc -= delta` on a backward branch, i.e. target = afterOperand - delta.
                int afterOperand = operandOffset + 1;
                int delta = isLoop ? (afterOperand - targetOffset) : (targetOffset - afterOperand);

                if (delta < sbyte.MinValue || delta > sbyte.MaxValue)
                    throw new ZenkaiAssemblerException($"Label '{labelName}' is out of 1-signed-byte jump range (delta={delta})");

                bytes[operandOffset] = unchecked((byte)(sbyte)delta);
            }

            bytes.Add(0x11); // END
            return bytes.ToArray();
        }

        private static void EmitPushes(List<byte> bytes, List<long> values)
        {
            if (values.Count == 0) return;

            if (values.Count == 1)
            {
                long v = values[0];
                if (v >= 0 && v <= 255)
                {
                    bytes.Add(0x00); // PushByte
                    bytes.Add((byte)v);
                }
                else
                {
                    bytes.Add(0x01); // PushVarint
                    EmitVarint(bytes, ZigZagEncode(v));
                }
                return;
            }

            bytes.Add(0x1D); // PushMultiVarint
            bytes.Add((byte)values.Count);
            foreach (var v in values)
                EmitVarint(bytes, ZigZagEncode(v));
        }

        // Matches SView_Decoder.ReadVarint's bit order: 7 bits per byte, MSB-first
        // accumulation ((value << 7) | (b & 0x7F)), continuation bit = 0x80.
        private static void EmitVarint(List<byte> bytes, long value)
        {
            var groups = new List<byte>();
            ulong u = (ulong)value;
            do
            {
                groups.Add((byte)(u & 0x7F));
                u >>= 7;
            } while (u != 0);

            for (int i = groups.Count - 1; i >= 0; i--)
            {
                byte b = groups[i];
                if (i != 0) b |= 0x80;
                bytes.Add(b);
            }
        }

        // Inverse of SView_Tools.ZigZagDecode, which is NOT the textbook zigzag formula:
        // decode(v) = odd(v) ? -(v>>1) : (v>>1). That means encode must be
        // n>=0 -> 2n, n<0 -> 2*(-n)+1 (sign-in-low-bit with absolute magnitude, no -1
        // adjustment) rather than the standard (n<<1)^(n>>63). Verified: decode(encode(-3))
        // = decode(7) = -(7>>1) = -3. The textbook formula would emit 5 here, which
        // decodes back to -2, not -3 -- silently wrong. Getting this right matters because
        // it's the difference between the assembler and the real ROM's own encoder
        // producing byte-identical varints for negative arguments.
        private static long ZigZagEncode(long v) => v >= 0 ? (v << 1) : (((-v) << 1) + 1);

        private static long ParseInt(string text)
        {
            bool neg = text.StartsWith('-');
            if (neg) text = text[1..];
            long v = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt64(text[2..], 16)
                : long.Parse(text);
            return neg ? -v : v;
        }

        private class ThrowingErrorListener : BaseErrorListener, IAntlrErrorListener<int>
        {
            private readonly List<string> _errors;
            public ThrowingErrorListener(List<string> errors) => _errors = errors;

            public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
                => _errors.Add($"line {line}:{charPositionInLine} {msg}");

            public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
                => _errors.Add($"line {line}:{charPositionInLine} {msg}");
        }
    }
}
