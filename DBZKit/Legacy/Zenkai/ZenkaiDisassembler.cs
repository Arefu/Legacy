using System.Text;

namespace Legacy.Zenkai
{
    /// <summary>
    /// Converts VM bytecode back into Zenkai source text (see Zenkai.g4), by decoding
    /// the byte stream per the main dispatch table (same logic as SView_Decoder, kept
    /// in sync with its confirmed 1-signed-byte Jump/JumpIfFalse/LoopOrJump fix) and
    /// folding each run of Push* instructions immediately followed by a Step into a
    /// single `Name(args...)` call, matching the call syntax ZenkaiAssembler accepts.
    /// Auto-generates label names (L0, L1, ...) for every jump target so output is
    /// directly re-assemblable without the user having to hand-compute byte offsets.
    /// </summary>
    public static class ZenkaiDisassembler
    {
        private record RawInsn(int Offset, int NextOffset, string Kind, List<long> PushedValues, int StepOpcode = -1, int JumpTarget = -1, bool IsLoop = false);

        // Flat, structure-free line model built by the main decode loop below -- exactly
        // the same lines that used to go straight to the output StringBuilder, just kept as
        // data so the structuring pass (Structure/Render, added 2026-09-19) can recognize
        // JumpIfFalse/Jump/label patterns and fold them back into real if/else blocks
        // instead of printing raw jumps. CondJump/UncondJump only cover opcodes 0x13/0x12 --
        // LoopOrJump (0x1C, backward branches -- i.e. loops, not if/else) is deliberately
        // NOT one of these; it stays a plain Code line, unrecognized by Structure(), same
        // as before this change.
        private abstract record Line;
        private sealed record CodeLine(string Text) : Line;
        private sealed record LabelLine(string Name) : Line;
        private sealed record CondJumpLine(string Cond, string Target) : Line;
        private sealed record UncondJumpLine(string Target) : Line;

        // A value on the simulated VM stack. Kept as a small expression (not eagerly
        // turned into a `var tN` line) so a value that's consumed exactly once can be
        // inlined where it's used -- e.g. `if (StackGetItemCount(0) == 1)` instead of
        // `var t0 = ...; var t1 = (t0 == 1); if (t1)`. Only materialized into a
        // `var tN = ...;` when it has to be an opcode-call argument (Zenkai.g4 args are
        // literals or var names, not nested calls).
        //   Literal = a pushed constant; Call = `Name(args)` from a value-producing Step;
        //   Compare = `a == b` etc. whose operands are both literals/calls (exactly what
        //   Zenkai.g4's `cond` accepts); Other = AND/NOT/arithmetic/nested compares
        //   (displayable, but no Zenkai source syntax for them yet).
        private enum ValKind { Literal, Call, Compare, Other }
        private sealed record Val(string Text, ValKind Kind);

        private abstract record Node;
        private sealed record RawNode(string Text) : Node;
        private sealed record IfNode(string Cond, List<Node> Then) : Node;
        private sealed record IfElseNode(string Cond, List<Node> Then, List<Node> Else) : Node;

        // Lets the pre-existing Take()/EmitProducing()/FlushRemainingStack() code below
        // keep calling "sb.AppendLine(...)" unchanged while actually appending to the
        // structure-aware `lines` list instead of a real StringBuilder.
        private sealed class EmitShim
        {
            private readonly Action<string> _emit;
            public EmitShim(Action<string> emit) => _emit = emit;
            public void AppendLine(string text) => _emit(text);
        }

        public static string Disassemble(byte[] data) => Disassemble(data, out _);

        // byteLength is how many bytes of `data` the script actually occupies (through and
        // including the terminating END opcode) - the caller needs this to know how much
        // ROM space is safe to overwrite in place when re-saving an edited script.
        public static string Disassemble(byte[] data, out int byteLength)
        {
            var insns = Decode(data);
            byteLength = insns.Count > 0 ? insns[^1].NextOffset : 0;

            // Find every distinct jump target so we can emit labels for them, in the
            // order they first appear, named by increasing byte offset for readability.
            var targets = insns
                .Where(i => i.JumpTarget >= 0)
                .Select(i => i.JumpTarget)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
            var labelNames = targets
                .Select((t, i) => (t, name: $"L{i}"))
                .ToDictionary(x => x.t, x => x.name);

            var lines = new List<Line>();
            void Emit(string text) => lines.Add(new CodeLine(text));

            // Kept as a thin shim so the rest of this method (Take/EmitProducing/
            // FlushRemainingStack below) reads exactly as it did before this change --
            // only the destination changed, from "sb.AppendLine" to "Emit".
            var sb = new EmitShim(Emit);

            // REDESIGNED 2026-09-19: this used to be a flat List<long> of "pending pushes"
            // handed wholesale to whatever Step came next, which was wrong in two ways real
            // ROM scripts exposed: (1) it ignored arity, so N pushes ahead of a 1-arg opcode
            // all got attributed to it; (2) opcodes that actually PRODUCE a value on the VM
            // stack (StackRand peek-and-replaces in place; StackAnd/StackAdd/comparisons/etc
            // all pop N and push 1 result) had no way to leave that result available for
            // whatever consumes it next -- their result just vanished, which is exactly why
            // real Z1A2 scripts (StackRand feeding an entity-command opcode, StackAdd feeding
            // another) came out as broken "orphan push" spam and 0-arg calls with an arity
            // mismatch comment despite the values being right there.
            //
            // This models the REAL VM stack symbolically: `stack` holds one string per value
            // currently on it -- either a literal ("54") or a reference to an already-emitted
            // temp variable ("t3") for a value that came from a computed result rather than a
            // plain PushByte/PushVarint. Any opcode that produces a result (Stack*/Push*-named
            // Step opcodes, and the raw arithmetic/comparison/logic ops 0x04-0x10) gets its
            // call wrapped in `tN = ...;` and `tN` pushed back onto `stack`, so later
            // consumers -- another Step, a raw op, a jump condition -- pick it up correctly
            // via the same LIFO pop this file already established. This is a DISPLAY-ONLY
            // convention (matches this whole project's own decompiler-output style, e.g.
            // `v1`/`v2` in every IDA excerpt across BytecodeVM_OpCodes.md) -- ZenkaiAssembler
            // does not understand `tN =` assignment syntax yet, so a script containing one
            // can be viewed correctly here but not reassembled from this text unhandled;
            // that's a real, separate grammar/assembler gap, not pretended away.
            var stack = new List<Val>();
            int tempCounter = 0;

            // Operand text for embedding inside a larger expression.
            static string Inline(Val v) => v.Kind is ValKind.Compare or ValKind.Other ? $"({v.Text})" : v.Text;

            // Text for use as an opcode-call argument: literals stay literal; anything
            // computed becomes a named temp (`var tN = ...;` emitted just before the call).
            string Arg(Val v)
            {
                if (v.Kind == ValKind.Literal) return v.Text;
                string t = $"t{tempCounter++}";
                sb.AppendLine($"var {t} = {v.Text};");   // an assignment needs no parentheses
                return t;
            }

            List<Val> Take(int arity)
            {
                int take = Math.Min(arity, stack.Count);
                var args = stack.GetRange(stack.Count - take, take);
                stack.RemoveRange(stack.Count - take, take);
                if (take < arity)
                    sb.AppendLine($"// arity mismatch: expected {arity}, only {take} value(s) on the simulated stack here");
                return args;
            }

            void FlushRemainingStack(string context)
            {
                // A script's END can legitimately leave exactly one value on the stack --
                // that's the script's return value (see Dialog_Format.md mode-0 entries,
                // and every QuestEntry condition script in Quest-System.md, both of which
                // rely on exactly this). More than one left over at END, or ANY leftover at
                // a point that isn't END (a jump crossing a branch, for instance), is
                // genuinely unusual for a well-formed script -- surfaced either way rather
                // than silently dropped. A leftover CALL is emitted as a plain statement
                // (the assembler leaves its result on the stack too), so it round-trips.
                bool isResult = stack.Count == 1 && context == "end";
                foreach (var v in stack)
                {
                    string note = isResult ? "script result" : $"leftover on stack at {context}";
                    if (v.Kind == ValKind.Call)
                        sb.AppendLine($"{v.Text}; // {note}");
                    else if (isResult && v.Kind == ValKind.Literal)
                        sb.AppendLine($"return {v.Text};"); // reassembles: push + END
                    else
                        sb.AppendLine($"// {note}: {v.Text}");
                }
                stack.Clear();
            }

            foreach (var insn in insns)
            {
                if (labelNames.TryGetValue(insn.Offset, out var label))
                    lines.Add(new LabelLine(label));

                switch (insn.Kind)
                {
                    case "push":
                        foreach (var v in insn.PushedValues)
                            stack.Add(new Val(v.ToString(), ValKind.Literal));
                        break;

                    case "step":
                        {
                            if (!OpcodeTable.IndexMap.TryGetValue(insn.StepOpcode, out var op))
                            {
                                sb.AppendLine($"// UNKNOWN_STEP_{insn.StepOpcode:X2}() -- arity unknown, stack left as-is");
                                break;
                            }

                            var args = Take(op.Arity).Select(Arg).ToList();
                            string call = $"{op.Name}({string.Join(", ", args)})";

                            // Heuristic (matches this project's own naming convention,
                            // cross-checked against opcode_data.js's "returns" column for
                            // every opcode sampled so far): a "Stack"/"Push"-prefixed name
                            // means the real VM function pops its args and pushes a result
                            // back (often in place, e.g. BytecodeVM_StackRand's peek-and-
                            // replace) -- everything else (Set*/Spawn*/Despawn*/Walk*/etc)
                            // is a pure action with nothing left on the stack afterward.
                            bool producesValue = op.Name.StartsWith("Stack", StringComparison.Ordinal)
                                || op.Name.StartsWith("Push", StringComparison.Ordinal);

                            if (producesValue)
                                stack.Add(new Val(call, ValKind.Call));
                            else
                                sb.AppendLine($"{call};");
                            break;
                        }

                    case "jump":
                        {
                            string targetLabel = labelNames.TryGetValue(insn.JumpTarget, out var l) ? l : $"0x{insn.JumpTarget:X}";

                            // JumpIfFalse pops its condition directly (the VM's jump handler
                            // consumes it, not a Step) -- same LIFO Take() as everywhere else.
                            // Captured as a CondJumpLine (rather than emitted as text here)
                            // so the structuring pass below (Structure/Render) can fold this,
                            // its matching label, and everything in between back into a real
                            // if/[else] block -- see that pass's own comments for exactly
                            // which shapes it recognizes and what it falls back to otherwise.
                            string? cond0 = null;
                            if (insn.StepOpcode == 0x13) // JumpIfFalse
                            {
                                var cond = Take(1);
                                cond0 = cond.Count > 0 ? cond[0].Text : "?";
                            }

                            if (stack.Count > 0)
                                FlushRemainingStack($"jump at offset {insn.Offset}");

                            if (insn.StepOpcode == 0x13)
                                lines.Add(new CondJumpLine(cond0!, targetLabel));
                            else if (insn.StepOpcode == 0x12) // Jump
                                lines.Add(new UncondJumpLine(targetLabel));
                            else // LoopOrJump (0x1C) -- a backward branch (a loop), not an if/else shape;
                                Emit($"LoopOrJump({targetLabel});"); // left as opaque text, not fed to Structure()
                            break;
                        }

                    case "stack":
                        {
                            // Raw main-dispatch ops with no Zenkai.g4 call syntax of their
                            // own (0x03 type-handler dispatch, 0x04-0x10 arithmetic/logic/
                            // comparison, 0x14 StackPop, 0x15-0x1A StepTypePost_* type-handler
                            // post-ops, 0x1B PushToAltStack). Arity/symbol/produces-result
                            // per opcode, confirmed against SView_Decoder's own handling of
                            // each (0x04-0x10 case blocks there do exactly this pop-2/push-1
                            // math already) -- this just mirrors that here instead of
                            // discarding whatever's pending as a fake "orphan".
                            var (arity, symbol, produces) = insn.StepOpcode switch
                            {
                                0x04 => (2, "AND", true),
                                0x05 => (1, "NOT", true),
                                0x06 => (1, "NEG", true),
                                0x07 => (2, "+", true),
                                0x08 => (2, "-", true),
                                0x09 => (2, "*", true),
                                0x0A => (2, "/", true),
                                0x0B => (2, "==", true),
                                0x0C => (2, "!=", true),
                                0x0D => (2, ">", true),
                                0x0E => (2, ">=", true),
                                0x0F => (2, "<", true),
                                0x10 => (2, "<=", true),
                                0x14 => (1, "StackPop", false),
                                0x1B => (1, "PushToAltStack", false),
                                _ => (1, $"op_0x{insn.StepOpcode:X2}", false), // 0x03, 0x15-0x1A -- semantics too uncertain to claim a result
                            };

                            var operands = Take(arity);
                            string O(int i) => i < operands.Count ? Inline(operands[i]) : "?";

                            // Constant folding (display only): arithmetic on plain literals is shown as the literal it computes, so
                            // `PushByte 8; StackNegate` reads `-8` (a legal Zenkai literal) instead of a temp `var t = (NEG(8));`.
                            // Same 32-bit signed semantics as the VM handlers; division by zero is left unfolded.
                            if (produces && operands.Count == arity && operands.All(o => o.Kind == ValKind.Literal && long.TryParse(o.Text, out _)))
                            {
                                var n = operands.Select(o => (int)long.Parse(o.Text)).ToArray();
                                int? folded = insn.StepOpcode switch
                                {
                                    0x06 => unchecked(-n[0]),
                                    0x07 => unchecked(n[0] + n[1]),
                                    0x08 => unchecked(n[0] - n[1]),
                                    0x09 => unchecked(n[0] * n[1]),
                                    0x0A when n[1] != 0 && !(n[0] == int.MinValue && n[1] == -1) => n[0] / n[1],
                                    _ => null,
                                };
                                if (folded is int f)
                                {
                                    stack.Add(new Val(f.ToString(), ValKind.Literal));
                                    break;
                                }
                            }

                            if (!produces)
                            {
                                string text = $"{symbol}({string.Join(", ", operands.Select(o => o.Text))})";
                                sb.AppendLine($"// raw main-dispatch opcode 0x{insn.StepOpcode:X2} at offset {insn.Offset}: {text}");
                                break;
                            }

                            bool isCompare = insn.StepOpcode is >= 0x0B and <= 0x10;
                            if (arity == 2)
                            {
                                string text = $"{O(0)} {symbol} {O(1)}";
                                // Only a compare of plain literals/calls is valid Zenkai `cond` syntax.
                                bool simple = operands.Count == 2 && operands.All(o => o.Kind is ValKind.Literal or ValKind.Call);
                                stack.Add(new Val(text, isCompare && simple ? ValKind.Compare : ValKind.Other));
                            }
                            else
                            {
                                stack.Add(new Val($"{symbol}({string.Join(", ", operands.Select(o => o.Text))})", ValKind.Other));
                            }
                            break;
                        }

                    case "end":
                        FlushRemainingStack("end");
                        break;

                    case "invalid":
                        {
                            if (stack.Count > 0) FlushRemainingStack($"invalid opcode at offset {insn.Offset}");
                            int remaining = Math.Max(0, data.Length - insn.Offset);
                            var head = data.Skip(insn.Offset).Take(24).Select(b => b.ToString("X2"));
                            Emit($"// invalid main opcode 0x{insn.StepOpcode:X2} at offset {insn.Offset}: the game's table only has 0x00-0x1D, so this is not script bytecode " +
                                 $"(text / other data, or a wrong start offset). Decoding stopped; {remaining} byte(s) left: {string.Join(" ", head)}{(remaining > 24 ? " ..." : "")}");
                            break;
                        }
                }
            }

            // A jump can target the byte just past the final instruction (typically "skip the rest, fall off the end"). No instruction
            // sits at that offset, so the loop above never emitted its label and Structure() could not find it to fold the JumpIfFalse
            // into an if block -- it printed the raw `// JumpIfFalse condition` + `JumpIfFalse(L0);` pair instead. Put those labels at the end.
            var instructionOffsets = insns.Select(i => i.Offset).ToHashSet();
            foreach (var t in targets)
                if (!instructionOffsets.Contains(t))
                    lines.Add(new LabelLine(labelNames[t]));

            var nodes = Structure(lines, 0, lines.Count);
            var outSb = new StringBuilder();
            Render(nodes, outSb, 0);
            return outSb.ToString();
        }

        // Folds the canonical shapes ZenkaiAssembler.EmitIf actually generates back into
        // real if/[else] blocks:
        //   JumpIfFalse(cond, L0); <then...> L0:                         -> if (cond) { <then...> }
        //   JumpIfFalse(cond, L0); <then...> Jump(L1); L0: <else...> L1: -> if (cond) { <then...> } else { <else...> }
        // Only ever recognizes these two exact shapes -- anything else (the matching label
        // missing from this scope, no trailing Jump before it, or that Jump's own target
        // missing too) is left as plain, unrestructured jump/label lines, same output as
        // before this pass existed. This deliberately does NOT attempt loops (LoopOrJump
        // never reaches here, see the "jump" case above) or hand-written gotos -- only the
        // two shapes this project's own compiler is known to emit.
        private static List<Node> Structure(List<Line> lines, int start, int end)
        {
            var result = new List<Node>();
            int i = start;
            while (i < end)
            {
                switch (lines[i])
                {
                    case CondJumpLine cj:
                        {
                            int labelIdx = FindLabel(lines, cj.Target, i + 1, end);
                            if (labelIdx < 0)
                            {
                                result.Add(new RawNode($"// JumpIfFalse condition: {cj.Cond}"));
                                result.Add(new RawNode($"JumpIfFalse({cj.Target});"));
                                i++;
                                break;
                            }

                            // else pattern: the line right before the label is an
                            // unconditional Jump, and ITS target also has a label in scope.
                            if (labelIdx - 1 > i && lines[labelIdx - 1] is UncondJumpLine uj)
                            {
                                int elseEnd = FindLabel(lines, uj.Target, labelIdx, end);
                                if (elseEnd >= 0)
                                {
                                    var thenNodes = Structure(lines, i + 1, labelIdx - 1);
                                    var elseNodes = Structure(lines, labelIdx + 1, elseEnd);
                                    // An empty else is just a Jump over nothing -- same behaviour
                                    // as a plain if, so don't print a pointless `else { }`.
                                    if (elseNodes.Count == 0)
                                        result.Add(new IfNode(cj.Cond, thenNodes));
                                    else
                                        result.Add(new IfElseNode(cj.Cond, thenNodes, elseNodes));
                                    i = elseEnd + 1;
                                    break;
                                }
                            }

                            // Plain if, no else.
                            var thenOnly = Structure(lines, i + 1, labelIdx);
                            result.Add(new IfNode(cj.Cond, thenOnly));
                            i = labelIdx + 1;
                            break;
                        }

                    case UncondJumpLine uj2:
                        result.Add(new RawNode($"Jump({uj2.Target});"));
                        i++;
                        break;

                    case LabelLine lbl:
                        result.Add(new RawNode($"{lbl.Name}:"));
                        i++;
                        break;

                    case CodeLine cl:
                        result.Add(new RawNode(cl.Text));
                        i++;
                        break;
                }
            }
            return result;
        }

        private static int FindLabel(List<Line> lines, string name, int from, int to)
        {
            for (int j = from; j < to; j++)
                if (lines[j] is LabelLine l && l.Name == name) return j;
            return -1;
        }

        private static void Render(List<Node> nodes, StringBuilder outSb, int indent)
        {
            string pad = new string(' ', indent * 4);
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case RawNode r:
                        outSb.AppendLine(pad + r.Text);
                        break;

                    case IfNode iff:
                        outSb.AppendLine($"{pad}if ({iff.Cond})");
                        RenderBody(iff.Then, outSb, pad, indent);
                        break;

                    case IfElseNode ie:
                        outSb.AppendLine($"{pad}if ({ie.Cond})");
                        RenderBody(ie.Then, outSb, pad, indent);
                        outSb.AppendLine($"{pad}else");
                        RenderBody(ie.Else, outSb, pad, indent);
                        break;
                }
            }
        }

        // Braces are optional for a single-statement body -- same as C#. Only a body that
        // is EXACTLY one plain statement (not a comment, not a label, not itself a nested
        // if) qualifies; anything else (multiple statements, or a comment alongside the
        // statement) keeps real braces so nothing is ambiguous about what's inside the if.
        private static bool IsBraceless(List<Node> body) =>
            body.Count == 1 && body[0] is RawNode r && r.Text.EndsWith(";") && !r.Text.StartsWith("//");

        private static void RenderBody(List<Node> body, StringBuilder outSb, string pad, int indent)
        {
            if (IsBraceless(body))
            {
                outSb.AppendLine($"{pad}    {((RawNode)body[0]).Text}");
                return;
            }

            outSb.AppendLine($"{pad}{{");
            Render(body, outSb, indent + 1);
            outSb.AppendLine($"{pad}}}");
        }

        private static List<RawInsn> Decode(byte[] data)
        {
            var result = new List<RawInsn>();
            int offset = 0;

            while (offset < data.Length)
            {
                int start = offset;
                byte op = data[offset++];

                switch (op)
                {
                    case 0x00: // PushByte
                        {
                            byte value = data[offset++];
                            result.Add(new RawInsn(start, offset, "push", new List<long> { value }));
                            break;
                        }
                    case 0x01: // PushVarint
                        {
                            long v = SView_Tools.ZigZagDecode(ReadVarint(data, ref offset));
                            result.Add(new RawInsn(start, offset, "push", new List<long> { v }));
                            break;
                        }
                    case 0x02: // Step
                        {
                            byte opcodeIndex = data[offset++];
                            result.Add(new RawInsn(start, offset, "step", new List<long>(), StepOpcode: opcodeIndex));
                            break;
                        }
                    case 0x11: // END
                        result.Add(new RawInsn(start, offset, "end", new List<long>()));
                        return result;

                    case 0x12: // Jump
                    case 0x13: // JumpIfFalse
                    case 0x1C: // LoopOrJump
                        {
                            sbyte delta = unchecked((sbyte)data[offset++]);
                            int afterOperand = offset;
                            int target = op == 0x1C ? (afterOperand - delta) : (afterOperand + delta);
                            result.Add(new RawInsn(start, offset, "jump", new List<long>(), StepOpcode: op, JumpTarget: target, IsLoop: op == 0x1C));
                            break;
                        }

                    case 0x1D: // PushMultiVarint
                        {
                            byte count = data[offset++];
                            var values = new List<long>();
                            for (int i = 0; i < count; i++)
                                values.Add(SView_Tools.ZigZagDecode(ReadVarint(data, ref offset)));
                            result.Add(new RawInsn(start, offset, "push", values));
                            break;
                        }

                    // 0x03 (type-handler dispatch), 0x04-0x10 (stack ops), 0x14 (StackPop),
                    // 0x15-0x1A (StepTypePost_*), 0x1B (PushToAltStack): no source syntax yet.
                    case 0x03:
                        offset++; // consumes a type-handler index byte
                        result.Add(new RawInsn(start, offset, "stack", new List<long>(), StepOpcode: op));
                        break;
                    case 0x15: case 0x16: case 0x17: case 0x18: case 0x19: case 0x1A:
                        // CONFIRMED via IDA 2026-09-16 (decompiled all six StepTypePost_*
                        // handlers at 0x8009131/0x8009161/0x8009191/0x80091BD/0x80091ED/0x8009225):
                        // each reads exactly ONE operand byte and reuses that single value as
                        // the index into BOTH BytecodeVM_OpcodeTable and BytecodeVM_TypeHandlerTable
                        // (`v3 = *(*a2)++; ... OpcodeTable[v3] ... TypeHandlerTable[v3]`). The
                        // previous `offset += 2` here (opcode-table byte + a SEPARATE type-handler
                        // byte) was wrong -- it ate one extra byte per StepTypePost_* instruction,
                        // silently desyncing every instruction after it for the rest of the script.
                        offset += 1; // single opcode-table-index byte, reused for the type handler too
                        result.Add(new RawInsn(start, offset, "stack", new List<long>(), StepOpcode: op));
                        break;

                    case 0x04: case 0x05: case 0x06: case 0x07: case 0x08: case 0x09: case 0x0A: case 0x0B:
                    case 0x0C: case 0x0D: case 0x0E: case 0x0F: case 0x10: case 0x14: case 0x1B:
                        result.Add(new RawInsn(start, offset, "stack", new List<long>(), StepOpcode: op));
                        break;

                    default:
                        // The game's main dispatch table has exactly 30 entries (0x00-0x1D). Anything above is NOT an instruction: this is data
                        // (text, a compressed blob, another table) or a script decoded from the wrong offset. Stop here instead of pretending
                        // every following byte is an opcode, which used to print hundreds of lines of "arity mismatch" noise.
                        result.Add(new RawInsn(start, offset, "invalid", new List<long>(), StepOpcode: op));
                        return result;
                }
            }

            return result;
        }

        private static int ReadVarint(byte[] data, ref int offset)
        {
            int value = 0;
            while (true)
            {
                byte b = data[offset++];
                value = (value << 7) | (b & 0x7F);
                if ((b & 0x80) == 0)
                    break;
            }
            return value;
        }
    }
}
