using System;
using System.Collections.Generic;
using System.Drawing.Imaging;

namespace DBZKit.Assets
{
    internal static class Portrait
    {
        /// <summary>
        /// Loads all portraits from the ROM's portrait table into the provided
        /// <see cref="ImageList"/> and <see cref="ListView"/>.
        /// </summary>
        /// <param name="rom">The raw GBA ROM bytes.</param>
        /// <param name="imageList">The ImageList to populate with rendered portrait bitmaps.</param>
        /// <param name="listView">The ListView to populate with portrait entries.</param>
        /// <param name="rawData">Dictionary to populate with raw decompressed bytes, keyed by portrait index.</param>
        internal static void Load(byte[] rom, ImageList imageList, ListView listView, Dictionary<int, byte[]> rawData, Color[]? palette = null)
        {
            const uint portraitTable = 0x083EC9D4;
            const int totalEntries = 115;

            for (int i = 0; i < totalEntries; i++)
            {
                uint pointer = GBA.ReadUInt32(rom, portraitTable + (uint)(i * 4));

                if (pointer == 0)
                    continue;

                int isCompressed = GBA.ReadInt32(rom, pointer);
                int deflatedSize = GBA.ReadInt32(rom, pointer + 4);
                int dataOffset = GBA.ToOffset(pointer) + 8;

                var result = Jcalg1Decompress.Decompress(rom, dataOffset, deflatedSize);

                string key = $"portrait_{i}";
                rawData[i] = result.Data;
                imageList.Images.Add(key, GBA.Render8bpp(result.Data, 64, 64, palette));
                listView.Items.Add(new ListViewItem(key, key));
            }
        }

        /// <summary>Exports a portrait bitmap to a PNG file.</summary>
        internal static void ExportPng(Bitmap bitmap, string path)
        {
            bitmap.Save(path, ImageFormat.Png);
        }

        /// <summary>Exports raw decompressed portrait bytes to a BIN file.</summary>
        internal static void ExportBin(byte[] rawData, string path)
        {
            File.WriteAllBytes(path, rawData);
        }

        /// <summary>Imports raw bytes from a BIN file.</summary>
        internal static byte[] ImportBin(string path)
        {
            return File.ReadAllBytes(path);
        }

        /// <summary>Imports an indexed PNG and returns the raw pixel bytes.</summary>
        internal static byte[] ImportPng(string path)
        {
            using var bmp = new Bitmap(path);
            var data = new byte[bmp.Width * bmp.Height];
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    data[y * bmp.Width + x] = bmp.GetPixel(x, y).R; // greyscale/indexed — R channel
            return data;
        }
    }
}