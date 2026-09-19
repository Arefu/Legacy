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
                        var endInstr = instructions.FirstOrDefault(instr => instr.Opcode == 0x11);
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
            {
                var line = lines[i];
                ushort nextIndex = (ushort)(i + 1); // last real entry points at the sentinel slot (index Count)
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

                entryAddresses[i] = entryAddress;
            }

            byte[] sequenceBytes = new byte[(lines.Count + 1) * 4];
            for (int i = 0; i < lines.Count; i++)
                BitConverter.GetBytes(entryAddresses[i]).CopyTo(sequenceBytes, i * 4);
            // Last 4 bytes stay zero -- the sentinel (entry>>>16==0) Dialog_ProcessNext checks for.

            int seqFileOffset = rom.AllocateFreeSpace(sequenceBytes.Length);
            rom.WriteBytesAt(seqFileOffset, sequenceBytes);
            return 0x08000000 | seqFileOffset;
        }
    }
}
