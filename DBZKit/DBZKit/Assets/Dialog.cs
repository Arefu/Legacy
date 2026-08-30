using System.Text;

enum DialogMode : byte
{
    DIALOG_SCRIPT = 0x0,
    DIALOG_TEXT = 0x1,
    DIALOG_TEXT_JCALG1 = 0x2,
    DIALOG_TEXT_INTERPOLATE = 0x3,
    DIALOG_TEXT_JCALG1_INTERPOLATE = 0x4,
    DIALOG_JUMP = 0x5,
}

class DialogEntry
{
    public uint Address;
    public uint EndOffset;
    public ushort Index;
    public DialogMode Mode;
    public byte CharacterId;
    public string DecodedText;
    public byte[] RawScript;
    public string Notes;
}

class DialogParser
{
    const uint GBA_ROM_BASE = 0x08000000;
    byte[] rom;

    public DialogParser(byte[] romData) { rom = romData; }

    uint ToOffset(uint gbaAddr) => gbaAddr - GBA_ROM_BASE;
    uint ToAddr(uint offset) => offset + GBA_ROM_BASE;

    public List<DialogEntry> ParseAll(uint startAddr, uint endAddr)
    {
        var results = new List<DialogEntry>();
        uint pos = ToOffset(startAddr);
        uint endOff = ToOffset(endAddr);

        while (pos < endOff)
        {
            var entry = ParseEntry(ToAddr(pos));
            results.Add(entry);

            if (entry.EndOffset <= pos)
            {
                entry.Notes += " [WARNING: length unknown, stopping]";
                break;
            }

            pos = entry.EndOffset;
        }

        return results;
    }

    public DialogEntry ParseEntry(uint address)
    {
        uint off = ToOffset(address);
        var entry = new DialogEntry { Address = address };

        entry.Index = (ushort)(rom[off] | (rom[off + 1] << 8));
        entry.Mode = (DialogMode)rom[off + 2];

        uint pos = off + 3;

        switch (entry.Mode)
        {
            case DialogMode.DIALOG_SCRIPT:
                pos = SkipScript(pos, out entry.RawScript);
                entry.DecodedText = "-script-";
                entry.EndOffset = pos;
                break;

            case DialogMode.DIALOG_TEXT:
                entry.CharacterId = rom[pos];
                pos++;
                entry.DecodedText = DecodeCharmapText(pos, out pos);
                entry.EndOffset = pos;
                break;

            case DialogMode.DIALOG_TEXT_JCALG1:
                entry.CharacterId = rom[pos];
                pos++;
                entry.DecodedText = "[JCALG1 - TODO decompress]";
                entry.RawScript = rom.AsSpan((int)pos, 16).ToArray();
                entry.EndOffset = 0;
                entry.Notes = "JCALG1 length unknown";
                break;

            case DialogMode.DIALOG_TEXT_INTERPOLATE:
                entry.CharacterId = rom[pos];
                pos++;
                uint formatStringPtr = ReadU32(pos);
                pos += 4;
                uint scriptData = ReadU32(pos);
                pos += 4;
                entry.DecodedText = ReadAsciiString(pos, out pos);
                entry.Notes = $"FormatPtr={formatStringPtr:X8} ScriptData={scriptData:X8}";
                // align to 4 bytes from entry start
                pos = AlignTo4(off, pos);
                entry.EndOffset = pos;
                break;

            case DialogMode.DIALOG_TEXT_JCALG1_INTERPOLATE:
                entry.CharacterId = rom[pos];
                pos++;
                entry.DecodedText = "[JCALG1_INTERPOLATE - TODO]";
                entry.EndOffset = 0;
                entry.Notes = "JCALG1_INTERPOLATE length unknown";
                break;

            case DialogMode.DIALOG_JUMP:
                entry.Notes = $"Jump target bytes: {rom[pos]:X2} {rom[pos + 1]:X2}";
                entry.EndOffset = pos + 2;
                break;

            default:
                entry.Notes = $"Unknown mode {entry.Mode}";
                entry.EndOffset = 0;
                break;
        }

        return entry;
    }

    /// <summary>
    /// Walks DIALOG_SCRIPT bytecode opcode-by-opcode (per BytecodeVM_MainDispatchTable),
    /// consuming the correct operand size for each, until terminator 0x11 is
    /// encountered AS AN OPCODE (not as operand data).
    /// </summary>
    uint SkipScript(uint pos, out byte[] rawBytes)
    {
        uint start = pos;

        while (true)
        {
            byte opcode = rom[pos];
            pos++;

            if (opcode == 0x11)
                break; // terminator

            switch (opcode)
            {
                case 0x00: // PushByte
                    pos += 1;
                    break;

                case 0x01: // PushVarint (LEB128)
                    pos = SkipVarint(pos);
                    break;

                case 0x02: // Step (sub-opcode byte into BytecodeVM_OpcodeTable)
                    pos += 1;
                    break;

                case 0x03: // sub_8008FA4 (type handler index byte)
                    pos += 1;
                    break;

                case 0x04:
                case 0x05:
                case 0x06:
                case 0x07:
                case 0x08:
                case 0x09:
                case 0x0A:
                case 0x0B:
                case 0x0C:
                case 0x0D:
                case 0x0E:
                case 0x0F:
                case 0x10:
                    // StackAnd..StackCmpLe - no operands
                    break;

                case 0x12: // Jump - TODO: confirm operand size (guessing 1 byte)
                case 0x13: // JumpIfFalse - TODO: confirm operand size (guessing 1 byte)
                    pos += 1;
                    break;

                case 0x14: // StackPop - no operands
                    break;

                case 0x15:
                case 0x16:
                case 0x17:
                case 0x18:
                case 0x19:
                case 0x1A:
                    // StepTypePost_* - sub-opcode byte
                    pos += 1;
                    break;

                case 0x1B: // PushToAltStack - no operands (assumed)
                    break;

                case 0x1C: // LoopOrJump - TODO: confirm operand size (guessing 1 byte)
                    pos += 1;
                    break;

                case 0x1D: // PushMultiVarint - count byte + N varints
                    pos = SkipMultiVarint(pos);
                    break;

                default:
                    // Unknown opcode - bail out so we don't run away
                    rawBytes = rom.AsSpan((int)start, (int)(pos - start)).ToArray();
                    return pos; // caller should check for weirdness via Notes if needed
            }
        }

        rawBytes = rom.AsSpan((int)start, (int)(pos - start)).ToArray();
        return pos;
    }

    uint SkipVarint(uint pos)
    {
        byte b;
        do
        {
            b = rom[pos];
            pos++;
        }
        while ((b & 0x80) != 0); // adjust if your LEB128 variant differs (left-accumulate vs right)
        return pos;
    }

    uint SkipMultiVarint(uint pos)
    {
        byte count = rom[pos];
        pos++;
        for (int i = 0; i < count; i++)
            pos = SkipVarint(pos);
        return pos;
    }

    uint AlignTo4(uint entryStart, uint pos)
    {
        uint len = pos - entryStart;
        uint aligned = (len + 3) & ~3u;
        return entryStart + aligned;
    }

    string DecodeCharmapText(uint pos, out uint endPos)
    {
        var sb = new StringBuilder();
        while (rom[pos] != 0x00)
        {
            sb.Append((char)rom[pos]);
            pos++;
        }
        endPos = pos + 1;
        return sb.ToString();
    }

    string ReadAsciiString(uint pos, out uint endPos)
    {
        var sb = new StringBuilder();
        while (rom[pos] != 0x00)
        {
            sb.Append((char)rom[pos]);
            pos++;
        }
        endPos = pos + 1;
        return sb.ToString();
    }

    uint ReadU32(uint pos) => BitConverter.ToUInt32(rom, (int)pos);
}