namespace Legacy
{
    internal static class SView_Decoder
    {
        internal class Instruction
        {
            public int Offset { get; set; }
            public string Name { get; set; } = "";
            public List<long> Args { get; set; } = new();
        }

        internal static List<Instruction> Decode(byte[] data)
        {
            var result = new List<Instruction>();
            var vmStack = new Stack<long>();
            int offset = 0;

            while (offset < data.Length)
            {
                int start = offset;
                byte op = data[offset++];

                switch (op)
                {
                    case 0x00: // PushByte
                        {
                            if (offset >= data.Length) return result;
                            byte value = data[offset++];
                            vmStack.Push(value);
                            result.Add(new Instruction { Offset = start, Name = "PushByte", Args = new List<long> { value } });
                            break;
                        }
                    case 0x01: // PushVarint
                        {
                            int raw = ReadVarint(data, ref offset);
                            long v = SView_Tools.ZigZagDecode(raw);
                            vmStack.Push(v);
                            result.Add(new Instruction { Offset = start, Name = "PushVarint", Args = new List<long> { v } });
                            break;
                        }
                    case 0x02: // Step
                        {
                            if (offset >= data.Length) return result;
                            byte opcodeIndex = data[offset++];
                            string rawName = BYTECODE_VM.OP_CODES.TryGetValue(opcodeIndex, out var n) ? n : $"UNKNOWN_{opcodeIndex:X2}";
                            string name = SView_Tools.CleanName(rawName);
                            result.Add(new Instruction { Offset = start, Name = name, Args = new List<long>() });
                            break;
                        }
                    case 0x03: // sub_8008FA4 (type handler dispatch)
                        {
                            // CONFIRMED via IDA 2026-09-16 (sub_8008FA4, 0x8008FA4): reads ONE
                            // operand byte (index into BytecodeVM_TypeHandlerTable). This case
                            // previously consumed no operand byte at all, desyncing everything
                            // after it in any script that used this instruction.
                            if (offset >= data.Length) return result;
                            byte typeHandlerIndex = data[offset++];
                            result.Add(new Instruction { Offset = start, Name = "sub_8008FA4", Args = new List<long> { typeHandlerIndex } });
                            break;
                        }
                    case 0x04: // StackAnd
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long r = (a != 0 && b != 0) ? 1 : 0;
                            vmStack.Push(r);
                            result.Add(new Instruction { Offset = start, Name = "StackAnd", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x05: // StackNot
                        {
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long r = a == 0 ? 1 : 0;
                            vmStack.Push(r);
                            result.Add(new Instruction { Offset = start, Name = "StackNot", Args = new List<long> { a } });
                            break;
                        }
                    case 0x06: // StackNegate
                        {
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            vmStack.Push(-a);
                            result.Add(new Instruction { Offset = start, Name = "StackNegate", Args = new List<long> { a } });
                            break;
                        }
                    case 0x07: // StackAdd
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            vmStack.Push(a + b);
                            result.Add(new Instruction { Offset = start, Name = "StackAdd", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x08: // StackSub
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            vmStack.Push(a - b);
                            result.Add(new Instruction { Offset = start, Name = "StackSub", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x09: // StackMul
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            vmStack.Push(a * b);
                            result.Add(new Instruction { Offset = start, Name = "StackMul", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x0A: // StackDiv
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long r = b != 0 ? a / b : 0;
                            vmStack.Push(r);
                            result.Add(new Instruction { Offset = start, Name = "StackDiv", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x0B: // StackCmpEq
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            vmStack.Push(a == b ? 1 : 0);
                            result.Add(new Instruction { Offset = start, Name = "StackCmpEq", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x0C: // StackCmpNe
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            vmStack.Push(a != b ? 1 : 0);
                            result.Add(new Instruction { Offset = start, Name = "StackCmpNe", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x0D: // StackCmpGt
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            vmStack.Push(a > b ? 1 : 0);
                            result.Add(new Instruction { Offset = start, Name = "StackCmpGt", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x0E: // StackCmpGe
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            vmStack.Push(a >= b ? 1 : 0);
                            result.Add(new Instruction { Offset = start, Name = "StackCmpGe", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x0F: // StackCmpLt
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            vmStack.Push(a < b ? 1 : 0);
                            result.Add(new Instruction { Offset = start, Name = "StackCmpLt", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x10: // StackCmpLe
                        {
                            long b = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            vmStack.Push(a <= b ? 1 : 0);
                            result.Add(new Instruction { Offset = start, Name = "StackCmpLe", Args = new List<long> { a, b } });
                            break;
                        }
                    case 0x11: // END - null entry in dispatch table
                        {
                            result.Add(new Instruction { Offset = start, Name = "END" });
                            return result;
                        }
                    case 0x12: // Jump
                        {
                            // CONFIRMED via IDA disasm 2026-09 (BytecodeVM_Jump, 0x80090FE):
                            // operand is ONE SIGNED BYTE, not a LEB128 varint. Target =
                            // (address immediately after this operand byte) + signed value.
                            // The old varint+zigzag decode here was WRONG and would silently
                            // desync every following instruction in any script using Jump.
                            if (offset >= data.Length) return result;
                            sbyte delta = unchecked((sbyte)data[offset++]);
                            long target = offset + delta;
                            result.Add(new Instruction { Offset = start, Name = "Jump", Args = new List<long> { target } });
                            break;
                        }
                    case 0x13: // JumpIfFalse
                        {
                            // CONFIRMED via IDA disasm 2026-09 (BytecodeVM_JumpIfFalse,
                            // 0x800910A): same 1-signed-byte operand as Jump, not a varint.
                            if (offset >= data.Length) return result;
                            sbyte delta = unchecked((sbyte)data[offset++]);
                            long cond = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            long target = offset + delta;
                            result.Add(new Instruction { Offset = start, Name = "JumpIfFalse", Args = new List<long> { cond, target } });
                            break;
                        }
                    case 0x14: // StackPop
                        {
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            result.Add(new Instruction { Offset = start, Name = "StackPop", Args = new List<long> { a } });
                            break;
                        }
                    // CONFIRMED via IDA 2026-09-16 (decompiled all six StepTypePost_* handlers
                    // at 0x8009131/0x8009161/0x8009191/0x80091BD/0x80091ED/0x8009225): each
                    // reads exactly ONE operand byte and reuses that single value as the index
                    // into BOTH BytecodeVM_OpcodeTable and BytecodeVM_TypeHandlerTable
                    // (`v3 = *(*a2)++; ... OpcodeTable[v3] ... TypeHandlerTable[v3]`). This
                    // family previously consumed no operand byte at all here, desyncing
                    // everything after it in any script that used one of these instructions --
                    // the most likely cause of the widespread garbage seen in decoded scripts.
                    case 0x15: // StepTypePost_Pop
                        {
                            if (offset >= data.Length) return result;
                            byte idx = data[offset++];
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_Pop", Args = new List<long> { idx } });
                            break;
                        }
                    case 0x16: // StepTypePost_IncPeek
                        {
                            if (offset >= data.Length) return result;
                            byte idx = data[offset++];
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_IncPeek", Args = new List<long> { idx } });
                            break;
                        }
                    case 0x17: // StepTypePost_Peek
                        {
                            if (offset >= data.Length) return result;
                            byte idx = data[offset++];
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_Peek", Args = new List<long> { idx } });
                            break;
                        }
                    case 0x18: // StepTypePost_PopDec
                        {
                            if (offset >= data.Length) return result;
                            byte idx = data[offset++];
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_PopDec", Args = new List<long> { idx } });
                            break;
                        }
                    case 0x19: // StepTypePost_DecPeek
                        {
                            if (offset >= data.Length) return result;
                            byte idx = data[offset++];
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_DecPeek", Args = new List<long> { idx } });
                            break;
                        }
                    case 0x1A: // StepTypePost_PeekDec
                        {
                            if (offset >= data.Length) return result;
                            byte idx = data[offset++];
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_PeekDec", Args = new List<long> { idx } });
                            break;
                        }
                    case 0x1B: // PushToAltStack
                        {
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            result.Add(new Instruction { Offset = start, Name = "PushToAltStack", Args = new List<long> { a } });
                            break;
                        }
                    case 0x1C: // LoopOrJump
                        {
                            // CONFIRMED via IDA disasm 2026-09 (BytecodeVM_LoopOrJump,
                            // 0x800926C): NOT a plain jump. Also a 1-signed-byte operand
                            // (not a varint), but it's a decrement-and-branch (dbnz):
                            // decrements a per-nesting-depth loop counter (ctx+0x44); while
                            // the counter hasn't reached 1 it branches BACKWARD by the
                            // operand (pc -= delta); on the final iteration it falls through
                            // with no jump at all. The "target" shown here is the
                            // backward-branch case only -- it will not actually jump every
                            // time this instruction executes.
                            if (offset >= data.Length) return result;
                            sbyte delta = unchecked((sbyte)data[offset++]);
                            long target = offset - delta;
                            result.Add(new Instruction { Offset = start, Name = "LoopOrJump", Args = new List<long> { target } });
                            break;
                        }
                    case 0x1D: // PushMultiVarint
                        {
                            byte count = data[offset++];
                            var values = new List<long>();
                            for (int i = 0; i < count; i++)
                            {
                                int raw = ReadVarint(data, ref offset);
                                long v = SView_Tools.ZigZagDecode(raw);
                                values.Add(v);
                            }
                            result.Add(new Instruction { Offset = start, Name = "PushMultiVarint", Args = values });
                            break;
                        }
                    // 0x1E was previously treated as a second END marker. CONFIRMED via IDA
                    // 2026-09 (BytecodeVM_MainDispatchTable, 0x83B5C00): the real dispatch
                    // table has exactly 30 entries (opcodes 0x00-0x1D); 0x11 is the ONLY
                    // real terminator (a null table slot). Opcode 0x1E can never legitimately
                    // appear in a real script -- removed the fake case so it now correctly
                    // falls into `default` (unknown opcode) instead of silently pretending
                    // to be a valid terminator.
                    default:
                        {
                            result.Add(new Instruction { Offset = start, Name = $"UNKNOWN_{op:X2}" });
                            return result;
                        }
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