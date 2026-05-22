using System.Drawing.Imaging;

namespace DBZKit.Assets
{
    internal static class Items
    {
        /// <summary>
        /// Loads all item sprites from g_ItemsInGame into the provided
        /// <see cref="ImageList"/> and <see cref="ListView"/>.
        /// Skips null sprite pointers. Each item can have up to 4 sprites.
        /// </summary>
        internal static void Load(byte[] rom, ImageList imageList, ListView listView, Dictionary<int, byte[]> rawData, Color[]? palette = null)
        {
            int slot = 0;

            for (int i = 0; i < 50; i++)
            {
                uint entryBase = 0x086ADE24 + (uint)(i * 0x38);

                int width = GBA.ReadInt32(rom, entryBase + 0x08);
                int height = GBA.ReadInt32(rom, entryBase + 0x0C);

                int[] spriteOffsets = [0x14, 0x24, 0x28, 0x2C];

                foreach (int spriteOff in spriteOffsets)
                {
                    uint spritePtr = GBA.ReadUInt32(rom, entryBase + (uint)spriteOff);
                    if (spritePtr == 0)
                        continue;

                    int isCompressed = GBA.ReadInt32(rom, spritePtr);
                    int deflatedSize = GBA.ReadInt32(rom, spritePtr + 4);
                    int dataOffset = GBA.ToOffset(spritePtr) + 8;

                    var result = Jcalg1Decompress.Decompress(rom, dataOffset, deflatedSize);

                    int spriteSize = (int)Math.Sqrt(result.Data.Length);
                    var raw = GBA.Render8bpp(result.Data, spriteSize, spriteSize, palette);
                    var scaled = new Bitmap(raw, new Size(32, 32));

                    string key = $"item_{i}_sprite_{spriteOff:X2}";
                    rawData[slot] = result.Data;
                    imageList.Images.Add(key, scaled);
                    listView.Items.Add(new ListViewItem(key, key));
                    slot++;
                }
            }
        }

        /// <summary>Exports an item sprite bitmap to a PNG file.</summary>
        internal static void ExportPng(Bitmap bitmap, string path) => bitmap.Save(path, ImageFormat.Png);

        /// <summary>Exports raw decompressed item bytes to a BIN file.</summary>
        internal static void ExportBin(byte[] rawData, string path) => File.WriteAllBytes(path, rawData);

        /// <summary>Imports raw bytes from a BIN file.</summary>
        internal static byte[] ImportBin(string path) => File.ReadAllBytes(path);

        /// <summary>Imports an indexed PNG and returns the raw pixel bytes.</summary>
        internal static byte[] ImportPng(string path)
        {
            using var bmp = new Bitmap(path);
            var data = new byte[bmp.Width * bmp.Height];
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    data[y * bmp.Width + x] = bmp.GetPixel(x, y).R;
            return data;
        }
    }
}