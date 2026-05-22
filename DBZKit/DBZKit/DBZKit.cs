using DBZKit.Assets;

namespace DBZKit
{
    public partial class DBZKit : Form
    {
        private readonly Dictionary<int, byte[]> _PortraitData = [];
        private readonly Dictionary<int, byte[]> _ItemData = [];

        private readonly ImageList _PortraitImageList;
        private readonly ImageList _ItemImageList;

        private byte[]? _GBARom;

        public DBZKit()
        {
            InitializeComponent();

            _PortraitImageList = new ImageList
            {
                ImageSize = new Size(128, 128),
                ColorDepth = ColorDepth.Depth32Bit
            };

            _ItemImageList = new ImageList
            {
                ImageSize = new Size(64, 64),
                ColorDepth = ColorDepth.Depth32Bit
            };

            ListView_ItemViewer.View = View.LargeIcon;
            ListView_ItemViewer.LargeImageList = _ItemImageList;
            ListView_ItemViewer.AutoArrange = true;

            ListView_PortraitViewer.View = View.LargeIcon;
            ListView_PortraitViewer.LargeImageList = _PortraitImageList;
            ListView_PortraitViewer.ContextMenuStrip = AssetContextMenu;

            AssetContextMenu.Items.Add("Export PNG", null, OnExportPng);
            AssetContextMenu.Items.Add("Export BIN", null, OnExportBin);
            AssetContextMenu.Items.Add(new ToolStripSeparator());
            AssetContextMenu.Items.Add("Replace...", null, OnReplace);
        }

        private void OnExportPng(object? sender, EventArgs e)
        {
            ListView? SelectedListView = (ListView)((ContextMenuStrip)((ToolStripMenuItem)sender).Owner).SourceControl;
            if (SelectedListView == null || SelectedListView.SelectedItems.Count == 0)
                return;

            string key = SelectedListView.SelectedItems[0].ImageKey;

            SaveFileDialog save = new() { Filter = "PNG Image|*.png", FileName = key };
            if (save.ShowDialog() != DialogResult.OK)
                return;

            switch (SelectedListView.Name)
            {
                case "ListView_PortraitViewer":
                    Bitmap? bitmap = (Bitmap)_PortraitImageList.Images[key];
                    Portraits.ExportPng(bitmap, save.FileName);
                    break;
                case "ListView_ItemViewer":
                    _ = MessageBox.Show("1");
                    break;
                default:
                    break;
            }
        }

        private void OnExportBin(object? sender, EventArgs e)
        {
            if (ListView_PortraitViewer.SelectedItems.Count == 0)
            {
                return;
            }

            string key = ListView_PortraitViewer.SelectedItems[0].ImageKey;
            int index = int.Parse(key.Split('_')[1]);

            SaveFileDialog save = new() { Filter = "Binary|*.bin", FileName = key };
            if (save.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            Portraits.ExportBin(_PortraitData[index], save.FileName);
        }

        private void OnReplace(object? sender, EventArgs e)
        {
            if (ListView_PortraitViewer.SelectedItems.Count == 0)
            {
                return;
            }

            _ = ListView_PortraitViewer.SelectedItems[0].ImageKey;

            OpenFileDialog open = new() { Filter = "PNG Image|*.png|Binary|*.bin" };
            if (open.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _ = MessageBox.Show("Replace not yet implemented.", "Coming Soon", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenRomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog OpenFile = new();
            if (OpenFile.ShowDialog() != DialogResult.OK)
            {
                MessageBox.Show("Please select a GBA ROM file to continue.", "No ROM Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _GBARom = File.ReadAllBytes(OpenFile.FileName);
            Portraits.Load(_GBARom, _PortraitImageList, ListView_PortraitViewer, _PortraitData, GBA.ReadPalette(_GBARom, 0x081DA6C8));
            Items.Load(_GBARom, _ItemImageList, ListView_ItemViewer, _ItemData, GBA.ReadPalette(_GBARom, 0x081DA6C8));
            MapTest.LoadFirstMap(_GBARom);
        }
    }
}
