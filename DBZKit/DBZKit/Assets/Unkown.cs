using System.Drawing.Imaging;

namespace DBZKit.Assets
{
    internal static class Unknowns
    {
        private const int SpriteWidthTiles = 4;
        private const int SpriteHeightTiles = 2;
        private const int SpriteBytes = 512;

        private static readonly (uint Ready, uint Cooling, string Name)[] AbilityTable =
        [
            (0x086D8C7C, 0x086D8E7C, "BigBang"),
            (0x086D907C, 0x086D927C, "BurningAttack"),
            (0x086D7754, 0x086D7954, "KiBlast"),
            (0x086D947C, 0x086D967C, "Kamehameha"),
            (0x086D987C, 0x086D9A7C, "Masenko"),
            (0x086D9C7C, 0x086D9C7C, "Disabled"),
            (0x08705AA4, 0x08705CA4, "Hercule"),
            (0x086D9E7C, 0x086DA07C, "EnergyPunch"),
            (0x086DA27C, 0x086DA47C, "ScatterShot"),
            (0x086DA67C, 0x086DA87C, "SpecialBeamCannon"),
            (0x086DAA7C, 0x086DAC7C, "SpiritBomb"),
            (0x086DAE7C, 0x086DB07C, "Transformation"),
            (0x086DAE7C, 0x086DB07C, "Transformation2"),
            (0x086D887C, 0x086D8A7C, "SwordBlast"),
        ];

        internal static void Load(byte[] rom, ImageList imageList, ListView listView, Dictionary<int, byte[]> rawData, Color[]? palette = null)
        {
            int index = 0;
            foreach (var (ready, cooling, name) in AbilityTable)
            {
                foreach (var (address, label) in new[] { (ready, $"{name}_ready"), (cooling, $"{name}_cooling") })
                {
                    int offset = GBA.ToOffset(address);
                    if (offset + SpriteBytes > rom.Length)
                        continue;

                    byte[] data = new byte[SpriteBytes];
                    Array.Copy(rom, offset, data, 0, SpriteBytes);

                    var bitmap = GBA.Render8bppTiled(data, SpriteWidthTiles, SpriteHeightTiles, palette);
                    var scaled = new Bitmap(bitmap, new Size(SpriteWidthTiles * 8, SpriteHeightTiles * 8));

                    rawData[index] = data;
                    imageList.Images.Add(label, scaled);
                    listView.Items.Add(new ListViewItem(label, label));

                    index++;
                }
            }
        }


        internal static void ExportPng(Bitmap bitmap, string path) => bitmap.Save(path, ImageFormat.Png);

        internal static void ExportBin(byte[] rawData, string path) => File.WriteAllBytes(path, rawData);
    }
}