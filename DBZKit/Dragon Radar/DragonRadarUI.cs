using Bulma;
using DrGero;
using DrGero.IO;
using DrGero.Loader;
using DrGero.Rendering;
using DrGero.Types;
using DrGero.UI;
using System.Drawing.Drawing2D;

namespace Dragon_Radar
{
    public partial class DragonRadarUI : Form
    {
        private IGame? _game;
        private ROM? _rom;

        public DragonRadarUI()
        {
            InitializeComponent();
            mapTreeView.AfterSelect += mapTreeView_AfterSelect;
            mapPictureBox.Paint += mapPictureBox_Paint;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Open GBA ROM",
                Filter = "GBA ROM|*.gba"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            _rom = ROM.FromFile(dialog.FileName);
            var configs = GameLibrary.LoadAll("games/");
            _game = GameFactory.Detect(_rom, configs);

            if (_game == null)
            {
                MessageBox.Show("ROM not supported.");
                return;
            }

            _game.Load(_rom);
            _game.DumpMapEntries("dump.txt");

            PopulateMapTree();

            MessageBox.Show($"Loaded {_game.MapEntries.Count} maps.");
        }

        private void PopulateMapTree()
        {
            if (_game == null) return;

            mapTreeView.BeginUpdate();
            mapTreeView.Nodes.Clear();

            foreach (var zoneNode in MapTreeBuilder.Build(_game.MapEntries))
            {
                mapTreeView.Nodes.Add(BuildTreeNode(zoneNode));
            }

            mapTreeView.EndUpdate();
        }

        private static TreeNode BuildTreeNode(MapTreeNode node)
        {
            var tvNode = new TreeNode(node.Text) { Tag = node.Entry };
            foreach (var child in node.Children)
                tvNode.Nodes.Add(BuildTreeNode(child));
            return tvNode;
        }

        private Bitmap? _currentMapBitmap;
        private readonly ImageList _tilesetThumbnails = new ImageList
        {
            ImageSize = new Size(64, 64),
            ColorDepth = ColorDepth.Depth32Bit
        };

        private void mapTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            _currentMapBitmap?.Dispose();
            _currentMapBitmap = null;
            listView1.Items.Clear();
            _tilesetThumbnails.Images.Clear();
            listView1.LargeImageList = _tilesetThumbnails;

            if (_rom == null || _game == null || e.Node?.Tag is not MapEntry entry)
            {
                mapPictureBox.Invalidate();
                return;
            }

            try
            {
                _rom.PushPosition(entry.VariationArray);
                int mapOffset = _rom.ReadPointer();
                _rom.PopPosition();

                var (bitmap, tilesets, usedTilesets) = MapRenderer.RenderMap(_rom, _game.Config, mapOffset, new MapRenderOptions());
                _currentMapBitmap = bitmap;

                PopulateTilesetList(tilesets, usedTilesets);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Render failed:\r\n{ex}", "Map Render Error");
            }

            mapPictureBox.Invalidate();
        }

        private void PopulateTilesetList(Dictionary<Range, Bitmap> tilesets, HashSet<Range> usedTilesets)
        {
            listView1.Items.Clear();
            _tilesetThumbnails.Images.Clear();
            listView1.View = View.LargeIcon;

            int imageIndex = 0;

            foreach (var range in usedTilesets.OrderBy(r => r.Start.Value))
            {
                if (!tilesets.TryGetValue(range, out var atlasBitmap))
                    continue;

                // The atlas bitmap may be larger than the thumbnail size (it's the
                // whole tileset sheet for this range, not a single tile) — scale it
                // down so it fits the ImageList icon size instead of getting cropped.
                var thumb = new Bitmap(_tilesetThumbnails.ImageSize.Width, _tilesetThumbnails.ImageSize.Height);
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(atlasBitmap, 0, 0, thumb.Width, thumb.Height);
                }

                _tilesetThumbnails.Images.Add(thumb);

                int count = range.End.Value - range.Start.Value + 1;
                var item = new ListViewItem($"{range.Start.Value}-{range.End.Value} ({count} tiles)", imageIndex);
                listView1.Items.Add(item);

                imageIndex++;
            }
        }

        private void mapPictureBox_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            if (_currentMapBitmap == null)
            {
                g.Clear(Color.Black);
                return;
            }

            g.DrawImage(_currentMapBitmap, 0, 0);
        }
    }
}