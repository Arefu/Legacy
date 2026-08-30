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
                    case 0x03: // sub_8008FA4
                        {
                            result.Add(new Instruction { Offset = start, Name = "sub_8008FA4", Args = new List<long>() });
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
                            int raw = ReadVarint(data, ref offset);
                            long target = SView_Tools.ZigZagDecode(raw);
                            result.Add(new Instruction { Offset = start, Name = "Jump", Args = new List<long> { target } });
                            break;
                        }
                    case 0x13: // JumpIfFalse
                        {
                            int raw = ReadVarint(data, ref offset);
                            long target = SView_Tools.ZigZagDecode(raw);
                            long cond = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            result.Add(new Instruction { Offset = start, Name = "JumpIfFalse", Args = new List<long> { cond, target } });
                            break;
                        }
                    case 0x14: // StackPop
                        {
                            long a = vmStack.Count > 0 ? vmStack.Pop() : 0;
                            result.Add(new Instruction { Offset = start, Name = "StackPop", Args = new List<long> { a } });
                            break;
                        }
                    case 0x15: // StepTypePost_Pop
                        {
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_Pop", Args = new List<long>() });
                            break;
                        }
                    case 0x16: // StepTypePost_IncPeek
                        {
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_IncPeek", Args = new List<long>() });
                            break;
                        }
                    case 0x17: // StepTypePost_Peek
                        {
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_Peek", Args = new List<long>() });
                            break;
                        }
                    case 0x18: // StepTypePost_PopDec
                        {
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_PopDec", Args = new List<long>() });
                            break;
                        }
                    case 0x19: // StepTypePost_DecPeek
                        {
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_DecPeek", Args = new List<long>() });
                            break;
                        }
                    case 0x1A: // StepTypePost_PeekDec
                        {
                            result.Add(new Instruction { Offset = start, Name = "StepTypePost_PeekDec", Args = new List<long>() });
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
                            int raw = ReadVarint(data, ref offset);
                            long target = SView_Tools.ZigZagDecode(raw);
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
                    case 0x1E: // END (was 0x11 in original — re-check your terminator byte)
                        {
                            result.Add(new Instruction { Offset = start, Name = "END" });
                            return result;
                        }
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