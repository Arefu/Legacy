using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Text;

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
