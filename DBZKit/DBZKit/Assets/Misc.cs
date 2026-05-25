using System.Drawing.Imaging;

namespace DBZKit.Assets
{
    internal static class Unknowns
    {
        private static readonly uint[] CompressedBlocks =
        [
            0x083ED3E4,
            0x083ED518,
            0x083EC86C,
            0x083ED2B0,
        ];

        // All ability sprites: (address, label)
        private static readonly (uint Address, string Label)[] AbilitySprites =
        [
            (0x086D8C7C, "BigBang_ready"),
            (0x086D8E7C, "BigBang_cooling"),
            (0x086D907C, "Burning_ready"),
            (0x086D927C, "Burning_cooling"),
            (0x086D7754, "KiBlast_ready"),
            (0x086D7954, "KiBlast_cooling"),
            (0x086D947C, "Hercules_ready"),
            (0x086D967C, "Hercules_cooling"),
            (0x086D987C, "Masenko_ready"),
            (0x086D9A7C, "Masenko_cooling"),
            (0x086D9C7C, "Disabled_ready"),
            (0x086D9E7C, "Ability7_ready"),
            (0x086DA07C, "Ability7_cooling"),
            (0x086DA27C, "Ability8_ready"),
            (0x086DA47C, "Ability8_cooling"),
            (0x086DA67C, "SpecialBeamCannon_ready"),
            (0x086DA87C, "SpecialBeamCannon_cooling"),
            (0x086DAA7C, "SpiritBomb_ready"),
            (0x086DAC7C, "SpiritBomb_cooling"),
            (0x086DAE7C, "Transformation_ready"),
            (0x086DB07C, "Transformation_cooling"),
            (0x086D887C, "SwordBlast_ready"),
            (0x086D8A7C, "SwordBlast_cooling"),
            (0x08705AA4, "Ability6_ready"),
            (0x08705CA4, "Ability6_cooling"),
        ];

        internal static void Load(
            byte[] rom,
            ImageList imageList,
            ListView listView,
            Dictionary<int, byte[]> rawData,
            Color[]? palette = null,
            string? dumpDirectory = null)
        {
            dumpDirectory ??= AppContext.BaseDirectory;

            for (int i = 0; i < CompressedBlocks.Length; i++)
            {
                uint spritePtr = CompressedBlocks[i];

                int deflatedSize = GBA.ReadInt32(rom, spritePtr + 4);
                int dataOffset = GBA.ToOffset(spritePtr) + 8;

                var result = Jcalg1Decompress.Decompress(rom, dataOffset, deflatedSize);

                string dumpPath = Path.Combine(dumpDirectory, $"unknown_{i}.bin");
                File.WriteAllBytes(dumpPath, result.Data);
                var raw = GBA.Render8bpp(result.Data, 160, 64, palette);
                var scaled = new Bitmap(raw, new Size(160, 64));

                string key = $"unknown_{i}";
                rawData[i] = result.Data;
                imageList.Images.Add(key, scaled);
                listView.Items.Add(new ListViewItem(key, key));
            }

            for (int i = 0; i < AbilitySprites.Length; i++)
            {
                var (address, label) = AbilitySprites[i];
                int offset = GBA.ToOffset(address);
                const int byteCount = 512; // full IDA-visible block size

                if (offset + byteCount > rom.Length)
                    continue;

                byte[] data = new byte[byteCount];
                Array.Copy(rom, offset, data, 0, byteCount);

                string dumpPath = Path.Combine(dumpDirectory, $"{label}.bin");
                File.WriteAllBytes(dumpPath, data);

                rawData[CompressedBlocks.Length + i] = data;

                // 512 bytes / 32 bytes per 8x8 4bpp tile = 16 tiles
                // tiled variants
                foreach (var (tw, th) in new[] { (8, 1), (4, 2), (2, 4), (1, 8) })
                {
                    string variantKey = $"{label}_{tw * 8}x{th * 8}_tiled";
                    var variantRaw = GBA.Render4bppTiled(data, tw, th, palette);
                    var variantScaled = new Bitmap(variantRaw, new Size(tw * 8, th * 8));
                    imageList.Images.Add(variantKey, variantScaled);
                    listView.Items.Add(new ListViewItem(variantKey, variantKey));
                }

                // flat variants — all divisors of 1024 pixels
                byte[] expanded = Expand4bppTo8bpp(data);
                int totalPixels = expanded.Length; // 1024
                for (int w = 1; w <= totalPixels; w++)
                {
                    if (totalPixels % w != 0)
                        continue;

                    int h = totalPixels / w;
                    string variantKey = $"{label}_{w}x{h}_flat";
                    var variantRaw = GBA.Render8bpp(expanded, w, h, palette);
                    var variantScaled = new Bitmap(variantRaw, new Size(w, h));
                    imageList.Images.Add(variantKey, variantScaled);
                    listView.Items.Add(new ListViewItem(variantKey, variantKey));
                }
            }
        }

        private static byte[] Expand4bppTo8bpp(byte[] data)
        {
            byte[] expanded = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                expanded[i * 2] = (byte)(data[i] & 0x0F);
                expanded[i * 2 + 1] = (byte)((data[i] >> 4) & 0x0F);
            }
            return expanded;
        }

        internal static void ExportPng(Bitmap bitmap, string path) =>
            bitmap.Save(path, ImageFormat.Png);

        internal static void ExportBin(byte[] rawData, string path) =>
            File.WriteAllBytes(path, rawData);
    }
}