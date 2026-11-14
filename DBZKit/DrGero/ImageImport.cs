using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using DrGero.IO;

namespace DrGero.Rendering
{
    /// <summary>
    /// Turning ordinary images into the game's 8bpp data: palette matching, building a palette from a set of images, tile order,
    /// and writing a stored (mode 0) resource block. Shared by every importer (portraits, attract-mode video, ...).
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    public static class ImageImport
    {
        /// <summary>GBA BGR555 for a colour (what the hardware can show).</summary>
        public static ushort ToBgr555(Color c) => (ushort)((c.R >> 3) | ((c.G >> 3) << 5) | ((c.B >> 3) << 10));

        public static Color FromBgr555(int c) => Color.FromArgb(0xFF, ((c & 0x1F) * 0x21) >> 2, (((c >> 5) & 0x1F) * 0x21) >> 2, (((c >> 10) & 0x1F) * 0x21) >> 2);

        /// <summary>
        /// Pixel indices for an image. An 8bpp indexed PNG keeps its indices as they are; anything else is matched to the nearest
        /// colour of <paramref name="palette"/>. With <paramref name="transparentZero"/>, transparent pixels become index 0 and index 0
        /// is never picked for a visible colour (the OBJ/BG convention).
        /// </summary>
        public static byte[] ToIndices(Bitmap bmp, Color[] palette, bool transparentZero)
        {
            int w = bmp.Width, h = bmp.Height;
            var indices = new byte[w * h];
            if (bmp.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
                try { for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, indices, y * w, w); }
                finally { bmp.UnlockBits(data); }
                return indices;
            }

            var argb = Pixels(bmp);
            var cache = new Dictionary<int, byte>();
            int first = transparentZero ? 1 : 0;
            for (int i = 0; i < argb.Length; i++)
            {
                int c = argb[i];
                if (transparentZero && (c >>> 24) < 128) { indices[i] = 0; continue; }
                int key = ToBgr555(Color.FromArgb(c));
                if (!cache.TryGetValue(key, out byte best))
                {
                    var target = FromBgr555(key);
                    int bestDistance = int.MaxValue;
                    for (int p = first; p < palette.Length; p++)
                    {
                        int dr = palette[p].R - target.R, dg = palette[p].G - target.G, db = palette[p].B - target.B;
                        int d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
                        if (d < bestDistance) { bestDistance = d; best = (byte)p; if (d == 0) break; }
                    }
                    cache[key] = best;
                }
                indices[i] = best;
            }
            return indices;
        }

        /// <summary>
        /// A palette of at most <paramref name="size"/> colours for a set of images, at GBA precision (BGR555). If they use few enough
        /// colours they are kept exactly; otherwise median cut, weighted by how often each colour appears.
        /// </summary>
        public static Color[] BuildPalette(IEnumerable<Bitmap> images, int size = 256)
        {
            var counts = new Dictionary<int, long>();
            foreach (var bmp in images)
                foreach (int c in Pixels(bmp))
                {
                    int key = ToBgr555(Color.FromArgb(c));
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }

            List<Color> result;
            if (counts.Count <= size) result = [.. counts.OrderByDescending(kv => kv.Value).Select(kv => FromBgr555(kv.Key))];
            else
            {
                var boxes = new List<List<(int R, int G, int B, long N)>>
                {
                    counts.Select(kv => (kv.Key & 0x1F, (kv.Key >> 5) & 0x1F, (kv.Key >> 10) & 0x1F, kv.Value)).ToList(),
                };
                while (boxes.Count < size)
                {
                    // Split the box with the widest channel range (ties: the most pixels) at its weighted median.
                    var box = boxes.Where(b => b.Count > 1).OrderByDescending(Range).ThenByDescending(b => b.Sum(x => x.N)).FirstOrDefault();
                    if (box == null) break;
                    int axis = Axis(box);
                    var sorted = box.OrderBy(x => axis == 0 ? x.R : axis == 1 ? x.G : x.B).ToList();
                    long half = sorted.Sum(x => x.N) / 2, run = 0;
                    int cut = 1;
                    for (int i = 0; i < sorted.Count - 1; i++) { run += sorted[i].N; if (run >= half) { cut = i + 1; break; } }
                    boxes.Remove(box);
                    boxes.Add(sorted[..cut]);
                    boxes.Add(sorted[cut..]);
                }
                result = [.. boxes.Select(b =>
                {
                    long n = b.Sum(x => x.N);
                    return FromBgr555((int)(b.Sum(x => x.R * x.N) / n) | (int)(b.Sum(x => x.G * x.N) / n) << 5 | (int)(b.Sum(x => x.B * x.N) / n) << 10);
                })];
            }
            while (result.Count < size) result.Add(Color.Black);
            return [.. result];

            static int Range(List<(int R, int G, int B, long N)> b) => Math.Max(b.Max(x => x.R) - b.Min(x => x.R), Math.Max(b.Max(x => x.G) - b.Min(x => x.G), b.Max(x => x.B) - b.Min(x => x.B)));
            static int Axis(List<(int R, int G, int B, long N)> b)
            {
                int r = b.Max(x => x.R) - b.Min(x => x.R), g = b.Max(x => x.G) - b.Min(x => x.G), bl = b.Max(x => x.B) - b.Min(x => x.B);
                return r >= g && r >= bl ? 0 : g >= bl ? 1 : 2;
            }
        }

        /// <summary>Row-major pixels to 8x8 tiles (row-major tile order, 64 bytes per tile): what BG/OBJ tile VRAM holds.</summary>
        public static byte[] ToTiles(byte[] linear, int width, int height)
        {
            var tiles = new byte[width * height];
            int tilesWide = width / 8, n = 0;
            for (int ty = 0; ty < height / 8; ty++)
                for (int tx = 0; tx < tilesWide; tx++)
                    for (int py = 0; py < 8; py++)
                        for (int px = 0; px < 8; px++)
                            tiles[n++] = linear[(ty * 8 + py) * width + tx * 8 + px];
            return tiles;
        }

        /// <summary>
        /// Writes a stored resource {u32 0, u32 size, data} in free space (Resource_LoadOrDecompress copies mode 0 as-is) and returns its
        /// file offset. Nothing needs compressing, at the cost of space.
        /// </summary>
        public static int WriteStored(ROM rom, byte[] data) => WriteResource(rom, 0, data.Length, data);

        /// <summary>Writes {u32 mode, u32 size, payload} 4-byte aligned in free space; returns its file offset.</summary>
        public static int WriteResource(ROM rom, int mode, int unpackedSize, byte[] payload)
        {
            var block = new byte[8 + payload.Length];
            BitConverter.GetBytes(mode).CopyTo(block, 0);
            BitConverter.GetBytes(unpackedSize).CopyTo(block, 4);
            payload.CopyTo(block, 8);
            int at = rom.AllocateFreeSpace(block.Length);
            rom.WriteBytesAt(at, block);
            return at;
        }

        private static int[] Pixels(Bitmap bmp)
        {
            using var argb = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(argb)) g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
            var data = argb.LockBits(new Rectangle(0, 0, argb.Width, argb.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new int[argb.Width * argb.Height];
            try { for (int y = 0; y < argb.Height; y++) System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * argb.Width, argb.Width); }
            finally { argb.UnlockBits(data); }
            return pixels;
        }
    }
}
