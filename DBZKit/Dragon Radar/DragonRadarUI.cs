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
            mapPictureBox.MouseDown += mapPictureBox_MouseDown;
            mapPictureBox.MouseMove += mapPictureBox_MouseMove;
            mapPictureBox.MouseUp += mapPictureBox_MouseUp;
            previewCollisionCheckBox.Checked = toolStrip_ShowCollision.Checked;
            KeyPreview = true;
            KeyDown += DragonRadarUI_KeyDown;
        }

        private void DragonRadarUI_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _placingItemId.HasValue)
            {
                _placingItemId = null;
                UpdateStatusLabel();
            }
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
        private const int DotRadius = 3;
        private const int HitTestPadding = 4; // extra pixels of forgiveness when clicking a point-like entity

        private Bitmap? _currentMapBitmap;
        private readonly ImageList _tileThumbnails = new ImageList
        {
            ImageSize = new Size(32, 32),
            ColorDepth = ColorDepth.Depth32Bit
        };
        private readonly ImageList _itemThumbnails = new ImageList
        {
            ImageSize = new Size(32, 32),
            ColorDepth = ColorDepth.Depth32Bit
        };

        // Set by clicking an entry in the Items tab; the next click on the map
        // places a new EntityKind.Item marker there instead of selecting/dragging.
        // SourceAddress is 0 for these -- they don't exist in the ROM yet, so
        // Save ROM As can't write them back (see toolStrip_SaveROM_Click).
        private int? _placingItemId;
        private (int OnPickup, int CollectionMsg) _placingTemplate;

        // Scans g_ItemsInGame once per ROM load and fills the Items tab with an
        // icon + id for every entry that looks real (see EnumerateValidItemIds).
        private void PopulateItemList()
        {
            itemsListView.Items.Clear();
            _itemThumbnails.Images.Clear();
            itemsListView.LargeImageList = _itemThumbnails;
            itemsListView.View = View.LargeIcon;

            if (_rom == null || _game == null) return;

            var images = new List<Image>();
            var items = new List<ListViewItem>();

            foreach (int itemId in ItemIconReader.EnumerateValidItemIds(_rom))
            {
                var icon = ItemIconReader.GetIcon(_rom, _game.Config, itemId) ?? new Bitmap(_itemThumbnails.ImageSize.Width, _itemThumbnails.ImageSize.Height);
                images.Add(icon);
                items.Add(new ListViewItem($"Item {itemId}", images.Count - 1) { Tag = itemId });
            }

            _itemThumbnails.Images.AddRange(images.ToArray());

            itemsListView.BeginUpdate();
            itemsListView.Items.AddRange(items.ToArray());
            itemsListView.EndUpdate();
        }

        private void itemsListView_MouseDown(object? sender, MouseEventArgs e)
        {
            var item = itemsListView.GetItemAt(e.X, e.Y);
            if (item?.Tag is not int itemId) return;

            if (_rom == null || _game == null)
            {
                ArmPlacement(itemId, 0, 0);
                return;
            }

            var templates = EntityReader.FindPickupTemplates(_rom, _game.MapEntries, itemId);

            if (templates.Count <= 1)
            {
                var t = templates.Count == 1 ? templates[0] : (OnPickup: 0, CollectionMsg: 0, MapName: "");
                ArmPlacement(itemId, t.OnPickup, t.CollectionMsg);
                return;
            }

            // Multiple existing pickup scripts use this item -- let the user pick which
            // one the new placement should reuse, instead of silently guessing.
            var menu = new ContextMenuStrip();
            foreach (var t in templates)
            {
                string label = string.IsNullOrEmpty(t.MapName) ? $"onPickup=0x{t.OnPickup:X} msg=0x{t.CollectionMsg:X}" : $"{t.MapName} (onPickup=0x{t.OnPickup:X} msg=0x{t.CollectionMsg:X})";
                var onPickup = t.OnPickup;
                var collectionMsg = t.CollectionMsg;
                menu.Items.Add(label, null, (_, _) => ArmPlacement(itemId, onPickup, collectionMsg));
            }
            menu.Show(itemsListView, e.Location);
        }

        private void ArmPlacement(int itemId, int onPickup, int collectionMsg)
        {
            _placingItemId = itemId;
            _placingTemplate = (onPickup, collectionMsg);
            statusLabel.Text = $"Placing item {itemId} — click the map to place it, Esc to cancel";
        }

        private void mapTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            _currentMapBitmap?.Dispose();
            _currentMapBitmap = null;
            _selectedIndex = -1;
            _dragging = false;
            _dirty = false;

            if (_rom == null || _game == null || e.Node?.Tag is not MapEntry entry)
            {
                listView1.Items.Clear();
                _tileThumbnails.Images.Clear();
                _currentEntities.Clear();
                _hasUnsupportedLayer = false;
                _collisionGrid = null;
                previewPictureBox.Image = null;
                mapPictureBox.Invalidate();
                UpdateStatusLabel();
                return;
            }

            _currentEntry = entry;

            _rom.PushPosition(entry.VariationArray);
            int mapOffset = _rom.ReadPointer();
            _rom.PopPosition();
            _currentMapOffset = mapOffset;

            var (bitmap, tilesets, usedTilesets, hasUnsupportedLayer, collisionGrid) = MapRenderer.RenderMap(_rom, _game.Config, mapOffset, new MapRenderOptions());
            _currentMapBitmap = bitmap;
            _hasUnsupportedLayer = hasUnsupportedLayer;
            _collisionGrid = collisionGrid;
            _viewportOrigin = new Point(0, 0);

            // mapPictureBox draws the bitmap manually (see mapPictureBox_Paint), not via
            // the Image property, so it doesn't auto-size to the map's real dimensions --
            // do it explicitly so mapScrollPanel's AutoScroll can reach every part of maps
            // bigger than the visible viewport (previously silently clipped, which is what
            // made entities/triggers near map edges look misplaced).
            mapPictureBox.Size = bitmap.Size;

            _currentEntities = EntityReader.ReadLevelGates(_rom, entry);
            _currentEntities.AddRange(EntityReader.ReadMapTriggers(_rom, entry));
            _currentEntities.AddRange(EntityReader.ReadObjectArray(_rom, entry));
            _currentEntities.AddRange(EntityReader.ReadMapScripts(_rom, entry));
            _currentEntities.AddRange(EntityReader.ReadMapItems(_rom, entry, mapOffset));
            PopulateTileList(tilesets, usedTilesets);

            mapPictureBox.Invalidate();
            UpdateStatusLabel();
            RefreshPreview();
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

        // Overlay markers on top of _currentMapBitmap, plus editor state (selection/drag).
        private List<Entity> _currentEntities = new();
        private MapEntry? _currentEntry;
        private int _currentMapOffset;
        private int _selectedIndex = -1;
        private bool _dragging = false;
        private Point _dragGrabOffset; // cursor position relative to the entity's own X/Y at drag start
        private bool _dirty = false;
        private bool _hasUnsupportedLayer = false; // see MapRenderer.DrawLayer's 0x4FCD comment

        // GBA-screen (240x160) viewport preview state. The real hardware resolution --
        // see MapRenderer.ReadCollisionMap's doc for how the collision grid this also
        // uses was traced. _viewportOrigin is the top-left of the crop, in the same
        // pixel space as _currentMapBitmap (map/world pixels, not screen/control pixels).
        private const int GbaScreenWidth = 240;
        private const int GbaScreenHeight = 160;
        private bool[,]? _collisionGrid;
        private Point _viewportOrigin;
        private bool _draggingViewport;

        private static Color ColorFor(EntityKind kind) => kind switch
        {
            EntityKind.LevelGate => Color.Cyan,
            EntityKind.Object => Color.Yellow,
            EntityKind.SpawnScript => Color.Red,
            EntityKind.Trigger => Color.Lime,
            EntityKind.Character => Color.Orange,
            EntityKind.Item => Color.Magenta,
            _ => Color.White
        };

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

            if (toolStrip_ShowCollision.Checked && _collisionGrid != null)
            {
                // Draw straight onto the screen via a throwaway same-size overlay bitmap rather
                // than mutating _currentMapBitmap -- this runs on every repaint (e.g. while
                // dragging the viewport), and the cached map bitmap must stay pristine for the
                // preview crop (RefreshPreview) and tile list (which reads from the tilesets
                // dictionary, not the composite, but best not to risk compounding tints anyway).
                using var overlay = new Bitmap(_currentMapBitmap.Width, _currentMapBitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                MapRenderer.DrawCollisionOverlay(_collisionGrid, overlay);
                g.DrawImage(overlay, 0, 0);
            }

            for (int i = 0; i < _currentEntities.Count; i++)
            {
                var entity = _currentEntities[i];
                var color = ColorFor(entity.Kind);

                using var pen = new Pen(color, 1);

                if (entity.Width > 0 || entity.Height > 0)
                {
                    // Trigger zone — draw the actual rect so misalignment is obvious at a glance.
                    g.DrawRectangle(pen, entity.X, entity.Y, entity.Width, entity.Height);
                }
                else if (entity.Kind == EntityKind.LevelGate)
                {
                    // Mirrors the game's own overlay (see LevelGate_Create/sub_8010304 in
                    // IDA): a small number badge showing the required level. The real game
                    // sometimes shows '?' instead until the barrier's been scanned, but the
                    // editor always shows the actual value since that's more useful here.
                    string label = entity.TypeId.ToString();
                    using var font = new Font(FontFamily.GenericSansSerif, 7f, FontStyle.Bold);
                    var textSize = g.MeasureString(label, font);
                    var badgeRect = new RectangleF(entity.X - textSize.Width / 2 - 1, entity.Y - textSize.Height / 2, textSize.Width + 2, textSize.Height);

                    using (var bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                        g.FillRectangle(bg, badgeRect);
                    g.DrawRectangle(pen, badgeRect.X, badgeRect.Y, badgeRect.Width, badgeRect.Height);
                    using (var textBrush = new SolidBrush(color))
                        g.DrawString(label, font, textBrush, badgeRect.X + 1, badgeRect.Y);
                }
                else
                {
                    // Object.TypeId is a real g_ItemsInGame id (see ReadObjectArray) --
                    // safe to use as an icon lookup. Item.TypeId is NOT: it's MapItem's
                    // itemIndex, an index into the map's decoration table (graphicObjects[],
                    // see ReadMapItems), a completely different numbering. Feeding it into
                    // ItemIconReader was drawing whatever pickup happened to share that
                    // small index (e.g. index 0-2) instead of the actual rock/object --
                    // confirmed against real ROM data for Zone1/Area1's three rocks.
                    Bitmap? icon = entity.Kind == EntityKind.Object && _rom != null && _game != null
                        ? ItemIconReader.GetIcon(_rom, _game.Config, entity.TypeId)
                        : null;

                    if (icon != null)
                    {
                        g.DrawImage(icon, entity.X - icon.Width / 2, entity.Y - icon.Height / 2, icon.Width, icon.Height);
                    }
                    else
                    {
                        using var brush = new SolidBrush(color);
                        g.FillEllipse(brush, entity.X - DotRadius, entity.Y - DotRadius, DotRadius * 2, DotRadius * 2);
                    }
                }

                if (i == _selectedIndex)
                {
                    using var selectPen = new Pen(Color.White, 1) { DashStyle = DashStyle.Dash };
                    int w = Math.Max(entity.Width, DotRadius * 2) + 4;
                    int h = Math.Max(entity.Height, DotRadius * 2) + 4;
                    g.DrawRectangle(selectPen, entity.X - w / 2, entity.Y - h / 2, w, h);
                }
            }

            // The draggable GBA-screen (240x160) viewport -- drag with the RIGHT mouse
            // button anywhere on the map to move it (left button is entity select/drag,
            // already used above). See RefreshPreview for what feeds previewPictureBox.
            using var viewportPen = new Pen(Color.Yellow, 2);
            g.DrawRectangle(viewportPen, _viewportOrigin.X, _viewportOrigin.Y, GbaScreenWidth, GbaScreenHeight);
        }

        private int HitTest(Point clickLocation)
        {
            // Iterate back-to-front so entities drawn last (on top) are picked first.
            for (int i = _currentEntities.Count - 1; i >= 0; i--)
            {
                var entity = _currentEntities[i];

                if (entity.Width > 0 || entity.Height > 0)
                {
                    var rect = new Rectangle(entity.X, entity.Y, entity.Width, entity.Height);
                    if (rect.Contains(clickLocation))
                        return i;
                }
                else
                {
                    int dx = clickLocation.X - entity.X;
                    int dy = clickLocation.Y - entity.Y;
                    if (dx * dx + dy * dy <= (DotRadius + HitTestPadding) * (DotRadius + HitTestPadding))
                        return i;
                }
            }

            return -1;
        }

        private void mapPictureBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (_currentMapBitmap == null) return;

            if (e.Button == MouseButtons.Right)
            {
                _draggingViewport = true;
                MoveViewportTo(e.Location);
                return;
            }

            if (_placingItemId.HasValue)
            {
                _currentEntities.Add(new Entity(EntityKind.Object, e.Location.X, e.Location.Y, _placingItemId.Value, SourceAddress: 0, OnPickup: _placingTemplate.OnPickup, CollectionMsg: _placingTemplate.CollectionMsg));
                _selectedIndex = _currentEntities.Count - 1;
                _placingItemId = null;
                _dirty = true;

                UpdateStatusLabel();
                mapPictureBox.Invalidate();
                return;
            }

            _selectedIndex = HitTest(e.Location);

            if (_selectedIndex >= 0)
            {
                var entity = _currentEntities[_selectedIndex];
                _dragGrabOffset = new Point(e.Location.X - entity.X, e.Location.Y - entity.Y);
                _dragging = true;
            }

            UpdateStatusLabel();
            mapPictureBox.Invalidate();
        }

        private void mapPictureBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_draggingViewport)
            {
                MoveViewportTo(e.Location);
                return;
            }

            if (!_dragging || _selectedIndex < 0) return;

            var entity = _currentEntities[_selectedIndex];
            int newX = e.Location.X - _dragGrabOffset.X;
            int newY = e.Location.Y - _dragGrabOffset.Y;

            if (newX == entity.X && newY == entity.Y) return;

            _currentEntities[_selectedIndex] = entity with { X = newX, Y = newY };
            _dirty = true;

            UpdateStatusLabel();
            mapPictureBox.Invalidate();
        }

        private void mapPictureBox_MouseUp(object? sender, MouseEventArgs e)
        {
            _dragging = false;
            _draggingViewport = false;
        }

        // Centers the 240x160 GBA-screen viewport on the given map-pixel point (clamped so it
        // never runs off the map bitmap), then repaints the map overlay and the preview crop.
        private void MoveViewportTo(Point mapLocation)
        {
            if (_currentMapBitmap == null) return;

            int maxX = Math.Max(0, _currentMapBitmap.Width - GbaScreenWidth);
            int maxY = Math.Max(0, _currentMapBitmap.Height - GbaScreenHeight);

            int x = Math.Clamp(mapLocation.X - (GbaScreenWidth / 2), 0, maxX);
            int y = Math.Clamp(mapLocation.Y - (GbaScreenHeight / 2), 0, maxY);

            if (x == _viewportOrigin.X && y == _viewportOrigin.Y) return;

            _viewportOrigin = new Point(x, y);
            mapPictureBox.Invalidate();
            RefreshPreview();
        }

        /// <summary>
        /// Crops _currentMapBitmap to the current 240x160 viewport (what you'd actually see on
        /// real GBA hardware scrolled to that position, since MapRenderer.RenderMap already
        /// composites layers in real GBA priority order -- see MapRenderer.RenderMap's comment)
        /// and shows it in previewPictureBox, with the same optional collision overlay as the
        /// main map view.
        /// </summary>
        private void RefreshPreview()
        {
            previewPictureBox.Image?.Dispose();
            previewPictureBox.Image = null;

            if (_currentMapBitmap == null) return;

            int cropW = Math.Min(GbaScreenWidth, _currentMapBitmap.Width);
            int cropH = Math.Min(GbaScreenHeight, _currentMapBitmap.Height);
            var cropRect = new Rectangle(_viewportOrigin.X, _viewportOrigin.Y, cropW, cropH);

            var crop = _currentMapBitmap.Clone(cropRect, _currentMapBitmap.PixelFormat);

            if (toolStrip_ShowCollision.Checked && _collisionGrid != null)
            {
                int originTileX = _viewportOrigin.X / TileSize;
                int originTileY = _viewportOrigin.Y / TileSize;
                MapRenderer.DrawCollisionOverlay(_collisionGrid, crop, originTileX, originTileY);
            }

            previewPictureBox.Image = crop;
            previewCoordLabel.Text = $"Viewport: ({_viewportOrigin.X}, {_viewportOrigin.Y})";
        }

        private void toolStrip_ShowCollision_CheckedChanged(object sender, EventArgs e)
        {
            previewCollisionCheckBox.CheckedChanged -= previewCollisionCheckBox_CheckedChanged;
            previewCollisionCheckBox.Checked = toolStrip_ShowCollision.Checked;
            previewCollisionCheckBox.CheckedChanged += previewCollisionCheckBox_CheckedChanged;

            mapPictureBox.Invalidate();
            RefreshPreview();
        }

        private void previewCollisionCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            toolStrip_ShowCollision.CheckedChanged -= toolStrip_ShowCollision_CheckedChanged;
            toolStrip_ShowCollision.Checked = previewCollisionCheckBox.Checked;
            toolStrip_ShowCollision.CheckedChanged += toolStrip_ShowCollision_CheckedChanged;

            mapPictureBox.Invalidate();
            RefreshPreview();
        }

        private void toolStrip_ShowPreviewTab_Click(object sender, EventArgs e)
        {
            sidebarTabControl.SelectedTab = previewTabPage;
        }

        private void UpdateStatusLabel()
        {
            string warning = _hasUnsupportedLayer
                ? " -- WARNING: this map uses an unrecognized layer format (0x4FCD) and is not fully rendered"
                : "";

            if (_selectedIndex < 0 || _selectedIndex >= _currentEntities.Count)
            {
                statusLabel.Text = (_dirty ? "No entity selected (unsaved position edits pending)" : "No entity selected") + warning;
                return;
            }

            var entity = _currentEntities[_selectedIndex];
            string detail = entity.Kind switch
            {
                EntityKind.Object => $"item id {entity.TypeId}",
                EntityKind.Item => $"item id {entity.TypeId} (position approximate)",
                EntityKind.Character => $"spriteId {entity.TypeId}",
                EntityKind.LevelGate => $"requires level {entity.TypeId}",
                EntityKind.Trigger => $"{entity.Width}x{entity.Height} zone",
                _ => $"type {entity.TypeId}"
            };

            string addressLabel = entity.SourceAddress == 0 ? "new, not yet saved" : $"0x{entity.SourceAddress:X}";
            string dirtyMarker = _dirty ? " [unsaved]" : "";
            statusLabel.Text = $"{entity.Kind} @ ({entity.X},{entity.Y}) — {detail} — {addressLabel}{dirtyMarker}{warning}";
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
            PopulateItemList();
        }

        // Writes every entity's current (possibly edited) position back into a copy
        // of the ROM and saves it under a new filename. Moving existing entities
        // patches their bytes in place. Newly-placed items (EntityKind.Item,
        // SourceAddress == 0) are persisted by growing mapItems/graphicObjects:
        // the combined (old + new) array contents are appended at the end of the
        // file, the old array storage is zeroed out, and the owning MapEntry /
        // Map_VariationEntry fields are repointed at the new location (see
        // EntityWriter.PersistNewItems for the full rationale/caveats).
        private void toolStrip_SaveROM_Click(object sender, EventArgs e)
        {
            if (_rom == null)
            {
                MessageBox.Show("No ROM loaded.");
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Title = "Save edited ROM As",
                Filter = "GBA ROM|*.gba"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            var editedRom = ROM.FromBytes(_rom.ToArray());

            int written = 0;
            foreach (var entity in _currentEntities)
            {
                // New, not-yet-saved entities (SourceAddress == 0) have no real
                // PositionAddress yet -- it defaults to 0 (see Entity), which
                // would patch bytes at absolute file offset 0, i.e. the ROM
                // header. CONFIRMED via a real corrupted save: this clobbered
                // the boot instruction at offset 0 with the new item's x/y,
                // producing exactly the "unrecognized format" load failure
                // reported. New entities are persisted separately below
                // (PersistNewObjects), not through WritePosition.
                if (entity.SourceAddress == 0)
                    continue;

                try
                {
                    EntityWriter.WritePosition(editedRom, entity, entity.X, entity.Y);
                    written++;
                }
                catch (NotSupportedException)
                {
                    // Kind without a known write-back format yet (e.g. SpawnScript) — skipped.
                }
                catch (InvalidOperationException ex)
                {
                    // WritePosition's address-0 guard tripped -- shouldn't happen given the
                    // SourceAddress check above, but surface it loudly instead of corrupting
                    // the ROM silently if it ever does.
                    MessageBox.Show(ex.Message, "Refused an unsafe write", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            int placed = 0;
            string? placeError = null;

            if (_currentEntry != null && _game != null)
            {
                int entryIndex = -1;
                for (int i = 0; i < _game.MapEntries.Count; i++)
                {
                    if (ReferenceEquals(_game.MapEntries[i], _currentEntry)) { entryIndex = i; break; }
                }

                if (entryIndex >= 0)
                {
                    int mapEntryAddress = _game.Config.MapEntriesOffset + (entryIndex * MapEntry.RecordSize);
                    try
                    {
                        placed = EntityWriter.PersistNewObjects(editedRom, _currentEntry, mapEntryAddress, _currentEntities);
                    }
                    catch (InvalidOperationException ex)
                    {
                        placeError = ex.Message;
                    }
                }
            }

            // editedRom may have grown (PersistNewObjects can allocate free space
            // within it), so pull the final bytes from it rather than an earlier snapshot.
            File.WriteAllBytes(dialog.FileName, editedRom.ToArray());

            if (placeError != null)
                MessageBox.Show(placeError, "Couldn't place new item(s)", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _dirty = false;
            UpdateStatusLabel();
            MessageBox.Show($"Saved {written} entity position(s) and {placed} newly-placed item(s) to {Path.GetFileName(dialog.FileName)}.\n\nReopen the saved file to keep editing the newly-placed items further.");
        }
    }
}
