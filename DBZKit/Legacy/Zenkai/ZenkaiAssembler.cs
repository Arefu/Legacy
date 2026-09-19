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

        // Opcodes whose real VM function has a side effect beyond "compute and return a
        // value" -- specifically, they advance the RNG state (GetRandom, 0x80213FC).
        // Because `var`/reference substitution means every reference to a var RE-RUNS its
        // defining call (see the Zenkai.g4 header comment -- there is no VM local storage
        // to cache a result in), referencing one of these more than once would silently
        // give a DIFFERENT random result each time rather than the one you already branched
        // on. Anything else producing a value (flag tests, item counts, character stats)
        // is a pure read of existing state, so re-running it is harmless.
        private static readonly HashSet<string> NonReplayableOpcodes = new() { "StackRand", "StackRandChance" };

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
            var labelOffsets = new Dictionary<string, int>();
            var fixups = new List<(int operandOffset, string label, bool isLoop)>();
            var varDefs = new Dictionary<string, ZenkaiParser.CallContext>();
            var varRefCounts = new Dictionary<string, int>();
            int syntheticCounter = 0;

            // Pass 0: collect every `var X = Call(...);` binding (recursing into if/else
            // blocks) and count how many times each var name is actually referenced
            // anywhere (as a call argument or an `if` condition) -- needed before we can
            // emit anything, both to resolve references forward and to reject reusing a
            // StackRand/StackRandChance-bound var. NOTE: this counts every syntactic
            // reference, including ones inside another var's OWN definition even if that
            // outer var itself ends up unused (and therefore never actually emitted) --
            // deliberately conservative rather than perfectly precise, so it can only ever
            // reject a technically-safe edge case, never silently miss a real double-roll.
            CollectVarsAndCounts(tree.stmt(), varDefs, varRefCounts);

            foreach (var (name, count) in varRefCounts)
            {
                if (count <= 1) continue;
                if (!varDefs.TryGetValue(name, out var defCall)) continue; // dangling ref, reported properly at emit time
                if (!IsNonReplayable(defCall)) continue;

                string calledName = defCall.IDENT().GetText();
                throw new ZenkaiAssemblerException(
                    $"'{name}' is bound to {calledName}(...) and referenced {count} times -- {calledName} can't be " +
                    "reused like this because there's no VM storage to cache its result in, so every reference " +
                    "actually re-runs it, which means a DIFFERENT random roll each time instead of the one you " +
                    "already checked. Restructure so you only need the roll once (e.g. duplicate the branch you " +
                    "need it in, or drive everything off a single JumpIfFalse on it).");
            }

            EmitStmts(tree.stmt(), bytes, labelOffsets, fixups, varDefs, ref syntheticCounter);

            // Pass 2: backpatch jump-family operand bytes (both user Jump/JumpIfFalse/
            // LoopOrJump calls and the synthetic ones `if`/`else` emit) now that every
            // label -- user-defined or synthetic -- has a known byte offset.
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

            // A trailing `return` already emitted its own END.
            var topStmts = tree.stmt();
            if (topStmts.Length == 0 || topStmts[^1].returnStmt() == null)
                bytes.Add(0x11); // END
            return bytes.ToArray();
        }

        private static bool IsNonReplayable(ZenkaiParser.CallContext call) =>
            OpcodeTable.TryResolveFuzzy(call.IDENT().GetText(), out var op, out _) && op != null && NonReplayableOpcodes.Contains(op.Name);

        private static void CollectVarsAndCounts(IEnumerable<ZenkaiParser.StmtContext> stmts, Dictionary<string, ZenkaiParser.CallContext> varDefs, Dictionary<string, int> varRefCounts)
        {
            foreach (var stmt in stmts)
            {
                if (stmt.varDecl() is { } vd)
                {
                    string name = vd.IDENT().GetText();
                    if (varDefs.ContainsKey(name))
                        throw new ZenkaiAssemblerException($"Duplicate variable '{name}'");

                    string calledOp = vd.call().IDENT().GetText();
                    if (!OpcodeTable.TryResolveFuzzy(calledOp, out var opInfo, out string? err) || opInfo == null)
                        throw new ZenkaiAssemblerException(err ?? $"Unknown opcode '{calledOp}'");

                    // Only Stack*/Push*-named opcodes actually leave a result on the VM
                    // stack to assign -- see the "producesValue" heuristic in
                    // ZenkaiDisassembler.cs (same convention, same justification).
                    bool produces = opInfo.Name.StartsWith("Stack", StringComparison.Ordinal) || opInfo.Name.StartsWith("Push", StringComparison.Ordinal);
                    if (!produces)
                        throw new ZenkaiAssemblerException($"var {name} = {calledOp}(...): {calledOp} doesn't produce a value (only Stack*/Push*-named opcodes do) -- call it as a plain statement instead, not a var assignment.");

                    varDefs[name] = vd.call();
                    CountRefsInCall(vd.call(), varRefCounts);
                    continue;
                }

                if (stmt.returnStmt() is { } ret)
                {
                    if (ret.IDENT() is { } retVar)
                        varRefCounts[retVar.GetText()] = varRefCounts.GetValueOrDefault(retVar.GetText()) + 1;
                    continue;
                }

                if (stmt.ifStmt() is { } ifs)
                {
                    CountRefsInCond(ifs.cond(), varRefCounts);
                    foreach (var b in ifs.body())
                        CollectVarsAndCounts(BodyStmts(b), varDefs, varRefCounts);
                    continue;
                }

                if (stmt.call() is { } call && !JumpFamily.Contains(call.IDENT().GetText()))
                    CountRefsInCall(call, varRefCounts);

                // label, and jump-family calls (their IDENT arg is a label, not a var ref): nothing to count.
            }
        }

        private static void CountRefsInCall(ZenkaiParser.CallContext call, Dictionary<string, int> varRefCounts)
        {
            var args = call.argList()?.arg() ?? Array.Empty<ZenkaiParser.ArgContext>();
            foreach (var a in args)
            {
                if (a is ZenkaiParser.LabelArgContext identArg)
                {
                    string name = identArg.IDENT().GetText();
                    varRefCounts[name] = varRefCounts.GetValueOrDefault(name) + 1;
                }
            }
        }

        private static void CountRefsInCond(ZenkaiParser.CondContext cond, Dictionary<string, int> varRefCounts)
        {
            if (cond is ZenkaiParser.CondVarContext cv)
            {
                string name = cv.IDENT().GetText();
                varRefCounts[name] = varRefCounts.GetValueOrDefault(name) + 1;
            }
            else if (cond is ZenkaiParser.CondCallContext cc)
            {
                CountRefsInCall(cc.call(), varRefCounts);
            }
            else if (cond is ZenkaiParser.CondCompareContext cmp)
            {
                foreach (var term in cmp.condTerm())
                    CountRefsInCondTerm(term, varRefCounts);
            }
        }

        private static void CountRefsInCondTerm(ZenkaiParser.CondTermContext term, Dictionary<string, int> varRefCounts)
        {
            if (term is ZenkaiParser.TermVarContext tv)
            {
                string name = tv.IDENT().GetText();
                varRefCounts[name] = varRefCounts.GetValueOrDefault(name) + 1;
            }
            else if (term is ZenkaiParser.TermCallContext tc)
            {
                CountRefsInCall(tc.call(), varRefCounts);
            }
        }

        private static void EmitStmts(IEnumerable<ZenkaiParser.StmtContext> stmts, List<byte> bytes, Dictionary<string, int> labelOffsets, List<(int operandOffset, string label, bool isLoop)> fixups, Dictionary<string, ZenkaiParser.CallContext> varDefs, ref int syntheticCounter)
        {
            foreach (var stmt in stmts)
            {
                if (stmt.label() is { } label)
                {
                    string name = label.IDENT().GetText();
                    if (labelOffsets.ContainsKey(name))
                        throw new ZenkaiAssemblerException($"Duplicate label '{name}'");
                    labelOffsets[name] = bytes.Count;
                    continue;
                }

                if (stmt.varDecl() is not null)
                {
                    // Nothing to emit at the declaration site itself -- see Zenkai.g4's
                    // header comment. Every REFERENCE re-runs the bound call in place
                    // (EmitCall below); a var that's never referenced never runs at all.
                    continue;
                }

                if (stmt.ifStmt() is { } ifs)
                {
                    EmitIf(ifs, bytes, labelOffsets, fixups, varDefs, ref syntheticCounter);
                    continue;
                }

                if (stmt.returnStmt() is { } ret)
                {
                    if (ret.INT() is { } retInt)
                        EmitPushes(bytes, new List<long> { ParseInt(retInt.GetText()) });
                    else if (ret.IDENT() is { } retVar)
                    {
                        if (!varDefs.TryGetValue(retVar.GetText(), out var retCall))
                            throw new ZenkaiAssemblerException($"return {retVar.GetText()}: unknown variable '{retVar.GetText()}'");
                        EmitCall(bytes, retCall, varDefs);
                    }
                    bytes.Add(0x11); // END -- ends the script with the value left on the stack
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
                    bytes.Add(0); // placeholder, backpatched later
                    continue;
                }

                EmitCall(bytes, call, varDefs);
            }
        }

        private static void EmitIf(ZenkaiParser.IfStmtContext ifs, List<byte> bytes, Dictionary<string, int> labelOffsets, List<(int operandOffset, string label, bool isLoop)> fixups, Dictionary<string, ZenkaiParser.CallContext> varDefs, ref int syntheticCounter)
        {
            int id = syntheticCounter++;
            string elseLabel = $"__if{id}_else";
            string endLabel = $"__if{id}_end";

            switch (ifs.cond())
            {
                case ZenkaiParser.CondVarContext cv:
                    {
                        string name = cv.IDENT().GetText();
                        if (!varDefs.TryGetValue(name, out var innerCall))
                            throw new ZenkaiAssemblerException($"if ({name}): unknown variable '{name}'");
                        EmitCall(bytes, innerCall, varDefs);
                        break;
                    }
                case ZenkaiParser.CondCallContext cc:
                    EmitCall(bytes, cc.call(), varDefs);
                    break;
                case ZenkaiParser.CondCompareContext cmp:
                    {
                        // Compiles straight to the VM's own raw comparison instructions
                        // (0x0B-0x10) -- see Zenkai.g4. LHS pushed first, RHS second,
                        // matching the confirmed real semantics (SView_Decode.cs /
                        // ZenkaiDisassembler.cs: `b = pop(); a = pop(); push(a OP b)`,
                        // so the first-pushed value is `a`/LHS).
                        var terms = cmp.condTerm();
                        EmitCondTerm(bytes, terms[0], varDefs);
                        EmitCondTerm(bytes, terms[1], varDefs);
                        byte cmpOp = cmp.CMPOP().GetText() switch
                        {
                            "==" => 0x0B,
                            "!=" => 0x0C,
                            ">" => 0x0D,
                            ">=" => 0x0E,
                            "<" => 0x0F,
                            "<=" => 0x10,
                            _ => throw new ZenkaiAssemblerException("unreachable")
                        };
                        bytes.Add(cmpOp);
                        break;
                    }
            }

            var bodies = ifs.body();
            bool hasElse = bodies.Length > 1;

            bytes.Add(0x13); // JumpIfFalse
            fixups.Add((bytes.Count, hasElse ? elseLabel : endLabel, false));
            bytes.Add(0);

            EmitStmts(BodyStmts(bodies[0]), bytes, labelOffsets, fixups, varDefs, ref syntheticCounter);

            if (hasElse)
            {
                bytes.Add(0x12); // Jump past the else block
                fixups.Add((bytes.Count, endLabel, false));
                bytes.Add(0);

                labelOffsets[elseLabel] = bytes.Count;
                EmitStmts(BodyStmts(bodies[1]), bytes, labelOffsets, fixups, varDefs, ref syntheticCounter);
            }

            labelOffsets[endLabel] = bytes.Count;
        }

        // A body is either a real { ... } block or (braces optional, same as C#) a single
        // bare statement -- see Zenkai.g4's body rule. Normalizes both shapes to "the list
        // of statements this body contains" so EmitIf/CollectVarsAndCounts don't need two
        // code paths.
        private static IEnumerable<ZenkaiParser.StmtContext> BodyStmts(ZenkaiParser.BodyContext body) =>
            body.block() is { } blk ? blk.stmt() : new[] { body.stmt() };

        // Emits one opcode call: pushes for each argument (a literal, or -- if the
        // argument is a var reference -- that var's own bound call re-emitted right here,
        // which leaves its result sitting on the VM stack exactly where this call expects
        // its argument to be), then Step(opcodeIndex). Recurses for nested var references
        // (e.g. `var C = Combine(A, B);`). Used for statement-level calls, `if` conditions,
        // and var-reference substitution alike -- there's no other place bytes get emitted
        // for a call.
        private static void EmitCall(List<byte> bytes, ZenkaiParser.CallContext call, Dictionary<string, ZenkaiParser.CallContext> varDefs)
        {
            string opName = call.IDENT().GetText();
            var args = call.argList()?.arg() ?? Array.Empty<ZenkaiParser.ArgContext>();

            if (!OpcodeTable.TryResolveFuzzy(opName, out var op, out string? resolveError) || op == null)
                throw new ZenkaiAssemblerException(resolveError ?? $"Unknown opcode '{opName}'");

            if (args.Length != op.Arity)
                throw new ZenkaiAssemblerException($"{opName} expects {op.Arity} argument(s), got {args.Length}");

            // All-literal calls keep the original canonical encoding (PushMultiVarint batch
            // for 2+ args) so existing scripts/tests still reassemble byte-identical. Only
            // falls back to emitting one push per argument when a var reference is mixed in,
            // since that can't be folded into a single PushMultiVarint batch.
            if (args.All(a => a is ZenkaiParser.IntArgContext))
            {
                var intArgs = args.Select(a => ParseInt(((ZenkaiParser.IntArgContext)a).INT().GetText())).ToList();
                EmitPushes(bytes, intArgs);
            }
            else
            {
                foreach (var a in args)
                {
                    if (a is ZenkaiParser.IntArgContext intArg)
                    {
                        EmitPushes(bytes, new List<long> { ParseInt(intArg.INT().GetText()) });
                    }
                    else if (a is ZenkaiParser.LabelArgContext identArg)
                    {
                        string name = identArg.IDENT().GetText();
                        if (!varDefs.TryGetValue(name, out var innerCall))
                            throw new ZenkaiAssemblerException($"{opName}: unknown variable '{name}'");
                        EmitCall(bytes, innerCall, varDefs);
                    }
                }
            }

            bytes.Add(0x02); // Step
            bytes.Add((byte)op.Index);
        }

        // Emits one side of a comparison (`cond` in Zenkai.g4): a literal, a var
        // reference (re-runs its bound call, same substitution rule as everywhere
        // else), or an inline call -- whichever leaves exactly one value on the VM
        // stack for the comparison instruction right after it to consume.
        private static void EmitCondTerm(List<byte> bytes, ZenkaiParser.CondTermContext term, Dictionary<string, ZenkaiParser.CallContext> varDefs)
        {
            switch (term)
            {
                case ZenkaiParser.TermIntContext ti:
                    EmitPushes(bytes, new List<long> { ParseInt(ti.INT().GetText()) });
                    break;
                case ZenkaiParser.TermVarContext tv:
                    {
                        string name = tv.IDENT().GetText();
                        if (!varDefs.TryGetValue(name, out var innerCall))
                            throw new ZenkaiAssemblerException($"unknown variable '{name}'");
                        EmitCall(bytes, innerCall, varDefs);
                        break;
                    }
                case ZenkaiParser.TermCallContext tc:
                    EmitCall(bytes, tc.call(), varDefs);
                    break;
            }
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
