using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DrGero.Rendering
{
    /// <summary>
    /// Turns palette indices into a bitmap in one copy. Bitmap.SetPixel is a GDI+ call per pixel: fine for a few hundred, but a sprite
    /// with 220 64x64 frames is 900,000 of them, which is slow on its own and far slower with a debugger attached.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    public static class IndexedImage
    {
        /// <summary>
        /// A width x height 32-bit bitmap from row-major indices. Index 0 is transparent when <paramref name="transparentZero"/> is set;
        /// indices past the palette are transparent too.
        /// </summary>
        public static Bitmap FromIndices(byte[] indices, int width, int height, Color[] palette, bool transparentZero = true, int offset = 0)
        {
            var argb = new int[palette.Length];
            for (int i = 0; i < palette.Length; i++) argb[i] = palette[i].ToArgb();
            var pixels = new int[width * height];
            int count = Math.Min(pixels.Length, Math.Max(0, indices.Length - offset));
            for (int i = 0; i < count; i++)
            {
                byte c = indices[offset + i];
                if ((c == 0 && transparentZero) || c >= argb.Length) continue;
                pixels[i] = argb[c];
            }
            return FromArgb(pixels, width, height);
        }

        /// <summary>A bitmap from 32-bit ARGB values, row-major.</summary>
        public static Bitmap FromArgb(int[] pixels, int width, int height)
        {
            var bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppArgb);
            if (width <= 0 || height <= 0) return bmp;
            var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < height; y++)
                    Marshal.Copy(pixels, y * width, data.Scan0 + y * data.Stride, width);
            }
            finally { bmp.UnlockBits(data); }
            return bmp;
        }
    }
}
