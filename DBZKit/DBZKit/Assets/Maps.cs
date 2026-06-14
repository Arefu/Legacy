using System.Drawing.Imaging;

namespace DBZKit.Assets
{
    internal static class Variations
    {
        internal static byte[]? LoadAsset(byte[] rom, uint assetHeaderAddress)
        {
            if (assetHeaderAddress == 0)
                return null;

            int isCompressed = GBA.ReadInt32(rom, assetHeaderAddress);
            int deflatedSize = GBA.ReadInt32(rom, assetHeaderAddress + 4);

            if (isCompressed != 1)
                return null;

            int dataOffset = GBA.ToOffset(assetHeaderAddress) + 8;

            var result = Jcalg1Decompress.Decompress(rom, dataOffset, deflatedSize);

            return result.Data;
        }

        internal static byte[]? LoadVariation1(byte[] rom, uint variationArrayAddress)
        {
            uint variation1Pointer = GBA.ReadUInt32(rom, variationArrayAddress);

            if (variation1Pointer == 0)
                return null;

            return LoadAsset(rom, variation1Pointer);
        }

        internal static Bitmap RenderTilemap(byte[] data, int widthInTiles, int heightInTiles)
        {
            int width = widthInTiles * 8;
            int height = heightInTiles * 8;
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            int index = 0;
            for (int tileY = 0; tileY < heightInTiles; tileY++)
            {
                for (int tileX = 0; tileX < widthInTiles; tileX++)
                {
                    if (index >= data.Length)
                        break;

                    byte tileIndex = data[index];

                    int hue = (tileIndex * 360) / 256;
                    Color color = HsvToRgb(hue, 0.6, tileIndex == 0 ? 0.15 : 0.9);

                    for (int py = 0; py < 8; py++)
                        for (int px = 0; px < 8; px++)
                            bmp.SetPixel(tileX * 8 + px, tileY * 8 + py, color);

                    index++;
                }
            }

            return bmp;
        }

        private static Color HsvToRgb(double h, double s, double v)
        {
            int hi = (int)(h / 60) % 6;
            double f = h / 60 - Math.Floor(h / 60);

            v *= 255;
            int vInt = (int)v;
            int p = (int)(v * (1 - s));
            int q = (int)(v * (1 - f * s));
            int t = (int)(v * (1 - (1 - f) * s));

            return hi switch
            {
                0 => Color.FromArgb(vInt, t, p),
                1 => Color.FromArgb(q, vInt, p),
                2 => Color.FromArgb(p, vInt, t),
                3 => Color.FromArgb(p, q, vInt),
                4 => Color.FromArgb(t, p, vInt),
                _ => Color.FromArgb(vInt, p, q),
            };
        }

        internal static (int widthInTiles, int heightInTiles) GuessTileGridShape(int totalTiles)
        {
            int best = 1;
            for (int w = 1; w <= totalTiles; w++)
            {
                if (totalTiles % w == 0)
                {
                    int h = totalTiles / w;
                    if (Math.Abs(w - h) < Math.Abs(best - (totalTiles / best)))
                        best = w;
                }
            }
            return (best, totalTiles / best);
        }

        internal static void DumpCandidates(byte[] data, string outputDirectory, Color[]? palette = null)
        {
            Directory.CreateDirectory(outputDirectory);

            if (data.Length % 64 == 0)
            {
                int totalTiles8 = data.Length / 64;
                foreach (var (w, h) in TileGridShapes(totalTiles8))
                {
                    var bmp = GBA.Render8bppTiled(data, w, h, palette);
                    bmp.Save(Path.Combine(outputDirectory, $"8bpp_tiled_{w}x{h}.png"), ImageFormat.Png);
                }
            }

            if (data.Length % 32 == 0)
            {
                int totalTiles4 = data.Length / 32;
                foreach (var (w, h) in TileGridShapes(totalTiles4))
                {
                    var bmp = GBA.Render4bppTiled(data, w, h, palette);
                    bmp.Save(Path.Combine(outputDirectory, $"4bpp_tiled_{w}x{h}.png"), ImageFormat.Png);
                }
            }

            int[] linearWidths = { 8, 16, 32, 64, 128, 256 };
            foreach (int width in linearWidths)
            {
                if (data.Length % width != 0)
                    continue;

                int height = data.Length / width;
                var bmp = GBA.Render8bpp(data, width, height, palette);
                bmp.Save(Path.Combine(outputDirectory, $"linear_{width}x{height}.png"), ImageFormat.Png);
            }
        }

        private static IEnumerable<(int w, int h)> TileGridShapes(int totalTiles)
        {
            var shapes = new List<(int w, int h)>();

            for (int w = 1; w <= totalTiles; w++)
            {
                if (totalTiles % w == 0)
                {
                    int h = totalTiles / w;
                    shapes.Add((w, h));
                }
            }

            return shapes;
        }

        internal static void Load(byte[] rom, uint assetHeaderAddress,
            PictureBox pictureBox, out byte[]? rawData, Color[]? palette = null)
        {
            rawData = LoadAsset(rom, assetHeaderAddress);

            if (rawData == null)
            {
                pictureBox.Image = null;
                return;
            }

            if (rawData.Length % 64 == 0)
            {
                int totalTiles = rawData.Length / 64;
                var (widthInTiles, heightInTiles) = GuessTileGridShape(totalTiles);

                pictureBox.Image = GBA.Render8bppTiled(rawData, widthInTiles, heightInTiles, palette);

                System.Diagnostics.Debug.WriteLine(
                    $"Variations.Load: 0x{assetHeaderAddress:X8} -> {rawData.Length} bytes, rendered as 8bpp tiled {widthInTiles}x{heightInTiles} tiles ({widthInTiles * 8}x{heightInTiles * 8} px)");
            }
            else
            {
                pictureBox.Image = null;
                System.Diagnostics.Debug.WriteLine(
                    $"Variations.Load: 0x{assetHeaderAddress:X8} -> {rawData.Length} bytes, not divisible by 64, cannot render as 8bpp tiles");
            }
        }

        internal static void ExportBin(byte[] rawData, string path)
        {
            File.WriteAllBytes(path, rawData);
        }
    }
}