namespace DBZKit.Assets
{
    internal static class MapTest
    {
        internal static void LoadFirstMap(byte[] rom)
        {
            // ── Tile Graphics ────────────────────────────────────────────
            // graphicsRes @ 0x86ADC28 — 4bpp tile graphics, decompresses to 0x2000
            ExtractCompressed(rom, 0x86ADC28, "graphics.bin", 0x2000);

            // tilesetRes @ 0x86A83C8 — secondary tile graphics, decompresses to 0x2000
            ExtractCompressed(rom, 0x86A83C8, "tileset.bin", 0x2000);

            // ── Layer Array (tilemap LUT) ─────────────────────────────────
            // layerArray @ 0x8553B60 — decompresses to 2048
            ExtractCompressed(rom, 0x8553B60, "layer_array.bin", 2048);

            // ── BG2 Chunks (rock/canyon background layer) ─────────────────
            // bgArray[2] @ 0x086AD1F4 — 8 chunks total
            uint[] bg2Chunks = [
                0x86A7124,   // bg2_chunk_0 (new)
                0x86A7174,   // bg2_chunk_1 (new)
                0x86ACBF0,   // bg2_chunk_2 (new)
                0x86A7368,   // bg2_chunk_3 (new)
                0x86ACDB8,   // bg2_chunk_4 (was layer_chunk_0)
                0x86ACF5C,   // bg2_chunk_5 (was layer_chunk_1)
                0x86AD07C,   // bg2_chunk_6 (was layer_chunk_2)
                0x86AD174,   // bg2_chunk_7 (was layer_chunk_3)
            ];

            for (int i = 0; i < bg2Chunks.Length; i++)
                ExtractCompressed(rom, bg2Chunks[i], $"bg2_chunk_{i}.bin", 2048);

            // ── BG3 Chunks (foreground/path layer) ────────────────────────
            // bgArray[3] @ 0x086ADBDC — 7+ chunks
            uint[] bg3Chunks = [
                0x86AD22C,   // bg3_chunk_0 (new)
                0x86AD28C,   // bg3_chunk_1 (new)
                0x86AD2E8,   // bg3_chunk_2 (new)
                0x86AD4CC,   // bg3_chunk_3 (new)
                0x86AD628,   // bg3_chunk_4 (was layer_chunk_4)
                0x86AD850,   // bg3_chunk_5 (was layer_chunk_5)
                0x86AD98C,   // bg3_chunk_6 (was layer_chunk_6)
                0x86ADAFC,   // bg3_chunk_7 (was layer_chunk_7)
            ];

            for (int i = 0; i < bg3Chunks.Length; i++)
                ExtractCompressed(rom, bg3Chunks[i], $"bg3_chunk_{i}.bin", 2048);

            // ── BG0 / BG1 ────────────────────────────────────────────────
            // bgArray[0] @ 0x086ACBB8 — chunk_count=0, no chunks (likely solid/blank layer)
            // bgArray[1] @ 0x086ACBD4 — chunk_count=1, one chunk
            // TODO: locate BG1's chunk pointer once confirmed in IDA

            // ── Palette ──────────────────────────────────────────────────
            // paletteArray = 0x029800C8 is EWRAM (runtime only).
            // Dump palette RAM from mGBA memory viewer at 0x05000000, 0x200 bytes.
            // Save as palette.bin manually for now.
        }

        private static void ExtractCompressed(byte[] rom, uint gbaAddr, string filename, int decompSize)
        {
            int offset = GBA.ToOffset(gbaAddr) + 8; // skip 8-byte header
            var result = Jcalg1Decompress.Decompress(rom, offset, decompSize);
            File.WriteAllBytes(filename, result.Data);
            Console.WriteLine($"Extracted {filename} from 0x{gbaAddr:X} ({result.Data.Length} bytes)");
        }
    }

}

