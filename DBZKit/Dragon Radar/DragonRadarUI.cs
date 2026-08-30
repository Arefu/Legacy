using Bulma;
using DrGero;
using DrGero.IO;
using DrGero.Loader;
using DrGero.Rendering;
using DrGero.Types;
using DrGero.UI;
using System.Drawing.Drawing2D;
using static DrGero.Types.MapEntry;

namespace Dragon_Radar
{
    public partial class DragonRadarUI : Form
    {
        private IGame? _game;
        private ROM? _rom;

        public DragonRadarUI()
        {
            InitializeComponent();
            mapPictureBox.Paint += mapPictureBox_Paint;
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

        private const int TileSize = 8;

        private Bitmap? _currentMapBitmap;
        private readonly ImageList _tileThumbnails = new ImageList
        {
            ImageSize = new Size(32, 32),
            ColorDepth = ColorDepth.Depth32Bit
        };

        private void mapTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            _currentMapBitmap?.Dispose();
            _currentMapBitmap = null;

            if (_rom == null || _game == null || e.Node?.Tag is not MapEntry entry)
            {
                listView1.Items.Clear();
                _tileThumbnails.Images.Clear();
                _currentEntities.Clear();
                mapPictureBox.Invalidate();
                return;
            }

            _rom.PushPosition(entry.VariationArray);
            int mapOffset = _rom.ReadPointer();
            _rom.PopPosition();

            var (bitmap, tilesets, usedTilesets) = MapRenderer.RenderMap(_rom, _game.Config, mapOffset, new MapRenderOptions());
            _currentMapBitmap = bitmap;

            _currentEntities = EntityReader.ReadNpcArray(_rom, entry);   // <-- this line was missing
            _currentEntities.AddRange(EntityReader.ReadMapTriggers(_rom, entry));
            PopulateTileList(tilesets, usedTilesets);

            mapPictureBox.Invalidate();
        }

        // Slices every used tileset sheet into individual TileSize x TileSize tiles
        // and shows one icon per tile in listView1, labeled with its absolute tile id.
        private void PopulateTileList(Dictionary<Range, Bitmap> tilesets, HashSet<Range> usedTilesets)
        {
            listView1.Items.Clear();
            _tileThumbnails.Images.Clear();
            listView1.LargeImageList = _tileThumbnails;
            listView1.View = View.LargeIcon;

            int outputSize = _tileThumbnails.ImageSize.Width;

            var images = new List<Image>();
            var items = new List<ListViewItem>();

            foreach (var range in usedTilesets.OrderBy(r => r.Start.Value))
            {
                if (!tilesets.TryGetValue(range, out var sheet))
                    continue;

                int columns = sheet.Width / TileSize;
                int rows = sheet.Height / TileSize;
                int rangeStart = range.Start.Value;

                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < columns; col++)
                    {
                        int tileId = rangeStart + (row * columns) + col;
                        if (tileId > range.End.Value)
                            break;

                        var tileBmp = new Bitmap(outputSize, outputSize);
                        using (var g = Graphics.FromImage(tileBmp))
                        {
                            g.InterpolationMode = InterpolationMode.NearestNeighbor;
                            g.PixelOffsetMode = PixelOffsetMode.Half;
                            g.DrawImage(sheet,
                                new Rectangle(0, 0, outputSize, outputSize),
                                new Rectangle(col * TileSize, row * TileSize, TileSize, TileSize),
                                GraphicsUnit.Pixel);
                        }

                        images.Add(tileBmp);
                        items.Add(new ListViewItem($"Tile {tileId}", images.Count - 1));
                    }
                }
            }

            _tileThumbnails.Images.AddRange(images.ToArray());

            listView1.BeginUpdate();
            listView1.Items.AddRange(items.ToArray());
            listView1.EndUpdate();

            foreach (var img in images)
                img.Dispose();
        }

        // In DragonRadarUI: overlay markers on top of _currentMapBitmap
        private List<Entity> _currentEntities = new();

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

            foreach (var entity in _currentEntities)
            {
                var color = entity.Kind switch
                {
                    EntityKind.Npc => Color.Cyan,
                    EntityKind.Object => Color.Yellow,
                    EntityKind.SpawnScript => Color.Red,
                    EntityKind.Trigger => Color.Lime,
                    _ => Color.White
                };

                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, entity.X - 3, entity.Y - 3, 6, 6);
            }
        }

        private void toolStrip_OpenROM_Click(object sender, EventArgs e)
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
        }
    }
}