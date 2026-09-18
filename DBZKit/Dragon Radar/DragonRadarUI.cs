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
            listView1.SelectedIndexChanged += listView1_SelectedIndexChanged;
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

            ArmPlacementWithDisambiguation(itemId, itemsListView, e.Location);
        }

        // Shared by the Items tab (place a fresh item of a chosen type) and the Objects tab
        // (place another copy of an item type that already exists somewhere) -- if the item
        // id has more than one distinct onPickup/collectionMsg pair already in the ROM, asks
        // which one the new placement should reuse rather than silently guessing which
        // pickup script applies.
        private void ArmPlacementWithDisambiguation(int itemId, Control menuOwner, Point menuAnchor)
        {
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
            menu.Show(menuOwner, menuAnchor);
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

            _highlightedTileId = null;

            if (_rom == null || _game == null || e.Node?.Tag is not MapEntry entry)
            {
                listView1.Items.Clear();
                _tileThumbnails.Images.Clear();
                _currentEntities.Clear();
                _hasUnsupportedLayer = false;
                _collisionGrid = null;
                _tileUsageIndex = new();
                DisposeDecorationSprites();
                mapPictureBox.Invalidate();
                UpdateStatusLabel();
                UpdatePropertiesPanel();
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
            _tileUsageIndex = MapRenderer.BuildTileUsageIndex(_rom, mapOffset);

            // mapPictureBox draws the bitmap manually (see mapPictureBox_Paint), not via
            // the Image property, so it doesn't auto-size to the map's real dimensions --
            // do it explicitly so mapScrollPanel's AutoScroll can reach every part of maps
            // bigger than the visible viewport (previously silently clipped, which is what
            // made entities/triggers near map edges look misplaced).
            mapPictureBox.Size = bitmap.Size;

            _currentEntities = EntityReader.ReadLevelGates(_rom, entry);
            _currentEntities.AddRange(EntityReader.ReadMapTriggers(_rom, entry));
            _currentEntities.AddRange(EntityReader.ReadVariationTriggers(_rom, mapOffset));
            _currentEntities.AddRange(EntityReader.ReadObjectArray(_rom, entry));
            _currentEntities.AddRange(EntityReader.ReadMapScripts(_rom, entry));
            _currentEntities.AddRange(EntityReader.ReadMapItems(_rom, entry, mapOffset));
            _currentEntities.AddRange(EntityReader.ReadGraphicObjects(_rom, mapOffset));

            DisposeDecorationSprites();
            _decorationSprites = MapRenderer.RenderGraphicObjectSprites(_rom, _game.Config, mapOffset);
            PopulateTileList(tilesets, usedTilesets);

            mapPictureBox.Invalidate();
            UpdateStatusLabel();
            UpdatePropertiesPanel();
        }

        private void DisposeDecorationSprites()
        {
            foreach (var sprite in _decorationSprites.Values)
                sprite.Dispose();
            _decorationSprites = new();
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
                        items.Add(new ListViewItem($"Tile {tileId}", images.Count - 1) { Tag = tileId });
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

        // Highlights every occurrence of the clicked tile on the map (see
        // MapRenderer.BuildTileUsageIndex and mapPictureBox_Paint's highlight box).
        private void listView1_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _highlightedTileId = listView1.SelectedItems.Count > 0 && listView1.SelectedItems[0].Tag is int tileId
                ? tileId
                : null;
            mapPictureBox.Invalidate();
        }

        // Overlay markers on top of _currentMapBitmap, plus editor state (selection/drag).
        private List<Entity> _currentEntities = new();
        private MapEntry? _currentEntry;
        private int _currentMapOffset;
        private int _selectedIndex = -1;
        private bool _dragging = false;
        private bool _resizing = false;
        private ResizeHandle _resizeHandle = ResizeHandle.None;
        private Point _dragGrabOffset; // cursor position relative to the entity's own X/Y at drag start
        private bool _dirty = false;
        private bool _hasUnsupportedLayer = false; // see MapRenderer.DrawLayer's 0x4FCD comment

        // Tile-list click-to-highlight: tileId -> every world-pixel position that tile
        // appears at across all 4 BG layers (see MapRenderer.BuildTileUsageIndex).
        private Dictionary<int, List<Point>> _tileUsageIndex = new();
        private int? _highlightedTileId;

        // graphicObjects decorations (trees, rocks, flight pads, etc) -- read as real
        // Entity records (EntityKind.Decoration, in _currentEntities) so they can be
        // selected/dragged like item pickups, instead of the old baked-into-the-bitmap
        // behavior. Keyed by Entity.SourceAddress (see MapRenderer.RenderGraphicObjectSprites).
        private Dictionary<int, Bitmap> _decorationSprites = new();

        // See MapRenderer.ReadCollisionMap's doc for how the collision grid was traced.
        private bool[,]? _collisionGrid;

        private static Color ColorFor(EntityKind kind) => kind switch
        {
            EntityKind.LevelGate => Color.Cyan,
            EntityKind.Object => Color.Yellow,
            EntityKind.SpawnScript => Color.Red,
            EntityKind.Trigger => Color.Lime,
            EntityKind.Character => Color.Orange,
            EntityKind.Item => Color.Magenta,
            EntityKind.Decoration => Color.SpringGreen,
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
                // than mutating _currentMapBitmap -- the cached map bitmap must stay pristine
                // for the tile list (which reads from the tilesets dictionary, not the
                // composite, but best not to risk compounding tints anyway).
                using var overlay = new Bitmap(_currentMapBitmap.Width, _currentMapBitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                MapRenderer.DrawCollisionOverlay(_collisionGrid, overlay);
                g.DrawImage(overlay, 0, 0);
            }

            // Decorations are drawn as a background pass first: they're real, often-opaque
            // sprite art (rocks, save point consoles, etc) and previously rendered blank, so
            // drawing them in normal entity order (after triggers) never mattered. Now that
            // they render real pixels, a decoration sharing a trigger's exact spot (e.g. a
            // save point's console and its save trigger) would otherwise paint over and hide
            // the trigger's outline -- reported as "triggers not showing now that rocks are".
            // Points/rects/gates always draw in a second pass on top so they stay visible
            // regardless of what decoration art happens to sit under them.
            for (int i = 0; i < _currentEntities.Count; i++)
            {
                var entity = _currentEntities[i];
                if (entity.Kind != EntityKind.Decoration) continue;

                if (_decorationSprites.TryGetValue(entity.SourceAddress, out var sprite))
                    g.DrawImage(sprite, entity.X, entity.Y);
                else
                    using (var pen = new Pen(ColorFor(entity.Kind), 1))
                        g.DrawRectangle(pen, entity.X, entity.Y, entity.Width, entity.Height);

                if (i == _selectedIndex)
                {
                    using var selectPen = new Pen(Color.White, 1) { DashStyle = DashStyle.Dash };
                    int w = entity.Width + 4;
                    int h = entity.Height + 4;
                    g.DrawRectangle(selectPen, entity.X - 2, entity.Y - 2, w, h);
                }
            }

            for (int i = 0; i < _currentEntities.Count; i++)
            {
                var entity = _currentEntities[i];
                if (entity.Kind == EntityKind.Decoration) continue;

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
                    // Triggers resize from anywhere on their border (see GetResizeHandleAt),
                    // not a discrete handle, so the dashed outline itself IS the grab target --
                    // no separate handle square to draw.
                    using var selectPen = new Pen(Color.White, 1) { DashStyle = DashStyle.Dash };

                    if (entity.Width > 0 || entity.Height > 0)
                    {
                        // FIXED: X/Y is this rect's top-left corner, not a center point --
                        // expand outward from the real rect instead of the point-entity
                        // centered math below, which previously shifted the outline by half
                        // the trigger's own size instead of hugging its actual bounds.
                        g.DrawRectangle(selectPen, entity.X - 2, entity.Y - 2, entity.Width + 4, entity.Height + 4);
                    }
                    else
                    {
                        int w = (DotRadius * 2) + 4;
                        int h = (DotRadius * 2) + 4;
                        g.DrawRectangle(selectPen, entity.X - w / 2, entity.Y - h / 2, w, h);
                    }
                }
            }

            // Tile-list click-to-highlight: outline every occurrence of the selected tile.
            if (_highlightedTileId.HasValue && _tileUsageIndex.TryGetValue(_highlightedTileId.Value, out var highlightPositions))
            {
                using var highlightPen = new Pen(Color.Cyan, 1);
                foreach (var p in highlightPositions)
                {
                    g.DrawRectangle(highlightPen, p.X, p.Y, TileSize - 1, TileSize - 1);
                }
            }

        }

        // Resize grabs anywhere within this many pixels of a trigger's border (not a
        // discrete handle square) -- edges resize one dimension, corners resize both,
        // matching standard bounding-box-editor conventions.
        private const int EdgeGrabMargin = 4;

        private enum ResizeHandle { None, Top, Bottom, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight }

        private static ResizeHandle GetResizeHandleAt(Entity entity, Point p)
        {
            var rect = new Rectangle(entity.X, entity.Y, entity.Width, entity.Height);

            bool nearLeft = Math.Abs(p.X - rect.Left) <= EdgeGrabMargin && p.Y >= rect.Top - EdgeGrabMargin && p.Y <= rect.Bottom + EdgeGrabMargin;
            bool nearRight = Math.Abs(p.X - rect.Right) <= EdgeGrabMargin && p.Y >= rect.Top - EdgeGrabMargin && p.Y <= rect.Bottom + EdgeGrabMargin;
            bool nearTop = Math.Abs(p.Y - rect.Top) <= EdgeGrabMargin && p.X >= rect.Left - EdgeGrabMargin && p.X <= rect.Right + EdgeGrabMargin;
            bool nearBottom = Math.Abs(p.Y - rect.Bottom) <= EdgeGrabMargin && p.X >= rect.Left - EdgeGrabMargin && p.X <= rect.Right + EdgeGrabMargin;

            if (nearTop && nearLeft) return ResizeHandle.TopLeft;
            if (nearTop && nearRight) return ResizeHandle.TopRight;
            if (nearBottom && nearLeft) return ResizeHandle.BottomLeft;
            if (nearBottom && nearRight) return ResizeHandle.BottomRight;
            if (nearLeft) return ResizeHandle.Left;
            if (nearRight) return ResizeHandle.Right;
            if (nearTop) return ResizeHandle.Top;
            if (nearBottom) return ResizeHandle.Bottom;
            return ResizeHandle.None;
        }

        private static Cursor CursorFor(ResizeHandle handle) => handle switch
        {
            ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
            ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
            ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
            ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
            _ => Cursors.Default
        };

        private int HitTest(Point clickLocation, bool skipTriggers = false)
        {
            // Iterate back-to-front so entities drawn last (on top) are picked first.
            for (int i = _currentEntities.Count - 1; i >= 0; i--)
            {
                var entity = _currentEntities[i];
                if (skipTriggers && entity.Kind == EntityKind.Trigger) continue; // Ctrl+Click passthrough -- see mapPictureBox_MouseDown

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

        // Every entity whose clickable region contains the point, topmost first -- for the
        // right-click disambiguation menu, where overlapping entities (e.g. a save trigger
        // sitting on top of its decoration) all need to be reachable, not just whichever one
        // HitTest's single-result would normally pick.
        private List<int> HitTestAll(Point p)
        {
            var results = new List<int>();
            for (int i = _currentEntities.Count - 1; i >= 0; i--)
            {
                var entity = _currentEntities[i];
                bool hit = entity.Width > 0 || entity.Height > 0
                    ? new Rectangle(entity.X, entity.Y, entity.Width, entity.Height).Contains(p)
                    : Math.Pow(p.X - entity.X, 2) + Math.Pow(p.Y - entity.Y, 2) <= Math.Pow(DotRadius + HitTestPadding, 2);

                if (hit) results.Add(i);
            }
            return results;
        }

        private static string DescribeEntity(Entity entity) => entity.Kind switch
        {
            // TypeId for Trigger is the raw vTable dispatch pointer read in ReadMapTriggers/
            // ReadVariationTriggers -- shown as a hex address since we don't have a confirmed
            // mapping from vtable address to trigger sub-type (DualRect/Dialog/Script/
            // SavePoint) yet. Not fabricating a friendly name we can't back up.
            EntityKind.Trigger => $"Trigger ({entity.Width}x{entity.Height}) vtable=0x{entity.TypeId:X}",
            EntityKind.Object => $"Object — item id {entity.TypeId}",
            EntityKind.Item => $"Item — id {entity.TypeId} (position approximate)",
            EntityKind.Decoration => $"Decoration — frame {entity.TypeId}",
            EntityKind.LevelGate => $"Level Gate — requires level {entity.TypeId}",
            EntityKind.Character => $"Character — sprite {entity.TypeId}",
            EntityKind.SpawnScript => "Spawn Script",
            _ => entity.Kind.ToString()
        };

        // Right-click, VS-style "select at cursor" menu: lists every entity overlapping the
        // click point so you can pick exactly which one, rather than only ever getting
        // whichever is topmost. Complements Ctrl+Click (which passes through triggers to
        // whatever's underneath in one step) -- this covers picking between anything else
        // that overlaps too, trigger or not.
        private void ShowEntityContextMenu(Point location)
        {
            var hits = HitTestAll(location);
            if (hits.Count == 0) return;

            var menu = new ContextMenuStrip();
            foreach (int idx in hits)
            {
                var entity = _currentEntities[idx];
                menu.Items.Add(DescribeEntity(entity), null, (_, _) =>
                {
                    _selectedIndex = idx;
                    UpdateStatusLabel();
                    mapPictureBox.Invalidate();
                });
            }
            menu.Show(mapPictureBox, location);
        }

        private void mapPictureBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (_currentMapBitmap == null) return;

            if (e.Button == MouseButtons.Right)
            {
                ShowEntityContextMenu(e.Location);
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

            // Grabbing the currently-selected trigger's border takes priority over starting
            // a fresh selection/move -- check it before HitTest replaces _selectedIndex.
            if (_selectedIndex >= 0 && _selectedIndex < _currentEntities.Count)
            {
                var selected = _currentEntities[_selectedIndex];
                if (selected.Kind == EntityKind.Trigger)
                {
                    var handle = GetResizeHandleAt(selected, e.Location);
                    if (handle != ResizeHandle.None)
                    {
                        _resizing = true;
                        _resizeHandle = handle;
                        UpdateStatusLabel();
                        mapPictureBox.Invalidate();
                        return;
                    }
                }
            }

            // Ctrl+Click passes through triggers to whatever's underneath (a rock, an item,
            // etc) -- deliberately a momentary modifier instead of a persistent "hide this
            // trigger" toggle, since a hidden trigger with no obvious way to bring it back
            // is worse than just holding a key each time you need to reach under a big one
            // (e.g. Z1A1's whole-map trigger).
            _selectedIndex = HitTest(e.Location, skipTriggers: ModifierKeys.HasFlag(Keys.Control));

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
            if (_resizing && _selectedIndex >= 0)
            {
                var resizingEntity = _currentEntities[_selectedIndex];
                const int minSize = TileSize;

                int left = resizingEntity.X;
                int top = resizingEntity.Y;
                int right = resizingEntity.X + resizingEntity.Width;
                int bottom = resizingEntity.Y + resizingEntity.Height;

                if (_resizeHandle is ResizeHandle.Left or ResizeHandle.TopLeft or ResizeHandle.BottomLeft)
                    left = Math.Min(e.Location.X, right - minSize);
                if (_resizeHandle is ResizeHandle.Right or ResizeHandle.TopRight or ResizeHandle.BottomRight)
                    right = Math.Max(e.Location.X, left + minSize);
                if (_resizeHandle is ResizeHandle.Top or ResizeHandle.TopLeft or ResizeHandle.TopRight)
                    top = Math.Min(e.Location.Y, bottom - minSize);
                if (_resizeHandle is ResizeHandle.Bottom or ResizeHandle.BottomLeft or ResizeHandle.BottomRight)
                    bottom = Math.Max(e.Location.Y, top + minSize);

                int resizedX = left, resizedY = top, newWidth = right - left, newHeight = bottom - top;
                if (resizedX == resizingEntity.X && resizedY == resizingEntity.Y && newWidth == resizingEntity.Width && newHeight == resizingEntity.Height) return;

                _currentEntities[_selectedIndex] = resizingEntity with { X = resizedX, Y = resizedY, Width = newWidth, Height = newHeight };
                _dirty = true;

                UpdateStatusLabel();
                mapPictureBox.Invalidate();
                return;
            }

            if (!_dragging)
            {
                UpdateResizeCursor(e.Location);
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

        // Shows a resize cursor over the selected trigger's handle, default otherwise --
        // matches the "cursor changes on edges/corners" convention users expect from
        // resize handles in image/drawing editors.
        private void UpdateResizeCursor(Point location)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _currentEntities.Count)
            {
                var selected = _currentEntities[_selectedIndex];
                if (selected.Kind == EntityKind.Trigger)
                {
                    var handle = GetResizeHandleAt(selected, location);
                    if (handle != ResizeHandle.None)
                    {
                        mapPictureBox.Cursor = CursorFor(handle);
                        return;
                    }
                }
            }

            mapPictureBox.Cursor = Cursors.Default;
        }

        private void mapPictureBox_MouseUp(object? sender, MouseEventArgs e)
        {
            _dragging = false;
            _resizing = false;
            _resizeHandle = ResizeHandle.None;
        }

        private void toolStrip_ShowCollision_CheckedChanged(object sender, EventArgs e)
        {
            mapPictureBox.Invalidate();
        }

        private void UpdateStatusLabel()
        {
            UpdatePropertiesPanel();

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
                EntityKind.Decoration => $"decoration frame {entity.TypeId} ({entity.Width}x{entity.Height})",
                _ => $"type {entity.TypeId}"
            };

            string addressLabel = entity.SourceAddress == 0 ? "new, not yet saved" : $"0x{entity.SourceAddress:X}";
            string dirtyMarker = _dirty ? " [unsaved]" : "";
            statusLabel.Text = $"{entity.Kind} @ ({entity.X},{entity.Y}) — {detail} — {addressLabel}{dirtyMarker}{warning}";
        }

        // Plain-language entity details for the Properties panel. Deliberately never shows
        // raw script/handler bytecode -- just the concrete facts already confirmed from ROM
        // data (position, size, addresses, item/sprite ids). For triggers specifically: we
        // can say a trigger zone exists here and its dispatch (vtable) address, but NOT which
        // specific sub-type it is (save point / dialog / script / warp) -- there's no
        // confirmed mapping from vtable address to sub-type yet, so this says so plainly
        // rather than guessing a friendly name it can't back up.
        private void UpdatePropertiesPanel()
        {
            bool hasSelection = _selectedIndex >= 0 && _selectedIndex < _currentEntities.Count;
            propertiesGroupBox.Enabled = hasSelection;

            if (!hasSelection)
            {
                propertiesLabel.Text = "No selection.\r\n\r\nClick an entity on the map, or right-click for a menu when more than one overlaps.";
                return;
            }

            var entity = _currentEntities[_selectedIndex];
            var lines = new List<string>
            {
                $"Kind: {entity.Kind}",
                $"Position: ({entity.X}, {entity.Y})"
            };

            if (entity.Width > 0 || entity.Height > 0)
                lines.Add($"Size: {entity.Width} x {entity.Height}");

            lines.Add("");

            switch (entity.Kind)
            {
                case EntityKind.Trigger:
                    lines.Add("A trigger zone -- the game runs its handler when the player enters this rectangle.");
                    lines.Add($"Dispatch (vtable) address: 0x{entity.TypeId:X}");
                    lines.Add("Specific trigger type (save point / dialog / script / warp) isn't identified from ROM data alone yet.");
                    break;
                case EntityKind.Object:
                    lines.Add($"Item ID: {entity.TypeId} (g_ItemsInGame index)");
                    if (entity.OnPickup != 0) lines.Add($"OnPickup script: 0x{entity.OnPickup:X}");
                    if (entity.CollectionMsg != 0) lines.Add($"Collection message: 0x{entity.CollectionMsg:X}");
                    break;
                case EntityKind.Item:
                    lines.Add($"Item index: {entity.TypeId}");
                    lines.Add("Position is approximate -- derived from a decoration box, not stored directly.");
                    break;
                case EntityKind.Decoration:
                    lines.Add($"Sprite frame offset: {entity.TypeId}");
                    break;
                case EntityKind.LevelGate:
                    lines.Add($"Requires level: {entity.TypeId}");
                    break;
                case EntityKind.Character:
                    lines.Add($"Sprite ID: {entity.TypeId}");
                    break;
                case EntityKind.SpawnScript:
                    lines.Add("Spawn script entity.");
                    break;
            }

            lines.Add("");
            lines.Add(entity.SourceAddress == 0 ? "ROM address: new, not yet saved" : $"ROM address: 0x{entity.SourceAddress:X}");
            if (entity.PositionAddress != entity.SourceAddress)
                lines.Add($"Position write-back address: 0x{entity.PositionAddress:X}");

            propertiesLabel.Text = string.Join("\r\n", lines);
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
            PopulateObjectsList();
        }

        // Cross-map object browser -- scans every map's mapObjects[] up front (via
        // EntityReader.ReadObjectArray, the same reader the main view uses) so "where are
        // all the objects" has a real answer instead of only ever showing whatever's on
        // whichever single map happens to be open.
        private void PopulateObjectsList()
        {
            objectsListView.Items.Clear();
            if (_rom == null || _game == null) return;

            var rows = new List<ListViewItem>();
            foreach (var entry in _game.MapEntries)
            {
                foreach (var entity in EntityReader.ReadObjectArray(_rom, entry))
                {
                    rows.Add(new ListViewItem(new[]
                    {
                        entry.Zone.ToString(),
                        entry.Area.ToString(),
                        entry.Name,
                        entity.TypeId.ToString(),
                        entity.X.ToString(),
                        entity.Y.ToString(),
                    })
                    { Tag = (entry, entity) });
                }
            }

            objectsListView.Items.AddRange(rows.ToArray());
        }

        private void objectsListView_MouseDoubleClick(object? sender, MouseEventArgs e) => GoToSelectedObject();

        private void objectsGoToButton_Click(object? sender, EventArgs e) => GoToSelectedObject();

        // Jumps the main view to an existing object: selects its map's tree node (triggers
        // the normal mapTreeView_AfterSelect load), then finds and selects the matching
        // entity by SourceAddress in the freshly-loaded _currentEntities and scrolls to it.
        private void GoToSelectedObject()
        {
            if (objectsListView.SelectedItems.Count == 0) return;
            if (objectsListView.SelectedItems[0].Tag is not (MapEntry entry, Entity entity)) return;

            var node = FindMapNode(mapTreeView.Nodes, entry);
            if (node == null) return;

            mapTreeView.SelectedNode = node;

            int idx = _currentEntities.FindIndex(x => x.Kind == EntityKind.Object && x.SourceAddress == entity.SourceAddress);
            if (idx < 0) return;

            _selectedIndex = idx;
            UpdateStatusLabel();
            mapPictureBox.Invalidate();

            var target = _currentEntities[idx];
            mapScrollPanel.AutoScrollPosition = new Point(
                Math.Max(0, target.X - (mapScrollPanel.ClientSize.Width / 2)),
                Math.Max(0, target.Y - (mapScrollPanel.ClientSize.Height / 2)));
        }

        // Places another copy of the selected row's item type on the CURRENTLY open map --
        // same disambiguation flow as the Items tab (asks which onPickup/collectionMsg
        // script to reuse if this item id already has more than one distinct pair in the
        // ROM), since dropping a duplicate of something that already exists is exactly the
        // "multiple scripts" case that flow was built for.
        private void objectsPlaceNewButton_Click(object? sender, EventArgs e)
        {
            if (objectsListView.SelectedItems.Count == 0) return;
            if (objectsListView.SelectedItems[0].Tag is not (MapEntry, Entity entity)) return;

            ArmPlacementWithDisambiguation(entity.TypeId, objectsListView, objectsListView.PointToClient(Cursor.Position));
        }

        private static TreeNode? FindMapNode(TreeNodeCollection nodes, MapEntry entry)
        {
            foreach (TreeNode node in nodes)
            {
                if (ReferenceEquals(node.Tag, entry)) return node;
                var found = FindMapNode(node.Nodes, entry);
                if (found != null) return found;
            }
            return null;
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
                        placed += EntityWriter.PersistNewCharacters(editedRom, _currentEntry, mapEntryAddress, _currentEntities);
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
