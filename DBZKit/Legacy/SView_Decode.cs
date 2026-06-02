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

            var vmStack = new Stack<long>(); // IMPORTANT: per-decode stack
            int offset = 0;

            while (offset < data.Length)
            {
                int start = offset;

                byte op = data[offset++];

                switch (op)
                {
                    case 0x00: // PushByte
                        {
                            if (offset >= data.Length)
                                return result;

                            byte value = data[offset++];

                            vmStack.Push(value);

                            result.Add(new Instruction
                            {
                                Offset = start,
                                Name = "PushByte",
                                Args = new List<long> { value }
                            });

                            break;
                        }

                    case 0x02: // Step
                        {
                            if (offset >= data.Length)
                                return result;

                            byte opcodeIndex = data[offset++];

                            string rawName =
                                BYTECODE_VM.OP_CODES.TryGetValue(opcodeIndex, out var n)
                                    ? n
                                    : $"UNKNOWN_{opcodeIndex:X2}";

                            string name = SView_Tools.CleanName(rawName);

                            // OPTIONAL: if you later add signature table, replace this
                            //int argCount = BYTECODE_VM.GetArgCount(opcodeIndex);

                            //var args = new List<long>();

                            //for (int i = 0; i < argCount; i++)
                            //{
                            //    if (vmStack.Count > 0)
                            //        args.Insert(0, vmStack.Pop());
                            //}

                            result.Add(new Instruction
                            {
                                Offset = start,
                                Name = name,
                                Args = new List<long>()
                            });

                            break;
                        }

                    case 0x1D:
                        {
                            byte count = data[offset++];

                            var values = new List<long>();

                            for (int i = 0; i < count; i++)
                            {
                                int raw = ReadVarint(data, ref offset);
                                long v = SView_Tools.ZigZagDecode(raw);

                                values.Add(v);
                            }

                            result.Add(new Instruction
                            {
                                Offset = start,
                                Name = "PushMultiVarint",
                                Args = values
                            });

                            break;
                        }

                    case 0x11: // terminator
                        {
                            result.Add(new Instruction
                            {
                                Offset = start,
                                Name = "END"
                            });

                            return result;
                        }

                    default:
                        {
                            result.Add(new Instruction
                            {
                                Offset = start,
                                Name = $"UNKNOWN_{op:X2}"
                            });

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

            // FORCE 32-bit signed overflow semantics like C
            return value;
        }
    }
}