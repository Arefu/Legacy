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

            var sb = new StringBuilder();
            var pendingPushes = new List<long>();

            void FlushOrphanPushes()
            {
                // Shouldn't happen for well-formed scripts (every Push* is consumed by
                // the next Step), but don't silently drop bytes if it does.
                foreach (var v in pendingPushes)
                    sb.AppendLine($"// orphan push: {v}");
                pendingPushes.Clear();
            }

            foreach (var insn in insns)
            {
                if (labelNames.TryGetValue(insn.Offset, out var label))
                    sb.AppendLine($"{label}:");

                switch (insn.Kind)
                {
                    case "push":
                        pendingPushes.AddRange(insn.PushedValues);
                        break;

                    case "step":
                        {
                            if (!OpcodeTable.IndexMap.TryGetValue(insn.StepOpcode, out var op))
                            {
                                sb.AppendLine($"// UNKNOWN_STEP_{insn.StepOpcode:X2}({string.Join(", ", pendingPushes)})");
                                pendingPushes.Clear();
                                break;
                            }
                            if (pendingPushes.Count != op.Arity)
                                sb.AppendLine($"// arity mismatch: {op.Name} expects {op.Arity}, got {pendingPushes.Count}");
                            sb.AppendLine($"{op.Name}({string.Join(", ", pendingPushes)});");
                            pendingPushes.Clear();
                            break;
                        }

                    case "jump":
                        {
                            string opName = insn.StepOpcode switch { 0x12 => "Jump", 0x13 => "JumpIfFalse", 0x1C => "LoopOrJump", _ => "Jump" };
                            // JumpIfFalse pops its condition directly (the VM's jump handler
                            // consumes it, not a Step) -- a pending push right before it is
                            // that condition, not an orphan. KNOWN GAP: the grammar has no
                            // syntax yet for stack-producing ops (0x04-0x10 comparisons/logic),
                            // so a real script whose condition comes from one of those (rather
                            // than a plain PushByte/PushVarint literal) can be disassembled but
                            // not hand-authored from source yet. Neither example script in
                            // Zenkai-Example-Scripts.md exercises JumpIfFalse, so this is
                            // untested against a real ROM case either way.
                            if (opName == "JumpIfFalse" && pendingPushes.Count == 1)
                            {
                                sb.AppendLine($"// JumpIfFalse condition (pushed literal): {pendingPushes[0]}");
                                pendingPushes.Clear();
                            }
                            FlushOrphanPushes();
                            string targetLabel = labelNames.TryGetValue(insn.JumpTarget, out var l) ? l : $"0x{insn.JumpTarget:X}";
                            sb.AppendLine($"{opName}({targetLabel});");
                            break;
                        }

                    case "stack":
                        // No-operand / stack-only main-dispatch ops (0x04-0x10, 0x14, 0x1B,
                        // 0x15-0x1A type-handler post-ops) have no Zenkai.g4 call syntax yet
                        // -- they don't appear in real dialog/cutscene scripts sampled so far
                        // (Zenkai-Example-Scripts.md's two examples use neither), so surface
                        // them as a raw byte comment rather than silently dropping them or
                        // inventing unverified syntax for them.
                        FlushOrphanPushes();
                        sb.AppendLine($"// raw main-dispatch opcode 0x{insn.StepOpcode:X2} at offset {insn.Offset} (no Zenkai.g4 syntax for this yet)");
                        break;

                    case "end":
                        FlushOrphanPushes();
                        break;
                }
            }

            return sb.ToString();
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

                    default:
                        result.Add(new RawInsn(start, offset, "stack", new List<long>(), StepOpcode: op));
                        break;
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
