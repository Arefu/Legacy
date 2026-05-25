using System.Drawing.Imaging;

namespace DBZKit
{
    internal static class GBA
    {
        private const uint RomBase = 0x08000000;

        /// <summary>Converts a GBA ROM pointer (0x08xxxxxx) to a file offset.</summary>
        public static int ToOffset(uint gbaPointer) => (int)(gbaPointer - RomBase);

        /// <summary>Converts a file offset back to a GBA ROM pointer.</summary>
        public static uint ToPointer(int offset) => (uint)(offset + RomBase);

        /// <summary>Reads a little-endian int32 from a GBA pointer.</summary>
        public static int ReadInt32(byte[] rom, uint gbaPointer) => BitConverter.ToInt32(rom, ToOffset(gbaPointer));

        /// <summary>Reads a little-endian uint32 from a GBA pointer.</summary>
        public static uint ReadUInt32(byte[] rom, uint gbaPointer) => BitConverter.ToUInt32(rom, ToOffset(gbaPointer));

        /// <summary>
        /// Reads a GBA BGR555 palette from the ROM and returns it as an array of 256 Colors.
        /// </summary>
        public static Color[] ReadPalette(byte[] rom, uint gbaPointer, int count = 256)
        {
            var palette = new Color[count];
            int offset = ToOffset(gbaPointer);

            for (int i = 0; i < count; i++)
            {
                ushort raw = BitConverter.ToUInt16(rom, offset + i * 2);
                int r = (raw & 0x1F) << 3;
                int g = ((raw >> 5) & 0x1F) << 3;
                int b = ((raw >> 10) & 0x1F) << 3;
                palette[i] = Color.FromArgb(r, g, b);
            }

            return palette;
        }

        /// <summary>
        /// Renders 4bpp GBA tiled sprite data. Tiles are 8x8, stored row by row within each tile.
        /// </summary>
        public static Bitmap Render4bppTiled(byte[] data, int widthInTiles, int heightInTiles, Color[]? palette = null)
        {
            int width = widthInTiles * 8;
            int height = heightInTiles * 8;
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            int tileIndex = 0;
            for (int tileY = 0; tileY < heightInTiles; tileY++)
            {
                for (int tileX = 0; tileX < widthInTiles; tileX++)
                {
                    int tileOffset = tileIndex * 32; // 32 bytes per 8x8 4bpp tile
                    for (int row = 0; row < 8; row++)
                    {
                        for (int col = 0; col < 8; col += 2)
                        {
                            byte b = data[tileOffset + row * 4 + col / 2];
                            byte lo = (byte)(b & 0x0F);
                            byte hi = (byte)((b >> 4) & 0x0F);

                            int px = tileX * 8 + col;
                            int py = tileY * 8 + row;

                            Color colorLo = palette != null ? palette[lo] : Color.FromArgb(lo * 17, lo * 17, lo * 17);
                            Color colorHi = palette != null ? palette[hi] : Color.FromArgb(hi * 17, hi * 17, hi * 17);

                            bmp.SetPixel(px, py, colorLo);
                            bmp.SetPixel(px + 1, py, colorHi);
                        }
                    }
                    tileIndex++;
                }
            }

            return bmp;
        }

        /// <summary>
        /// Renders 8bpp indexed pixel data using a palette. Falls back to greyscale if palette is null.
        /// </summary>
        public static Bitmap Render8bpp(byte[] data, int width, int height, Color[]? palette = null)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte index = data[y * width + x];
                    Color color = palette != null
                        ? palette[index]
                        : Color.FromArgb(index, index, index);
                    bmp.SetPixel(x, y, color);
                }
            }

            return bmp;
        }
    }
}
