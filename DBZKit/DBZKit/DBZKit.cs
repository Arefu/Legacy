using DBZKit.Assets;
using System.Diagnostics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace DBZKit
{
    public partial class DBZKit : Form
    {
        private readonly Dictionary<int, byte[]> _PortraitData = new();
        private readonly ImageList _PortraitImageList;
        private byte[]? _GBARom;

        public DBZKit()
        {
            InitializeComponent();

            _PortraitImageList = new ImageList
            {
                ImageSize = new Size(128, 128),
                ColorDepth = ColorDepth.Depth32Bit
            };

            ListView_PortraitViewer.View = View.LargeIcon;
            ListView_PortraitViewer.LargeImageList = _PortraitImageList;
            ListView_PortraitViewer.ContextMenuStrip = PortraitContextMenu;

            PortraitContextMenu.Items.Add("Export PNG", null, OnExportPng);
            PortraitContextMenu.Items.Add("Export BIN", null, OnExportBin);
            PortraitContextMenu.Items.Add(new ToolStripSeparator());
            PortraitContextMenu.Items.Add("Replace...", null, OnReplace);
        }

        private void OnExportPng(object? sender, EventArgs e)
        {
            if (ListView_PortraitViewer.SelectedItems.Count == 0) return;
            string key = ListView_PortraitViewer.SelectedItems[0].ImageKey;

            var save = new SaveFileDialog { Filter = "PNG Image|*.png", FileName = key };
            if (save.ShowDialog() != DialogResult.OK) return;

            var bitmap = (Bitmap)_PortraitImageList.Images[key];
            Portrait.ExportPng(bitmap, save.FileName);
        }

        private void OnExportBin(object? sender, EventArgs e)
        {
            if (ListView_PortraitViewer.SelectedItems.Count == 0) return;
            string key = ListView_PortraitViewer.SelectedItems[0].ImageKey;
            int index = int.Parse(key.Split('_')[1]);

            var save = new SaveFileDialog { Filter = "Binary|*.bin", FileName = key };
            if (save.ShowDialog() != DialogResult.OK) return;

            Portrait.ExportBin(_PortraitData[index], save.FileName);
        }

        private void OnReplace(object? sender, EventArgs e)
        {
            if (ListView_PortraitViewer.SelectedItems.Count == 0) return;
            string key = ListView_PortraitViewer.SelectedItems[0].ImageKey;

            var open = new OpenFileDialog { Filter = "PNG Image|*.png|Binary|*.bin" };
            if (open.ShowDialog() != DialogResult.OK) return;

            MessageBox.Show("Replace not yet implemented.", "Coming Soon", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void openRomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var OpenFile = new OpenFileDialog();
            if (OpenFile.ShowDialog() != DialogResult.OK)
            {
                MessageBox.Show("No File Selected", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _GBARom = File.ReadAllBytes(OpenFile.FileName);
            Portrait.Load(_GBARom, _PortraitImageList, ListView_PortraitViewer, _PortraitData, GBA.ReadPalette(_GBARom, 0x081DA6C8));
        }
    }
}
