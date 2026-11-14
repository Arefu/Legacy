using DrGero.IO;
using DrGero.Rendering;

namespace DrGero.Quests
{
    /// <summary>
    /// Reads and writes dialog sequences -- the sequence[]/DialogEntry format documented
    /// in DBZKit/Dialog_Format.md (session 2026-09-14, ground-truthed against the real
    /// YAJIROBE Senzu Bean conversation).
    ///
    /// Modes 0 (DIALOG_SCRIPT), 1 (DIALOG_TEXT), 2 (DIALOG_TEXT_JCALG1), and 3
    /// (DIALOG_TEXT_INTERPOLATE) are all fully handled -- there was no real blocker on
    /// 2/3, they just hadn't been implemented yet: mode 2 is read via the SAME JCALG1
    /// decompressor this project already has for graphics (DrGero.Rendering.JCALG1,
    /// no-header sequential stream, exactly what Dialog_CreateTextFromCompressed_Impl's
    /// j_jcalg1_iwram_dst call does), and mode 3's format string + arg-computing script is
    /// fully traced against the real Yajirobe example. Writing NEVER produces mode 2 or 4
    /// -- there's no JCALG1 *compressor* ported (only a decompressor), but there's also no
    /// reason to want one here: the original game compressed text to save ROM space on
    /// real hardware, and a hand-edited ROM isn't under that constraint, so every line
    /// this writes goes out as plain mode 1 (or mode 3 if it has interpolation args) even
    /// when it was read in as mode 2.
    ///
    /// Mode 4 (DIALOG_TEXT_JCALG1_INTERPOLATE) is read-only-detected, not decoded --
    /// Dialog_Format.md's own notes on it are inconsistent about where the compressed
    /// data actually starts vs. what the pointer field means, so treating it as
    /// understood would be guessing, not building. Mode 5 (DIALOG_JUMP) is likewise
    /// detected-not-decoded: its redirect behavior lives in a vtable that was never
    /// traced (see Dialog_Format.md's own "not yet safe to author against" note).
    /// </summary>
    public enum DialogLineKind { Text, Script, Interpolated }

    public sealed class DialogLine
    {
        public DialogLineKind Kind { get; init; }

        // Text: CharacterId + Text is the line shown.
        // Interpolated: CharacterId as normal; Text is the printf-style FORMAT STRING
        // (e.g. "What's up, %s?"); ScriptBytes is the arg-computing script whose final
        // stack contents become the vsprintf varargs, in push order.
        public byte CharacterId { get; init; }
        public string Text { get; init; } = "";

        // Script: the action to run (no text box), already ending in END (0x11), e.g.
        // from Legacy.Zenkai.ZenkaiAssembler.Assemble("PickUpItem(5);").
        // Interpolated: see Text above.
        public byte[] ScriptBytes { get; init; } = Array.Empty<byte>();

        /// <summary>Convenience for callers written against the old bool-only shape.</summary>
        public bool IsScript => Kind == DialogLineKind.Script;

        public static DialogLine TextLine(byte characterId, string text) => new() { Kind = DialogLineKind.Text, CharacterId = characterId, Text = text };
        public static DialogLine Script(byte[] scriptBytes) => new() { Kind = DialogLineKind.Script, ScriptBytes = scriptBytes };
        public static DialogLine Interpolated(byte characterId, string format, byte[] argScriptBytes) =>
            new() { Kind = DialogLineKind.Interpolated, CharacterId = characterId, Text = format, ScriptBytes = argScriptBytes };
    }

    public sealed class DialogReadResult
    {
        public List<DialogLine> Lines { get; init; } = new();
        public bool FullyUnderstood { get; init; }
        public string? Note { get; init; }
    }

    public static class DialogReader
    {
        // CRASH FIX 2026-09-19: a real crash report showed DialogScanner.
        // LooksLikeDialogSequence's shallow "check the first few entries" heuristic can
        // false-positive on non-dialog data, and this walk (up to maxEntries) then ran
        // off into garbage addresses and threw instead of gracefully giving up. Every
        // raw address here goes through InBounds() before PushPosition/ReadBytesAt now,
        // so a false positive correctly stops and reports itself instead of crashing.
        private static bool InBounds(ROM rom, int addr, int length) =>
            addr >= 0 && length >= 0 && addr <= rom.Length - length;

        /// <summary>
        /// Walks a sequence[] array same as DialogScanner, but keeps the actual text/
        /// script content instead of just scanning for flag writes. Stops (and reports
        /// FullyUnderstood=false) the moment it hits a mode this can't represent, so a
        /// caller never silently drops content from a richer conversation.
        ///
        /// Addresses are plain ROM file offsets throughout (ROM.ReadPointer() already
        /// strips the 0x08 top byte) -- see DialogScanner's doc comment for why the old
        /// "(x & 0xFF000000) == 0x08000000" checks here were dead code.
        /// </summary>
        public static DialogReadResult Read(ROM rom, int sequenceAddr, int maxEntries = 64)
        {
            var lines = new List<DialogLine>();
            if (!InBounds(rom, sequenceAddr, 4))
                return new DialogReadResult { FullyUnderstood = false, Note = "Not a valid ROM pointer." };

            rom.PushPosition(sequenceAddr);
            try
            {
                for (int i = 0; i < maxEntries; i++)
                {
                    if (!InBounds(rom, rom.Position, 4))
                        return new DialogReadResult { Lines = lines, FullyUnderstood = false, Note = $"Entry {i}'s pointer runs past the end of the ROM -- stopping here." };

                    int entryPtr = rom.ReadPointer();
                    if (entryPtr == 0 || (entryPtr >>> 16) == 0) // sentinel: end of sequence
                        return new DialogReadResult { Lines = lines, FullyUnderstood = true };

                    if (!InBounds(rom, entryPtr, 3))
                        return new DialogReadResult { Lines = lines, FullyUnderstood = false, Note = $"Entry {i} points outside the ROM -- stopping here." };

                    rom.PushPosition(entryPtr);
                    rom.Skip(2); // Index -- not needed for reading, entries are walked in array order
                    byte mode = (byte)rom.ReadByte();

                    if (mode == 0 || mode == 3)
                    {
                        // Both modes have a bytecode script whose real end we need the
                        // instruction-aware decoder for, NOT a raw byte search for 0x11 --
                        // a script that pushes the literal value 17 (e.g. PushByte(17),
                        // decimal 17 == 0x11) would falsely match its own operand byte as
                        // the END opcode and truncate mid-script.
                        int scriptStart = mode == 0 ? entryPtr + 3 : entryPtr + 8;
                        if (!InBounds(rom, scriptStart, 1))
                        {
                            rom.PopPosition();
                            return new DialogReadResult { Lines = lines, FullyUnderstood = false, Note = $"Entry {i}'s script address is outside the ROM -- stopping here." };
                        }

                        int readLen = Math.Min(256, rom.Length - scriptStart);
                        byte[] window = rom.ReadBytesAt(scriptStart, readLen);
                        var instructions = InstructionDecoder.Decode(window);
                        var endInstr = instructions.LastOrDefault(instr => instr.Opcode == 0x11); // last: an earlier END can be an early return
                        if (endInstr == null)
                        {
                            rom.PopPosition();
                            return new DialogReadResult { Lines = lines, FullyUnderstood = false, Note = $"Entry {i}'s script didn't terminate within {readLen} bytes -- stopping here." };
                        }
                        byte[] scriptBytes = window[..(endInstr.Offset + 1)];

                        if (mode == 0)
                        {
                            lines.Add(DialogLine.Script(scriptBytes));
                        }
                        else // mode 3: format string pointer sits at entry+4
                        {
                            byte characterId;
                            int formatPtr;
                            rom.PushPosition(entryPtr + 3);
                            characterId = (byte)rom.ReadByte();
                            formatPtr = rom.ReadPointer();
                            rom.PopPosition();

                            string? format = ReadCString(rom, formatPtr);
                            if (format == null)
                            {
                                rom.PopPosition();
                                return new DialogReadResult { Lines = lines, FullyUnderstood = false, Note = $"Entry {i}'s format string pointer is outside the ROM -- stopping here." };
                            }
                            lines.Add(DialogLine.Interpolated(characterId, format, scriptBytes));
                        }
                    }
                    else if (mode == 1)
                    {
                        byte characterId = (byte)rom.ReadByte();
                        string? text = ReadCStringFromCurrentPosition(rom);
                        if (text == null)
                        {
                            rom.PopPosition();
                            return new DialogReadResult { Lines = lines, FullyUnderstood = false, Note = $"Entry {i}'s text ran past the end of the ROM without a terminator -- stopping here." };
                        }
                        lines.Add(DialogLine.TextLine(characterId, text));
                    }
                    else if (mode == 2)
                    {
                        byte characterId = (byte)rom.ReadByte();
                        // No-header JCALG1 stream, exactly what Dialog_CreateTextFromCompressed_Impl
                        // feeds straight into a text box (Dialog_Format.md mode 2). Capped
                        // output size and wrapped in try/catch: mode-byte alone (0-5) doesn't
                        // guarantee what follows is REALLY a valid compressed stream, and a
                        // malformed one could otherwise run the decompressor past sane bounds.
                        string? text = null;
                        try
                        {
                            var decompressed = JCALG1.DecompressSequential(rom, expectedOutputSize: 4096);
                            int nul = Array.IndexOf(decompressed.Data, (byte)0);
                            text = System.Text.Encoding.ASCII.GetString(decompressed.Data, 0, nul >= 0 ? nul : decompressed.Data.Length);
                        }
                        catch
                        {
                            // fall through with text == null
                        }

                        if (text == null)
                        {
                            rom.PopPosition();
                            return new DialogReadResult { Lines = lines, FullyUnderstood = false, Note = $"Entry {i} (mode 2) didn't decompress cleanly -- stopping here." };
                        }
                        lines.Add(DialogLine.TextLine(characterId, text));
                    }
                    else
                    {
                        rom.PopPosition();
                        return new DialogReadResult
                        {
                            Lines = lines,
                            FullyUnderstood = false,
                            Note = $"Entry {i} is mode {mode} (compressed+interpolated text, or jump) -- not supported for editing yet, stopping here.",
                        };
                    }

                    rom.PopPosition();
                }
            }
            finally
            {
                rom.PopPosition();
            }

            return new DialogReadResult { Lines = lines, FullyUnderstood = false, Note = $"Hit the {maxEntries}-entry safety limit." };
        }

        private static string? ReadCString(ROM rom, int addr)
        {
            if (!InBounds(rom, addr, 1)) return null;
            rom.PushPosition(addr);
            string? result = ReadCStringFromCurrentPosition(rom);
            rom.PopPosition();
            return result;
        }

        private static string? ReadCStringFromCurrentPosition(ROM rom)
        {
            var bytes = new List<byte>();
            for (int guard = 0; guard < 512; guard++)
            {
                if (!InBounds(rom, rom.Position, 1)) return null;
                byte b = (byte)rom.ReadByte();
                if (b == 0) break;
                bytes.Add(b);
            }
            return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
        }
    }

    public static class DialogWriter
    {
        /// <summary>
        /// Writes a full conversation to free space: each line's entry bytes first, then
        /// the sequence[] pointer array (N real entries + a null sentinel), and returns
        /// the sequence[] array's address -- what a trigger's dialogArray/payload field
        /// (dataPtr+0x10) should point at.
        ///
        /// Entries chain via NextIndex = "the index of the NEXT slot in sequence[]" (an
        /// array index, not a byte offset -- confirmed, Dialog_Format.md section 3), so a
        /// straight-line conversation just gets NextIndex = its own array index + 1, with
        /// the last real entry pointing at index N (the sentinel slot).
        ///
        /// Interpolated (mode 3) entries need their format string placed somewhere with a
        /// known address before the entry's own bytes (which embed that address) can be
        /// finalized -- handled by allocating each entry's full blob (header + script +
        /// format string) in ONE piece up front, so the format string's address is just
        /// "this blob's address + a known offset" rather than needing a second pass.
        /// </summary>
        public static int WriteSequence(ROM rom, IReadOnlyList<DialogLine> lines)
        {
            if (lines.Count == 0)
                throw new ArgumentException("A dialog sequence needs at least one line.", nameof(lines));

            var entryAddresses = new int[lines.Count];
            for (int i = 0; i < lines.Count; i++)
                entryAddresses[i] = WriteEntry(rom, lines[i], (ushort)(i + 1)); // last real entry points at the sentinel slot (index Count)

            byte[] sequenceBytes = new byte[(lines.Count + 1) * 4];
            for (int i = 0; i < lines.Count; i++)
                BitConverter.GetBytes(entryAddresses[i]).CopyTo(sequenceBytes, i * 4);
            // Last 4 bytes stay zero -- the sentinel (entry>>>16==0) Dialog_ProcessNext checks for.

            int seqFileOffset = rom.AllocateFreeSpace(sequenceBytes.Length);
            rom.WriteBytesAt(seqFileOffset, sequenceBytes);
            return 0x08000000 | seqFileOffset;
        }

        /// <summary>
        /// Writes a whole table: every slot in order, end markers included, so a table with several conversations
        /// (see DialogBranches) keeps all of them. A slot whose entry is unchanged and already continues to the right
        /// slot is pointed at where it is (compressed text stays compressed, nothing is duplicated); anything else is
        /// written new. Returns the table's 0x08-prefixed address and each slot's entry file offset (0 for end markers).
        /// </summary>
        public static (int TableAddress, int[] EntryOffsets) WriteTable(ROM rom, IReadOnlyList<DialogSlot> slots)
        {
            if (slots.Count == 0 || slots[0].IsEnd)
                throw new ArgumentException("A dialog table has to start with a message or script step.", nameof(slots));

            // The table always closes with an end marker, whether or not the caller listed one.
            int count = slots[^1].IsEnd ? slots.Count : slots.Count + 1;
            var offsets = new int[slots.Count];
            byte[] table = new byte[count * 4];

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsEnd)
                {
                    // The marker's value = the step the next talk starts at (0 = from the top). Keep it, or an NPC that
                    // cycles through lines would say only its first one.
                    BitConverter.GetBytes(slot.EndValue).CopyTo(table, i * 4);
                    continue;
                }

                ushort next = (ushort)(slot.NextSlot >= 0 ? slot.NextSlot : i + 1);
                int offset;
                if (slot.ReuseEntryOffset > 0 && slot.ReuseEntryOffset + 2 <= rom.Length
                    && BitConverter.ToUInt16(rom.ReadBytesAt(slot.ReuseEntryOffset, 2), 0) == next)
                {
                    offset = slot.ReuseEntryOffset;
                }
                else if (slot.RawEntry != null)
                {
                    byte[] copy = (byte[])slot.RawEntry.Clone();
                    copy[0] = (byte)(next & 0xFF);
                    copy[1] = (byte)(next >> 8);
                    offset = rom.AllocateFreeSpace(copy.Length);
                    rom.WriteBytesAt(offset, copy);
                }
                else if (slot.Line != null)
                {
                    // A line break in editor text is the game's page break, byte 0x0B (0x0A is not used: 89 against 1 in the US ROM's plain lines).
                    var line = slot.Line.Kind == DialogLineKind.Script ? slot.Line : new DialogLine
                    {
                        Kind = slot.Line.Kind,
                        CharacterId = slot.Line.CharacterId,
                        Text = slot.Line.Text.Replace("\r\n", "\n").Replace('\n', '\x0B'),
                        ScriptBytes = slot.Line.ScriptBytes,
                    };
                    offset = WriteEntry(rom, line, next) & 0x00FFFFFF;
                }
                else
                {
                    throw new ArgumentException($"Slot {i} has nothing to write.", nameof(slots));
                }

                offsets[i] = offset;
                BitConverter.GetBytes(0x08000000 | offset).CopyTo(table, i * 4);
            }

            int tableOffset = rom.AllocateFreeSpace(table.Length);
            rom.WriteBytesAt(tableOffset, table);
            return (0x08000000 | tableOffset, offsets);
        }

        /// <summary>Writes one entry to free space with the given Index (the slot it continues to) and returns its 0x08-prefixed address.</summary>
        public static int WriteEntry(ROM rom, DialogLine line, ushort nextIndex)
        {
            {
                int entryAddress;

                switch (line.Kind)
                {
                    case DialogLineKind.Script:
                        {
                            byte[] entryBytes = new byte[3 + line.ScriptBytes.Length];
                            entryBytes[0] = (byte)(nextIndex & 0xFF);
                            entryBytes[1] = (byte)(nextIndex >> 8);
                            entryBytes[2] = 0; // mode 0
                            line.ScriptBytes.CopyTo(entryBytes, 3);

                            int fileOffset = rom.AllocateFreeSpace(entryBytes.Length);
                            rom.WriteBytesAt(fileOffset, entryBytes);
                            entryAddress = 0x08000000 | fileOffset;
                            break;
                        }

                    case DialogLineKind.Interpolated:
                        {
                            // Layout: [NextIndex(2)][mode=3(1)][CharacterId(1)][FormatStringPtr(4)]
                            // [arg script bytes...][format string bytes + NUL], all in one blob so
                            // FormatStringPtr can point at the tail of the SAME allocation.
                            byte[] formatBytes = System.Text.Encoding.ASCII.GetBytes(line.Text);
                            int headerLen = 8;
                            int blobLen = headerLen + line.ScriptBytes.Length + formatBytes.Length + 1;

                            int fileOffset = rom.AllocateFreeSpace(blobLen);
                            int formatStringAddress = 0x08000000 | (fileOffset + headerLen + line.ScriptBytes.Length);

                            byte[] blob = new byte[blobLen];
                            blob[0] = (byte)(nextIndex & 0xFF);
                            blob[1] = (byte)(nextIndex >> 8);
                            blob[2] = 3; // mode 3
                            blob[3] = line.CharacterId;
                            BitConverter.GetBytes(formatStringAddress).CopyTo(blob, 4);
                            line.ScriptBytes.CopyTo(blob, headerLen);
                            formatBytes.CopyTo(blob, headerLen + line.ScriptBytes.Length);
                            // final byte stays 0 -- NUL terminator

                            rom.WriteBytesAt(fileOffset, blob);
                            entryAddress = 0x08000000 | fileOffset;
                            break;
                        }

                    default: // Text
                        {
                            byte[] textBytes = System.Text.Encoding.ASCII.GetBytes(line.Text);
                            byte[] entryBytes = new byte[4 + textBytes.Length + 1];
                            entryBytes[0] = (byte)(nextIndex & 0xFF);
                            entryBytes[1] = (byte)(nextIndex >> 8);
                            entryBytes[2] = 1; // mode 1
                            entryBytes[3] = line.CharacterId;
                            textBytes.CopyTo(entryBytes, 4);
                            entryBytes[^1] = 0; // NUL terminator

                            int fileOffset = rom.AllocateFreeSpace(entryBytes.Length);
                            rom.WriteBytesAt(fileOffset, entryBytes);
                            entryAddress = 0x08000000 | fileOffset;
                            break;
                        }
                }

                return entryAddress;
            }
        }
    }

    /// <summary>One slot of a dialog table as an editor holds it: an end marker, or an entry to write (see DialogWriter.WriteTable).</summary>
    public sealed class DialogSlot
    {
        /// <summary>An end marker (a slot that is not a pointer): the conversation running into it stops.</summary>
        public bool IsEnd { get; init; }
        /// <summary>End markers: the step the next conversation with this character starts at (0 = from the top).</summary>
        public uint EndValue { get; init; }
        /// <summary>The entry's content, for when it has to be written new.</summary>
        public DialogLine? Line { get; init; }
        /// <summary>An entry kept byte for byte apart from its Index (a mode-5 jump to another table).</summary>
        public byte[]? RawEntry { get; init; }
        /// <summary>File offset of the entry as it already is in the ROM, when its content is unchanged. 0 = write it new.</summary>
        public int ReuseEntryOffset { get; init; }
        /// <summary>The slot this entry continues to. -1 = the one after it (true of all but 2 of the 2940 entries in the US ROM).</summary>
        public int NextSlot { get; init; } = -1;
    }

    /// <summary>One slot of a dialog table as read from the ROM.</summary>
    public sealed class DialogTableSlot
    {
        public bool IsEnd { get; init; }
        /// <summary>End markers: the raw value, = the step the next talk starts at (see DialogBranches.NextTalkSlot).</summary>
        public uint EndValue { get; init; }
        public int EntryOffset { get; init; }
        public byte Mode { get; init; }
        /// <summary>The slot this entry continues to (its Index field).</summary>
        public int Index { get; init; }
        /// <summary>Modes 0-4. Text keeps its leading position/centre characters; a page break (0x0B) is '\n'.</summary>
        public DialogLine? Line { get; init; }
        /// <summary>Mode 5 (jump to another table): the 8 entry bytes.</summary>
        public byte[]? RawEntry { get; init; }
        public int JumpTarget { get; init; }
    }

    public static class DialogTableReader
    {
        /// <summary>
        /// Every slot of the table at <paramref name="sequenceAddr"/>, end markers included, up to the last slot any of
        /// its conversations reaches. Null when it is not a dialog table or an entry can't be read (the caller should
        /// then not offer to rewrite it).
        /// </summary>
        public static List<DialogTableSlot>? Read(ROM rom, int sequenceAddr, out string? problem)
        {
            problem = null;
            int count = DialogBranches.ReachableSlotCount(rom, sequenceAddr);
            if (count == 0) { problem = "This is not a dialog table."; return null; }

            byte[] d = rom.RawBytes;
            var slots = new List<DialogTableSlot>();
            for (int i = 0; i < count; i++)
            {
                uint value = BitConverter.ToUInt32(d, sequenceAddr + i * 4);
                int entry = (int)(value & 0x00FFFFFF);
                if (value >> 24 != 0x08 || entry + 4 > d.Length || d[entry + 2] > 5)
                {
                    slots.Add(new DialogTableSlot { IsEnd = true, EndValue = value });
                    continue;
                }

                int index = BitConverter.ToUInt16(d, entry);
                byte mode = d[entry + 2];
                byte who = d[entry + 3];
                DialogLine? line = null;
                byte[]? rawEntry = null;
                int jump = 0;

                switch (mode)
                {
                    case 0:
                        {
                            byte[]? script = ScriptAt(d, entry + 3);
                            if (script == null) { problem = $"Step {i}'s script has no END."; return null; }
                            line = DialogLine.Script(script);
                            break;
                        }
                    case 1:
                        line = DialogLine.TextLine(who, CString(d, entry + 4));
                        break;
                    case 2:
                        {
                            string? text = Compressed(rom, entry + 4);
                            if (text == null) { problem = $"Step {i}'s compressed text did not decompress."; return null; }
                            line = DialogLine.TextLine(who, text);
                            break;
                        }
                    case 3: // format string pointer at +4, argument script inline at +8
                    case 4: // argument script pointer at +4, compressed format string inline at +8
                        {
                            if (entry + 8 > d.Length) { problem = $"Step {i} runs past the end of the ROM."; return null; }
                            int pointer = (int)(BitConverter.ToUInt32(d, entry + 4) & 0x00FFFFFF);
                            byte[]? script = ScriptAt(d, mode == 3 ? entry + 8 : pointer);
                            string? format = mode == 3 ? CString(d, pointer) : Compressed(rom, entry + 8);
                            if (script == null || format == null) { problem = $"Step {i} (a line with filled-in values) could not be read."; return null; }
                            line = DialogLine.Interpolated(who, format, script);
                            break;
                        }
                    default: // 5: jump to another table
                        if (entry + 8 > d.Length) { problem = $"Step {i} runs past the end of the ROM."; return null; }
                        rawEntry = d.AsSpan(entry, 8).ToArray();
                        jump = (int)(BitConverter.ToUInt32(d, entry + 4) & 0x00FFFFFF);
                        break;
                }

                slots.Add(new DialogTableSlot { EntryOffset = entry, Mode = mode, Index = index, Line = line, RawEntry = rawEntry, JumpTarget = jump });
            }
            return slots;
        }

        private static byte[]? ScriptAt(byte[] d, int addr)
        {
            if (addr <= 0 || addr >= d.Length) return null;
            byte[] window = d.AsSpan(addr, Math.Min(1024, d.Length - addr)).ToArray();
            // The script's END is the last instruction decoded (an earlier END can be an early return inside an if).
            var code = InstructionDecoder.Decode(window);
            return code.Count == 0 || code[^1].Opcode != 0x11 ? null : window[..(code[^1].Offset + 1)];
        }

        private static string CString(byte[] d, int addr)
        {
            if (addr < 0 || addr >= d.Length) return "";
            int end = addr;
            while (end < d.Length && d[end] != 0 && end - addr < 2048) end++;
            return System.Text.Encoding.ASCII.GetString(d, addr, end - addr).Replace('\x0B', '\n');
        }

        private static string? Compressed(ROM rom, int addr)
        {
            if (addr < 0 || addr >= rom.Length) return null;
            rom.PushPosition(addr);
            try
            {
                var data = JCALG1.DecompressSequential(rom, expectedOutputSize: 4096).Data;
                int nul = Array.IndexOf(data, (byte)0);
                return System.Text.Encoding.ASCII.GetString(data, 0, nul >= 0 ? nul : data.Length).Replace('\x0B', '\n');
            }
            catch
            {
                return null;
            }
            finally
            {
                rom.PopPosition();
            }
        }
    }
}
