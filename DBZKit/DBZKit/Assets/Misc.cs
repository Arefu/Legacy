using System.Drawing.Imaging;

namespace DBZKit.Assets
{
    internal static class Misc
    {
        private static readonly uint[] CompressedBlocks =
        [
            0x083ED3E4,
            0x083ED518,
            0x083EC86C,
            0x083ED2B0,
        ];

        internal static void Load(byte[] rom, ImageList imageList, ListView listView, Dictionary<int, byte[]> rawData, Color[]? palette = null)
        {

            for (int i = 0; i < CompressedBlocks.Length; i++)
            {
                uint spritePtr = CompressedBlocks[i];

                int deflatedSize = GBA.ReadInt32(rom, spritePtr + 4);
                int dataOffset = GBA.ToOffset(spritePtr) + 8;

                var result = Jcalg1Decompress.Decompress(rom, dataOffset, deflatedSize);

                var raw = GBA.Render8bpp(result.Data, 160, 64, palette);
                var scaled = new Bitmap(raw, new Size(160, 64));

                string key = $"unknown_{i}";
                rawData[i] = result.Data;
                imageList.Images.Add(key, scaled);
                listView.Items.Add(new ListViewItem(key, key));
            }

        }

        internal static void ExportPng(Bitmap bitmap, string path) =>
            bitmap.Save(path, ImageFormat.Png);

        internal static void ExportBin(byte[] rawData, string path) =>
            File.WriteAllBytes(path, rawData);
    }
}