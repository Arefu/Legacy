using DrGero.IO;

namespace DrGero.Rendering
{
    /// <summary>
    /// Copying a decoration's look between layouts.
    ///
    /// A decoration record (graphicObjects, 24 bytes) is a box plus OAM attributes: +0x10 shape/size, +0x14 a tile
    /// index into its LAYOUT's sprite sheet (Map_VariationEntry +0x2C, 8bpp, in 32-byte OAM units: 2 per 64-byte
    /// tile), +0x16 depth, +0x17 a flag. GraphicObjectController_Create uploads that whole sheet to OBJ VRAM
    /// (ObjVram_Alloc(sheet size)) and every decoration on the layout draws from it. The OBJ palette is one for
    /// the whole game (ItemIconReader.ReadOBJPalette), so a frame from another map only needs its tiles.
    ///
    /// Importing appends the frame's tiles to this layout's sheet (or reuses them if the sheet already has them),
    /// writes the sheet back as a stored resource (format 0, which Resource_LoadOrDecompress copies as-is) and
    /// repoints +0x2C for this layout only: other layouts that shared the old sheet keep it.
    /// ObjVram_Alloc returns -1 when sprite VRAM is full, so a sheet is capped at <see cref="MaxSheetBytes"/>, the
    /// largest a regular field map ships with (Z8A48, which also has enemies on screen).
    /// </summary>
    public static class Decorations
    {
        public const int MaxSheetBytes = 10240;

        /// <summary>
        /// Record +0x17: shown when the map loads (1) or hidden (0). GraphicObject_Create keeps it as a bool the draw
        /// check (0x80071FA) tests first; message 4 (0x8007298) sets it from a script: arg = index*2 + visible.
        /// Opcode 30 sends index*2, i.e. hides decoration n (how a broken gate rock goes away). Some shipped
        /// decorations start hidden and are shown by their map's scripts (event objects).
        /// </summary>
        public const int VisibleFlagOffset = 0x17;

        public static bool IsVisible(ROM rom, int record) => rom.ReadBytesAt(record + VisibleFlagOffset, 1)[0] != 0;
        public static void SetVisible(ROM rom, int record, bool visible) => rom.PatchByte(record + VisibleFlagOffset, (byte)(visible ? 1 : 0));
        private const int SheetFieldOffset = 0x2C;
        private const int TileBytes = 64;

        private static int SheetPointer(ROM rom, int layout) => BitConverter.ToInt32(rom.ReadBytesAt(layout + SheetFieldOffset, 4)) & 0x01FFFFFF;

        private static byte[] ReadSheet(ROM rom, int layout)
        {
            int p = SheetPointer(rom, layout);
            return p == 0 ? Array.Empty<byte>() : JCALG1.Decompress(rom, p);
        }

        /// <summary>
        /// The 24 bytes a new decoration on <paramref name="targetLayout"/> should start from to look like
        /// <paramref name="templateRecord"/> (a graphicObjects record on <paramref name="templateLayout"/>; 0 = the
        /// target itself). Imports the frame's tiles into the target's sheet when the two layouts use different
        /// sheets. The box is the template's; callers move it.
        /// </summary>
        public static byte[] TemplateFor(ROM rom, int targetLayout, int templateRecord, int templateLayout)
        {
            byte[] record = rom.ReadBytesAt(templateRecord, EntityReader.GraphicObjectRecordSize);
            if (templateLayout == 0 || templateLayout == targetLayout || SheetPointer(rom, templateLayout) == SheetPointer(rom, targetLayout))
                return record;

            int w = BitConverter.ToInt32(record, 8) - BitConverter.ToInt32(record, 0);
            int h = BitConverter.ToInt32(record, 12) - BitConverter.ToInt32(record, 4);
            int tiles = Math.Max(1, w / 8) * Math.Max(1, h / 8);
            int firstTile = BitConverter.ToUInt16(record, 0x14) / 2;

            byte[] source = ReadSheet(rom, templateLayout);
            if ((firstTile + tiles) * TileBytes > source.Length)
                throw new InvalidOperationException("That decoration's frame runs past its own map's sprite sheet, so it can't be copied.");
            byte[] frame = source.AsSpan(firstTile * TileBytes, tiles * TileBytes).ToArray();

            byte[] sheet = ReadSheet(rom, targetLayout);
            int at = FindFrame(sheet, frame);
            if (at < 0)
            {
                if (sheet.Length + frame.Length > MaxSheetBytes)
                    throw new InvalidOperationException(
                        $"This layout's decoration sheet would grow to {sheet.Length + frame.Length} bytes. Decorations share sprite memory with " +
                        $"characters and enemies, and no regular map in the game goes past {MaxSheetBytes}, so it was refused. " +
                        "Use decorations already on this map, or fewer kinds from other maps.");
                at = sheet.Length / TileBytes;
                var grown = new byte[sheet.Length + frame.Length];
                sheet.CopyTo(grown, 0);
                frame.CopyTo(grown, sheet.Length);

                var blob = new byte[8 + grown.Length];
                BitConverter.GetBytes(0).CopyTo(blob, 0);            // format 0 = stored
                BitConverter.GetBytes(grown.Length).CopyTo(blob, 4); // size (ObjVram_Alloc reads this too)
                grown.CopyTo(blob, 8);
                int sheetAt = rom.AllocateFreeSpace(blob.Length);
                rom.WriteBytesAt(sheetAt, blob);
                rom.PatchInt32(targetLayout + SheetFieldOffset, 0x08000000 | sheetAt);
            }

            BitConverter.GetBytes((ushort)(at * 2)).CopyTo(record, 0x14);
            return record;
        }

        // Tile index where the sheet already holds exactly these tiles, or -1.
        private static int FindFrame(byte[] sheet, byte[] frame)
        {
            for (int t = 0; (t * TileBytes) + frame.Length <= sheet.Length; t++)
                if (sheet.AsSpan(t * TileBytes, frame.Length).SequenceEqual(frame)) return t;
            return -1;
        }
    }
}
