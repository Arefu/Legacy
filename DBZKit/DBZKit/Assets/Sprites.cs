namespace DBZKit.Assets
{
    internal static class Sprites
    {
        internal static Bitmap AssembleSprite(byte[] tileData, int tilesWide, int tilesHigh, Color[]? palette = null)
        {
            int tileSize = 64; // 8x8 pixels, 8bpp = 64 bytes per tile
            int spriteW = tilesWide * 8;
            int spriteH = tilesHigh * 8;
            var bmp = new Bitmap(spriteW, spriteH);

            for (int ty = 0; ty < tilesHigh; ty++)
            {
                for (int tx = 0; tx < tilesWide; tx++)
                {
                    int tileIdx = ty * tilesWide + tx;
                    int tileOffset = tileIdx * tileSize;

                    if (tileOffset + tileSize > tileData.Length)
                        break;

                    for (int py = 0; py < 8; py++)
                    {
                        for (int px = 0; px < 8; px++)
                        {
                            byte colorIdx = tileData[tileOffset + py * 8 + px];
                            Color c = palette != null && colorIdx < palette.Length
                                ? palette[colorIdx]
                                : Color.FromArgb(colorIdx, colorIdx, colorIdx);

                            bmp.SetPixel(tx * 8 + px, ty * 8 + py, c);
                        }
                    }
                }
            }

            return bmp;
        }
        /// <summary>
        /// Loads a single character sprite frame from a known frame data pointer.
        /// The format is: [u32 isCompressed][u32 decompressedSize][compressed data...]
        /// </summary>
        internal static void Load(byte[] rom, ImageList imageList, ListView listView, Color[]? palette = null)
        {
            var spriteSets = new (string name, uint[] framePtrs)[]
            {
        ("NPC10", [0x083A8D9C, 0x083A8EAC, 0x083A8FC0, 0x083A90C4]),
        ("NPC11", [0x083A91C4, 0x083A92B0, 0x083A93A0, 0x083A947C]),
        ("NPC12", [0x083A9550, 0x083A9634, 0x083A9718, 0x083A97FC]),
        ("NPC12_Flip", [0x083A9550, 0x083A9634, 0x083A9718, 0x083A97FC]), // same data, H-flip via attr
            };

            int bytesPerFrame = 2 * 4 * 64;

            foreach (var (name, framePtrs) in spriteSets)
            {
                for (int state = 0; state < framePtrs.Length; state++)
                {
                    uint framePtr = framePtrs[state];
                    int decompSize = GBA.ReadInt32(rom, framePtr + 4);
                    int dataOffset = GBA.ToOffset(framePtr) + 8;

                    var result = Jcalg1Decompress.Decompress(rom, dataOffset, decompSize);
                    int frameCount = result.Data.Length / bytesPerFrame;

                    Console.WriteLine($"{name} state{state}: {result.Data.Length} bytes = {frameCount} frames");

                    for (int f = 0; f < frameCount; f++)
                    {
                        var frameData = result.Data.Skip(f * bytesPerFrame).Take(bytesPerFrame).ToArray();
                        var bmp = AssembleSprite(frameData, 2, 4, palette);
                        var scaled = new Bitmap(bmp, new Size(64, 128));

                        string key = $"{name}_s{state}_f{f}";
                        imageList.Images.Add(key, scaled);
                        listView.Items.Add(new ListViewItem($"{name} State{state} Frame{f}", key));
                    }
                }

            }
        }

        internal static void Load2(byte[] rom, ImageList imageList, ListView listView, Color[]? palette = null)
        {
            uint[] addresses = new uint[]
            {

                0x83B1F20
            };

            int bytesPerFrame = 2 * 4 * 64;

            for (int i = 0; i < addresses.Length; i++)
            {
                uint addr = addresses[i];
                string name = $"Sprite_{addr:X8}";

                try
                {
                    int decompSize = GBA.ReadInt32(rom, addr + 4);
                    int dataOffset = GBA.ToOffset(addr) + 8;

                    var result = Jcalg1Decompress.Decompress(rom, dataOffset, decompSize);
                    int frameCount = result.Data.Length / bytesPerFrame;

                    Console.WriteLine($"{name}: {result.Data.Length} bytes = {frameCount} frames");

                    for (int f = 0; f < frameCount; f++)
                    {
                        var frameData = result.Data.Skip(f * bytesPerFrame).Take(bytesPerFrame).ToArray();
                        var bmp = AssembleSprite(frameData, 2, 4, palette);  // swap: 4 wide, 2 tall
                        var scaled = new Bitmap(bmp, new Size(64, 128));

                        string key = $"{name}_f{f}";
                        imageList.Images.Add(key, scaled);
                        listView.Items.Add(new ListViewItem($"{name} Frame{f}", key));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skipping {name}: {ex.Message}");
                }
            }
        }
    }
}


