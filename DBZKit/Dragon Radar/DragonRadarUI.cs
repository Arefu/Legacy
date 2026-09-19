using Bulma;
using DrGero;
using DrGero.IO;
using DrGero.Loader;
using DrGero.Rendering;
using DrGero.Types;
using DrGero.UI;
using System.Drawing.Drawing2D;
using static DrGero.Types.MapEntry;
using FlagUsageScanner_FlagUsage = DrGero.Quests.FlagUsageScanner.FlagUsage;

namespace Dragon_Radar
{
    public partial class DragonRadarUI : Form
    {
        private IGame? _game;
        private ROM? _rom;
        private List<DrGero.Quests.QuestEntry> _quests = new();
        private List<DrGero.Quests.FlagUsageScanner.FlagUsage> _flagUsages = new();

        // File offset of the currently-selected trigger's 4-byte script/payload
        // pointer (dataPtr+0x10 -- see ResolveTriggerScriptPayload), so
        // editScriptButton_Click knows exactly where to patch on save. Null when
        // nothing's selected, the selection isn't a trigger, or the payload doesn't
        // resolve to a real ROM pointer.
        private int? _selectedTriggerPayloadFileOffset;

        public DragonRadarUI()
        {
            InitializeComponent();

            // Fixed-size window: a bit bigger than the designer default, no maximize/resize -- and the
            // toolbar is locked in place (no drag grip).
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1700, 960);
            viewportToolStrip.GripStyle = ToolStripGripStyle.Hidden;

            mapPictureBox.Paint += mapPictureBox_Paint;
            mapPictureBox.MouseDown += mapPictureBox_MouseDown;
            mapPictureBox.MouseMove += mapPictureBox_MouseMove;
            mapPictureBox.MouseUp += mapPictureBox_MouseUp;
            listView1.SelectedIndexChanged += listView1_SelectedIndexChanged;
            KeyPreview = true;
            KeyDown += DragonRadarUI_KeyDown;

            mapTreeView.KeyDown += MapTreeView_KeyDown;
            BuildLevelGatePanel();
            BuildEditorTab();
        }

        // ---- Tile brush -------------------------------------------------------------------
        // Toggle "Paint tiles", pick a tile in the Tiles tab (it becomes the brush), then click
        // and HOLD on the map to paint it into the chosen BG layer. While painting is on, the
        // tile list's usual "highlight every use of this tile" is switched off -- those boxes
        // just get in the way of a brush. Edits go straight into the loaded ROM (see
        // TileLayerEditor); nothing reaches disk until Save ROM As.

        // These live in the sidebar's Editor tab (see BuildEditorTab), not the toolbar.
        private CheckBox _paintTilesButton = null!;
        private CheckBox _eraseButton = null!;
        private ComboBox _paintLayerCombo = null!;
        private ComboBox _brushSizeCombo = null!;
        private CheckBox _flipXButton = null!;
        private CheckBox _flipYButton = null!;
        private CheckBox _showGridCheck = null!;
        private CheckBox _snapGridCheck = null!;

        private bool _painting;                 // a click-and-hold stroke is in progress
        private int _brushTileId = -1;          // tileset slot id chosen in the Tiles tab
        private Dictionary<Range, Bitmap> _currentTilesets = new(); // for the live stroke preview
        // One editor per layer, kept across strokes so each chunk is only written to free space once.
        private readonly Dictionary<int, TileLayerEditor> _tileEditors = new();
        private int _tileEditorMapOffset = -1;

        // ---- Undo (tiles + collision) ----
        // A stroke = everything one click-and-hold changed (or one "block/clear tile uses").
        // Each edit remembers the cell's OLD value; Undo (Ctrl+Z / toolbar) puts them back. The
        // history is per map: it's cleared when you open another map. Tileset slots added for a
        // borrowed tile aren't removed on undo -- an unused extra slot is harmless.
        // Old = the cell's value before the edit, New = after (Undo restores Old, Redo re-applies New).
        private readonly record struct PaintEdit(bool IsCollision, int Layer, int X, int Y, int Old, int New);
        private List<PaintEdit> _currentStroke = new();
        private readonly List<List<PaintEdit>> _undoStack = new();
        private readonly List<List<PaintEdit>> _redoStack = new(); // strokes that were undone; a new stroke clears it
        private const int MaxUndoStrokes = 100;
        private Button _undoButton = null!;
        private Button _redoButton = null!;

        private void PushCurrentStroke()
        {
            if (_currentStroke.Count > 0)
            {
                _undoStack.Add(_currentStroke);
                if (_undoStack.Count > MaxUndoStrokes) _undoStack.RemoveAt(0);
                _redoStack.Clear(); // doing something new forks the history
            }
            _currentStroke = new();
            UpdateUndoButton();
        }

        private void UpdateUndoButton()
        {
            if (_undoButton != null)
            {
                _undoButton.Enabled = _undoStack.Count > 0;
                _undoButton.Text = _undoStack.Count > 0 ? $"Undo ({_undoStack.Count})" : "Undo";
            }
            if (_redoButton != null)
            {
                _redoButton.Enabled = _redoStack.Count > 0;
                _redoButton.Text = _redoStack.Count > 0 ? $"Redo ({_redoStack.Count})" : "Redo";
            }
        }

        // Re-applies the most recently undone stroke.
        private void RedoLastStroke()
        {
            if (_rom == null || _redoStack.Count == 0) return;

            var stroke = _redoStack[^1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            bool tilesTouched = false;
            foreach (var edit in stroke) // oldest first, the order they were originally made
            {
                if (edit.IsCollision)
                    SetCollisionAtWorld(edit.X, edit.Y, edit.New != 0, record: false);
                else if (GetTileEditor(edit.Layer) is { } editor && editor.SetTile(edit.X, edit.Y, (ushort)edit.New))
                    tilesTouched = true;
            }

            _undoStack.Add(stroke); // back on the undo stack (without clearing what's left to redo)
            if (tilesTouched) RerenderMapView();
            mapPictureBox.Invalidate();
            UpdateUndoButton();
            statusLabel.Text = $"Redid {stroke.Count} cell change(s).";
        }

        private void UndoLastStroke()
        {
            if (_rom == null || _undoStack.Count == 0) return;

            var stroke = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Add(stroke); // so it can be redone

            bool tilesTouched = false;
            for (int i = stroke.Count - 1; i >= 0; i--) // newest first, so overlapping edits unwind correctly
            {
                var edit = stroke[i];
                if (edit.IsCollision)
                    SetCollisionAtWorld(edit.X, edit.Y, edit.Old != 0, record: false);
                else if (GetTileEditor(edit.Layer) is { } editor && editor.SetTile(edit.X, edit.Y, (ushort)edit.Old))
                    tilesTouched = true;
            }

            if (tilesTouched) RerenderMapView(); // layer priority/blending, so the picture matches the ROM again
            mapPictureBox.Invalidate();
            UpdateUndoButton();
            statusLabel.Text = $"Undid {stroke.Count} cell change(s).";
        }
        private HashSet<Range> _currentUsedTilesets = new();
        private Point? _lastPaintPoint;         // previous stroke sample, so fast mouse moves leave no gaps

        // "Tiles from map #": browse another map's tileset and paint with its tiles. A tile id in a
        // layer is a SLOT in that map's own tileset, so a tile from another map is first resolved
        // to its atlas tile (source map's slot table) and then to a slot in THIS map's table,
        // adding one if needed (TilesetTable.EnsureAtlasTile).
        private ComboBox _tileSourceCombo = null!; // every map by Zone/Area; the item index is the map index
        private bool _suppressTileSourceEvents;
        private int _tileSourceMap = -1;                   // map index whose tiles the list shows (-1 = this map)
        private int[]? _sourceAtlasIndices;                // slot -> atlas tile for that other map; null = this map
        private readonly Dictionary<int, int> _atlasToSlot = new(); // atlas tile -> slot in THIS map, per map load

        // ---- Collision brush ---------------------------------------------------------------
        // "Paint collision" paints solid (Block) or clear (Clear) 8x8 cells with the same click-and-
        // hold stroke and brush size as tiles. "Tiles also block" makes the TILE brush set collision
        // under whatever it paints (and clear it when erasing); "Block/Clear tile uses" applies it
        // to every cell the selected tile appears in. Edits go straight into the loaded ROM.
        private CheckBox _paintCollisionButton = null!;
        private ComboBox _collisionModeCombo = null!;
        private CheckBox _tileAlsoCollidesButton = null!;
        private CollisionEditor? _collisionEditor;

        // Layer visibility ("Layers" menu) and the isolate-while-painting toggle -- see BuildRenderOptions.
        private readonly CheckBox[] _layerItems = new CheckBox[4];
        private CheckBox _isolateLayerButton = null!;
        private bool _suppressLayerEvents;

        private CollisionEditor? GetCollisionEditor()
        {
            if (_rom == null || _currentEntry == null) return null;
            if (_collisionEditor != null && ReferenceEquals(_collisionEditor.Rom, _rom) && _collisionEditor.MapOffset == _currentMapOffset)
                return _collisionEditor;

            _collisionEditor = CollisionEditor.Open(_rom, _currentMapOffset);
            return _collisionEditor;
        }

        // Sets collision for the 8x8 cell containing a world pixel, keeping the displayed grid in step.
        // `allowed` = the cell's collision bit: true (red on the overlay) = the player IS allowed to
        // walk there, false = barred. (The toolbar's Block/Clear labels are left as they were.)
        private bool SetCollisionAtWorld(int worldX, int worldY, bool allowed, bool record = true)
        {
            if (worldX < 0 || worldY < 0) return false;
            var editor = GetCollisionEditor();
            if (editor == null) return false;

            int tx = worldX / TileSize, ty = worldY / TileSize;
            bool was = editor.IsAllowed(tx, ty);
            if (!editor.SetAllowed(tx, ty, allowed)) return false;
            if (record && was != allowed) _currentStroke.Add(new PaintEdit(true, 0, worldX, worldY, was ? 1 : 0, allowed ? 1 : 0));
            if (_collisionGrid != null && tx < _collisionGrid.GetLength(0) && ty < _collisionGrid.GetLength(1))
                _collisionGrid[tx, ty] = allowed;
            return true;
        }

        private void PaintCollisionAt(Point p)
        {
            if (_rom == null || _currentMapBitmap == null) return;
            if (GetCollisionEditor() == null)
            {
                statusLabel.Text = "This map's collision data couldn't be read, so it can't be edited.";
                return;
            }

            bool block = _collisionModeCombo.SelectedIndex == 0;
            int size = _brushSizeCombo.SelectedIndex + 1;
            var origin = BrushOrigin(p, size, 0, 0); // collision is in plain world tiles, no layer offset
            var dirty = Rectangle.Empty;

            for (int dy = 0; dy < size; dy++)
            {
                for (int dx = 0; dx < size; dx++)
                {
                    int wx = origin.X + (dx * TileSize), wy = origin.Y + (dy * TileSize);
                    if (!SetCollisionAtWorld(wx, wy, block)) continue;

                    var cell = new Rectangle((wx / TileSize) * TileSize, (wy / TileSize) * TileSize, TileSize, TileSize);
                    dirty = dirty.IsEmpty ? cell : Rectangle.Union(dirty, cell);
                }
            }

            if (!dirty.IsEmpty)
            {
                _dirty = true;
                mapPictureBox.Invalidate(dirty);
            }
        }

        // Applies Block/Clear to every cell the brush tile is used in on this map.
        private void SetCollisionForTileUses(bool block)
        {
            if (_rom == null) return;
            if (_sourceAtlasIndices != null || _brushTileId < 0)
            {
                statusLabel.Text = "Pick a tile from THIS map's own tile list first (set Tiles from map # to this map).";
                return;
            }
            if (!_tileUsageIndex.TryGetValue(_brushTileId, out var uses) || uses.Count == 0)
            {
                statusLabel.Text = $"Tile {_brushTileId} isn't used anywhere on this map.";
                return;
            }

            int changed = 0;
            _currentStroke = new();
            foreach (var pt in uses)
                if (SetCollisionAtWorld(pt.X, pt.Y, block)) changed++;
            PushCurrentStroke(); // the whole batch is one undo step

            _dirty = true;
            toolStrip_ShowCollision.Checked = true;
            mapPictureBox.Invalidate();
            statusLabel.Text = block
                ? $"Painted collision on {changed} cell(s) where tile {_brushTileId} is used -- those cells are now ALLOWED space."
                : $"Removed collision from {changed} cell(s) where tile {_brushTileId} is used -- those cells are now barred.";
        }

        private bool _suppressVariantEvents;

        // Fills the toolbar's Variant box with every variant of the open map's Zone/Area.
        private void SyncVariantBox()
        {
            if (_game == null || _currentEntry == null) return;
            _suppressVariantEvents = true;
            toolStrip_VariantCombo.Items.Clear();
            var siblings = _game.MapEntries
                .Where(e => e.Zone == _currentEntry.Zone && e.Area == _currentEntry.Area)
                .OrderBy(e => e.Variation).ToList();
            foreach (var e in siblings)
                toolStrip_VariantCombo.Items.Add(new VariantItem(e));
            toolStrip_VariantCombo.SelectedIndex = siblings.FindIndex(e => ReferenceEquals(e, _currentEntry));
            toolStrip_VariantCombo.Enabled = siblings.Count > 1;
            _suppressVariantEvents = false;
        }

        private void toolStrip_VariantCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressVariantEvents || toolStrip_VariantCombo.SelectedItem is not VariantItem item) return;
            if (ReferenceEquals(item.Entry, _currentEntry)) return;
            var node = FindMapNode(mapTreeView.Nodes, item.Entry);
            if (node == null) return;
            mapTreeView.SelectedNode = node; // AfterSelect loads the variant
            node.EnsureVisible();
        }

        private sealed record VariantItem(MapEntry Entry)
        {
            public override string ToString() => $"Variation {Entry.Variation}";
        }

        private int CurrentMapIndex()
        {
            if (_currentEntry == null || _game == null) return -1;
            for (int i = 0; i < _game.MapEntries.Count; i++)
                if (ReferenceEquals(_game.MapEntries[i], _currentEntry)) return i;
            return -1;
        }

        // Keeps the box in step with the map that was just opened (without re-triggering a load).
        private void SyncTileSourceBox()
        {
            if (_tileSourceCombo == null || _game == null) return;
            _suppressTileSourceEvents = true;

            // Fill the list once per ROM: "Z1 A2 - Pepper Town" (+ " v2" for a map variation).
            if (_tileSourceCombo.Items.Count != _game.MapEntries.Count)
            {
                _tileSourceCombo.BeginUpdate();
                _tileSourceCombo.Items.Clear();
                foreach (var entry in _game.MapEntries)
                    _tileSourceCombo.Items.Add(MapLabelFor(entry));
                _tileSourceCombo.EndUpdate();
            }

            int current = CurrentMapIndex();
            _tileSourceCombo.SelectedIndex = current < 0 ? -1 : current;
            _tileSourceMap = -1;
            _suppressTileSourceEvents = false;
        }

        private static string MapLabelFor(MapEntry entry)
        {
            string label = $"Z{entry.Zone} A{entry.Area}";
            if (entry.Variation > 1) label += $" v{entry.Variation}";
            return string.IsNullOrWhiteSpace(entry.Name) ? label : $"{label} - {entry.Name}";
        }

        private void TileSourceCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressTileSourceEvents || _rom == null || _game == null) return;

            int index = _tileSourceCombo.SelectedIndex;
            if (index < 0 || index >= _game.MapEntries.Count) return;
            if (index == CurrentMapIndex())
            {
                // Back to this map: its own list of tiles in use.
                _tileSourceMap = -1;
                _sourceAtlasIndices = null;
                PopulateTileList(_currentTilesets, _currentUsedTilesets);
                statusLabel.Text = "Tile list: this map's own tiles.";
                return;
            }

            var entry = _game.MapEntries[index];
            _rom.PushPosition(entry.VariationArray);
            int otherMapOffset = _rom.ReadPointer();
            _rom.PopPosition();

            try
            {
                var all = MapRenderer.DrawTileset(_rom, _game.Config, otherMapOffset);
                // Only the static tileset (slots from 0) can be borrowed: animated sequences live in
                // per-map VRAM ranges and aren't atlas tiles you can drop into another map.
                var staticOnly = all.Where(kv => kv.Key.Start.Value == 0).ToDictionary(kv => kv.Key, kv => kv.Value);
                _sourceAtlasIndices = TilesetTable.ReadAtlasIndices(_rom, otherMapOffset);
                _tileSourceMap = index;
                PopulateTileList(staticOnly, new HashSet<Range>(staticOnly.Keys));
                statusLabel.Text = $"Tile list: borrowing tiles from {MapLabelFor(entry)}. Pick one and paint -- it's added to this map's tileset as needed.";
            }
            catch (Exception ex)
            {
                _sourceAtlasIndices = null;
                _tileSourceMap = -1;
                statusLabel.Text = $"Couldn't read the tileset for {MapLabelFor(entry)}: {ex.Message}";
            }
        }

        // The tileset slot to paint with on THIS map for the current brush: the brush itself when
        // the tile list is this map's own, otherwise the (found or freshly added) slot for the same
        // atlas tile. -1 = no brush / can't be placed (status says why).
        private int ResolveBrushSlot()
        {
            if (_brushTileId < 0 || _rom == null || _game == null) return -1;
            if (_sourceAtlasIndices == null) return _brushTileId;
            if (_brushTileId >= _sourceAtlasIndices.Length) return -1;

            int atlas = _sourceAtlasIndices[_brushTileId];
            if (_atlasToSlot.TryGetValue(atlas, out int known)) return known;

            int slot = TilesetTable.EnsureAtlasTile(_rom, _currentMapOffset, atlas);
            if (slot < 0)
            {
                statusLabel.Text = "This map's tileset can't take another tile (it's full, or the next slot is used by an animated tile).";
                return -1;
            }

            _atlasToSlot[atlas] = slot;
            _currentTilesets = MapRenderer.DrawTileset(_rom, _game.Config, _currentMapOffset); // now includes the new slot
            _dirty = true;
            return slot;
        }

        // A stroke sample: paint at the point, filling in along the line from the previous sample
        // so a quick drag doesn't skip cells.
        private void PaintStrokeTo(Point p)
        {
            void Paint(Point at)
            {
                if (_paintCollisionButton.Checked) PaintCollisionAt(at);
                else PaintTilesAt(at);
            }

            if (_lastPaintPoint is { } from)
            {
                int dx = p.X - from.X, dy = p.Y - from.Y;
                int steps = Math.Max(Math.Abs(dx), Math.Abs(dy)) / (TileSize / 2);
                for (int i = 1; i < steps; i++)
                    Paint(new Point(from.X + (dx * i / steps), from.Y + (dy * i / steps)));
            }
            Paint(p);
            _lastPaintPoint = p;
        }

        // Builds the sidebar's "Editor" tab: tile brush, collision brush, view options (layers, grid,
        // snap) and undo. They used to be toolbar items, which got crowded; the toolbar keeps its
        // original buttons. Everything here is an ordinary control so it can sit in a scrollable
        // column, and the fields keep their names so the painting code below didn't need to change.
        private void BuildEditorTab()
        {
            var tips = new ToolTip { AutoPopDelay = 12000 };

            CheckBox Check(string text, string tip, bool isChecked = false)
            {
                var c = new CheckBox { Text = text, AutoSize = true, Checked = isChecked, Margin = new Padding(3, 2, 3, 2) };
                tips.SetToolTip(c, tip);
                return c;
            }
            ComboBox Combo(int width, string tip, params object[] items)
            {
                var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };
                c.Items.AddRange(items);
                c.SelectedIndex = 0;
                tips.SetToolTip(c, tip);
                return c;
            }
            Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 6, 3, 0) };
            FlowLayoutPanel Row(params Control[] controls)
            {
                var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
                row.Controls.AddRange(controls);
                return row;
            }
            GroupBox Group(string title, params Control[] rows)
            {
                var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
                flow.Controls.AddRange(rows);
                var box = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Width = 240, Padding = new Padding(6, 4, 6, 6) };
                box.Controls.Add(flow);
                return box;
            }

            // ---- tiles ----
            _paintTilesButton = Check("Paint tiles", "Click and hold on the map to paint the tile selected in the Tiles tab");
            _paintLayerCombo = Combo(120, "Which background layer to paint into", "BG0", "BG1", "BG2", "BG3");
            _brushSizeCombo = Combo(60, "Brush size in tiles", "1x1", "2x2", "3x3", "4x4");
            _flipXButton = Check("Flip X", "Paint the tile mirrored left-right");
            _flipYButton = Check("Flip Y", "Paint the tile mirrored top-bottom");
            _eraseButton = Check("Erase", "Paint blank (tile 0) instead of the selected tile");
            _tileSourceCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 215, DropDownWidth = 320, MaxDropDownItems = 20 };
            _tileSourceCombo.SelectedIndexChanged += TileSourceCombo_SelectedIndexChanged;
            tips.SetToolTip(_tileSourceCombo, "Pick any map to browse its tileset in the Tiles tab and paint with its tiles. Pick this map again to go back to its own tiles.");

            // ---- collision ----
            _paintCollisionButton = Check("Paint collision", "Click and hold on the map to paint collision. Red cells are where you ARE allowed to walk; anything not red is barred.");
            _collisionModeCombo = Combo(70, "Block = paint collision (red = ALLOWED space you can walk on). Clear = remove it (that cell becomes barred).", "Block", "Clear");
            _tileAlsoCollidesButton = Check("Tiles also block", "Painting a tile also paints collision under it (red = ALLOWED space); erasing removes it, making the cell barred");
            // These act on the tile selected in the palette, so they live right under it (see the assembly below).
            var blockUses = new Button { Text = "Paint collision where used", AutoSize = true };
            var clearUses = new Button { Text = "Clear collision where used", AutoSize = true };
            tips.SetToolTip(blockUses, "Paint collision on every cell of this map that uses the selected tile (red = ALLOWED space you can walk on)");
            tips.SetToolTip(clearUses, "Remove collision from every cell of this map that uses the selected tile (those cells become barred)");
            _selectedTileInfo = new Label { AutoSize = false, Dock = DockStyle.Top, Height = 34, Text = "No tile selected.", Padding = new Padding(2, 4, 2, 0) };
            blockUses.Click += (_, _) => SetCollisionForTileUses(block: true);
            clearUses.Click += (_, _) => SetCollisionForTileUses(block: false);

            // ---- view ----
            var layerBoxes = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Width = 220, Margin = new Padding(0) };
            for (int i = 0; i < 4; i++)
            {
                var box = Check($"BG{i}", $"Show or hide background layer {i}", isChecked: true);
                box.CheckedChanged += (_, _) => { if (!_suppressLayerEvents) RerenderMapView(); };
                _layerItems[i] = box;
                layerBoxes.Controls.Add(box);
            }
            _isolateLayerButton = Check("Only edited layer", "While painting tiles, show only the layer being painted", isChecked: true);
            _showGridCheck = Check("Show grid", "Draw the 8x8 tile grid over the map (brighter lines mark 256-pixel chunk edges)");
            _snapGridCheck = Check("Snap to grid", "Tile brush: snap to a grid of brush-sized blocks so strokes line up. Dragging things on the map snaps to 8-pixel tiles.");
            _showGridCheck.CheckedChanged += (_, _) => mapPictureBox.Invalidate();

            // ---- undo ----
            _undoButton = new Button { Text = "Undo", AutoSize = true, Enabled = false };
            tips.SetToolTip(_undoButton, "Undo the last tile/collision stroke (Ctrl+Z)");
            _undoButton.Click += (_, _) => UndoLastStroke();
            _redoButton = new Button { Text = "Redo", AutoSize = true, Enabled = false };
            tips.SetToolTip(_redoButton, "Redo the stroke you just undid (Ctrl+Y or Ctrl+Shift+Z)");
            _redoButton.Click += (_, _) => RedoLastStroke();

            // ---- behaviour ----
            // Tile and collision painting are exclusive modes; collision painting also needs the overlay visible.
            _paintCollisionButton.CheckedChanged += (_, _) =>
            {
                if (_paintCollisionButton.Checked)
                {
                    _paintTilesButton.Checked = false;
                    toolStrip_ShowCollision.Checked = true;
                }
                UpdateStatusLabel();
            };
            _paintTilesButton.CheckedChanged += (_, _) =>
            {
                if (_paintTilesButton.Checked) _paintCollisionButton.Checked = false;
                // Paint mode owns the tile list selection: no highlight boxes while it's on.
                if (_paintTilesButton.Checked) _highlightedTileId = null;
                else RefreshTileHighlightFromList();
                if (_isolateLayerButton.Checked) RerenderMapView(); // isolating depends on paint mode
                UpdateStatusLabel();
                mapPictureBox.Invalidate();
            };
            _paintLayerCombo.SelectedIndexChanged += (_, _) =>
            {
                if (!_suppressLayerEvents && _isolateLayerButton.Checked && _paintTilesButton.Checked) RerenderMapView();
            };
            _isolateLayerButton.CheckedChanged += (_, _) => { if (_paintTilesButton.Checked) RerenderMapView(); };

            // ---- assemble the tab ----
            var mapProps = new Button { Text = "Map properties...", AutoSize = true };
            tips.SetToolTip(mapProps, "Change this map's width and height");
            mapProps.Click += (_, _) => ShowMapProperties();

            // Layout, top to bottom, so painting never needs a tab switch:
            //   brush controls  ->  the TILE PALETTE (fills the spare height)  ->  collision / view / history.
            // The palette is the old Tiles tab's list, moved here.
            var tilesGroup = Group("Tiles",
                _paintTilesButton,
                Row(Caption("Layer"), _paintLayerCombo, Caption("Brush"), _brushSizeCombo),
                Row(_flipXButton, _flipYButton, _eraseButton),
                Caption("Tiles from map (Zone / Area)"),
                _tileSourceCombo);

            tilesTabPage.Controls.Remove(listView1);
            listView1.Dock = DockStyle.Fill;
            var uses = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            uses.Controls.AddRange(new Control[] { blockUses, clearUses });
            var palette = new GroupBox { Text = "Tile palette -- click a tile to use it as the brush", Dock = DockStyle.Fill, Padding = new Padding(6, 4, 6, 6), MinimumSize = new Size(0, 140) };
            palette.Controls.Add(listView1);      // Fill first, then the edge-docked pieces
            palette.Controls.Add(uses);
            palette.Controls.Add(_selectedTileInfo);

            var lower = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            lower.Controls.Add(Group("Collision (red = allowed to walk)",
                _paintCollisionButton,
                Row(Caption("Mode"), _collisionModeCombo),
                _tileAlsoCollidesButton));
            lower.Controls.Add(Group("View",
                layerBoxes,
                Row(_isolateLayerButton),
                Row(_showGridCheck, _snapGridCheck)));
            lower.Controls.Add(Group("History / Map", Row(_undoButton, _redoButton, mapProps)));

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(4) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(tilesGroup, 0, 0);
            layout.Controls.Add(palette, 0, 1);
            layout.Controls.Add(lower, 0, 2);

            var tab = new TabPage("Editor") { Padding = new Padding(0) };
            tab.Controls.Add(layout);
            sidebarTabControl.TabPages.Add(tab);
            sidebarTabControl.TabPages.Remove(tilesTabPage); // its list now lives in the Editor tab
            sidebarTabControl.SelectedTab = tab;
            sidebarTabControl.Width = 350;                    // room for the palette + controls side by side
        }

        private Label _selectedTileInfo = null!;

        // What the palette's selected tile is, and how much of this map uses it -- shown right above the
        // "collision where used" buttons that act on it.
        private void UpdateSelectedTileInfo()
        {
            if (_selectedTileInfo == null) return;
            if (_brushTileId < 0) { _selectedTileInfo.Text = "No tile selected. Click one below."; return; }
            if (_sourceAtlasIndices != null)
            {
                _selectedTileInfo.Text = $"Tile {_brushTileId} from another map. The collision buttons work on this map's own tiles.";
                return;
            }
            int uses = _tileUsageIndex.TryGetValue(_brushTileId, out var list) ? list.Count : 0;
            _selectedTileInfo.Text = $"Selected tile {_brushTileId}: used {uses} time(s) on this map. The buttons below apply to every one of them.";
        }

        // Map properties: the map's pixel size (see MapResizer for how the stored fields map to it).
        // Growing it can also add empty chunks to the layers so the new area can be painted; both
        // changes go straight into the loaded ROM.
        private void ShowMapProperties()
        {
            if (_rom == null || _currentEntry == null || _currentMapBitmap == null)
            {
                MessageBox.Show("Open a map first.", "Map properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var (width, height) = MapResizer.ReadSize(_rom, _currentMapOffset);
            var (layerW, layerH) = MapResizer.LayerCoverage(_rom, _currentMapOffset);

            using var form = new Form
            {
                Text = $"Map properties -- Zone {_currentEntry.Zone} Area {_currentEntry.Area}",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize = new Size(400, 230),
                ShowIcon = false
            };

            var widthBox = new NumericUpDown { Minimum = MapResizer.ScreenWidth, Maximum = MapResizer.MaxSize, Value = Math.Clamp(width, MapResizer.ScreenWidth, MapResizer.MaxSize), Width = 90, Location = new Point(110, 18) };
            var heightBox = new NumericUpDown { Minimum = MapResizer.ScreenHeight, Maximum = MapResizer.MaxSize, Value = Math.Clamp(height, MapResizer.ScreenHeight, MapResizer.MaxSize), Width = 90, Location = new Point(110, 50) };
            var grow = new CheckBox { Text = "Also add empty layer chunks so the new area can be painted", Checked = true, AutoSize = false, Size = new Size(370, 36), Location = new Point(16, 120) };
            var info = new Label
            {
                AutoSize = false,
                Size = new Size(370, 40),
                Location = new Point(16, 78),
                Text = $"Currently {width} x {height} pixels. The background layers cover {layerW} x {layerH}. Anything past the layers is empty until you grow them and paint it."
            };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(220, 185), Width = 80 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(310, 185), Width = 80 };
            form.Controls.AddRange(new Control[]
            {
                new Label { Text = "Width (pixels)", AutoSize = true, Location = new Point(16, 21) }, widthBox,
                new Label { Text = "Height (pixels)", AutoSize = true, Location = new Point(16, 53) }, heightBox,
                info, grow, ok, cancel
            });
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            if (form.ShowDialog(this) != DialogResult.OK) return;

            int newWidth = (int)widthBox.Value, newHeight = (int)heightBox.Value;
            if (newWidth == width && newHeight == height && !grow.Checked) return;

            string? error = MapResizer.Resize(_rom, _currentMapOffset, newWidth, newHeight, grow.Checked, out int layersGrown);
            if (error != null)
            {
                MessageBox.Show(error, "Map properties", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _tileEditors.Clear(); // a grown layer is a new struct -- drop the cached editors for the old one
            _dirty = true;
            RerenderMapView();
            RefreshPaintLayerChoices();
            statusLabel.Text = $"Map is now {newWidth} x {newHeight}" + (layersGrown > 0 ? $"; {layersGrown} layer(s) extended." : ".");
        }

        private static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);

        // The layer scroll offsets the grid should line up with: the layer being painted while
        // painting tiles, else 0 (collision and entities use plain world pixels).
        private (int X, int Y) GridOffsets()
        {
            if (_paintTilesButton.Checked && GetTileEditor() is { } editor) return (editor.OffX, editor.OffY);
            return (0, 0);
        }

        // Top-left world pixel of a brush of `size` cells around p. Normally it's centred on the
        // cursor's cell; with "Snap to grid" it snaps to a grid of brush-sized blocks instead, so
        // 2x2/3x3/4x4 strokes tile together without overlapping or leaving gaps.
        private Point BrushOrigin(Point p, int size, int offX, int offY)
        {
            int cx = FloorDiv(p.X + offX, TileSize), cy = FloorDiv(p.Y + offY, TileSize);
            if (_snapGridCheck.Checked)
            {
                cx = FloorDiv(cx, size) * size;
                cy = FloorDiv(cy, size) * size;
            }
            else
            {
                int first = -((size - 1) / 2); // 2x2 extends right/down of the cursor cell, 3x3 is centred
                cx += first;
                cy += first;
            }
            return new Point((cx * TileSize) - offX, (cy * TileSize) - offY);
        }

        // 8x8 tile grid over the repainted area; brighter lines every 256 px, where the map's chunks meet.
        private void DrawGrid(Graphics g, Rectangle clip)
        {
            if (_currentMapBitmap == null) return;
            clip = Rectangle.Intersect(clip, new Rectangle(0, 0, _currentMapBitmap.Width, _currentMapBitmap.Height));
            if (clip.Width <= 0 || clip.Height <= 0) return;

            var (offX, offY) = GridOffsets();
            const int chunk = 256;
            using var thin = new Pen(Color.FromArgb(70, 255, 255, 255));
            using var thick = new Pen(Color.FromArgb(170, 255, 220, 0));

            int startX = clip.Left - ((((clip.Left + offX) % TileSize) + TileSize) % TileSize);
            for (int x = startX; x <= clip.Right; x += TileSize)
                g.DrawLine(((x + offX) % chunk) == 0 ? thick : thin, x, clip.Top, x, clip.Bottom);

            int startY = clip.Top - ((((clip.Top + offY) % TileSize) + TileSize) % TileSize);
            for (int y = startY; y <= clip.Bottom; y += TileSize)
                g.DrawLine(((y + offY) % chunk) == 0 ? thick : thin, clip.Left, y, clip.Right, y);
        }

        private void RefreshTileHighlightFromList()
        {
            _highlightedTileId = listView1.SelectedItems.Count > 0 && listView1.SelectedItems[0].Tag is int tileId ? tileId : null;
        }

        // The editor for a layer (default: the one picked in the toolbar). Reused while it's still for
        // the same ROM object and map; a different ROM/map starts fresh.
        private TileLayerEditor? GetTileEditor(int? layerOverride = null)
        {
            if (_rom == null || _currentEntry == null) return null;
            int layer = layerOverride ?? _paintLayerCombo.SelectedIndex;
            if (layer < 0) return null;

            if (_tileEditorMapOffset != _currentMapOffset) { _tileEditors.Clear(); _tileEditorMapOffset = _currentMapOffset; }
            if (_tileEditors.TryGetValue(layer, out var existing))
            {
                if (ReferenceEquals(existing.Rom, _rom)) return existing;
                _tileEditors.Clear(); // the ROM object was replaced (e.g. a reload) -- drop stale chunk caches
            }

            _rom.PushPosition(_currentMapOffset + 0x14 + (layer * 4));
            int layerAddress = _rom.ReadPointer();
            _rom.PopPosition();

            var opened = TileLayerEditor.Open(_rom, layerAddress);
            if (opened != null) _tileEditors[layer] = opened;
            return opened;
        }

        // Paints the brush around a map point: N x N cells centred on it, into the chosen layer,
        // and draws them straight onto the displayed bitmap so the stroke shows up live (the full
        // layered/blended re-render happens once when the mouse is released).
        private void PaintTilesAt(Point p)
        {
            if (_rom == null || _currentMapBitmap == null) return;

            var editor = GetTileEditor();
            if (editor == null)
            {
                statusLabel.Text = $"{_paintLayerCombo.SelectedItem} isn't an editable layer on this map (empty, or a format that isn't supported yet).";
                return;
            }

            bool erase = _eraseButton.Checked;
            int tileId = erase ? 0 : ResolveBrushSlot();
            if (tileId < 0)
            {
                if (!erase && _brushTileId < 0) statusLabel.Text = "Pick a tile in the Tiles tab first -- it becomes the brush.";
                return;
            }

            bool flipX = !erase && _flipXButton.Checked, flipY = !erase && _flipYButton.Checked;
            ushort entry = (ushort)((tileId & 0x3FF) | (flipX ? 1 << 10 : 0) | (flipY ? 1 << 11 : 0));

            int size = _brushSizeCombo.SelectedIndex + 1;
            var origin = BrushOrigin(p, size, editor.OffX, editor.OffY);

            using var g = Graphics.FromImage(_currentMapBitmap);
            var dirty = Rectangle.Empty;

            for (int dy = 0; dy < size; dy++)
            {
                for (int dx = 0; dx < size; dx++)
                {
                    int wx = origin.X + (dx * TileSize), wy = origin.Y + (dy * TileSize);
                    ushort? before = editor.GetTile(wx, wy);
                    if (!editor.SetTile(wx, wy, entry)) continue;
                    if (before.HasValue && before.Value != entry)
                        _currentStroke.Add(new PaintEdit(false, _paintLayerCombo.SelectedIndex, wx, wy, before.Value, entry));

                    var cell = editor.CellOrigin(wx, wy);
                    if (_tileAlsoCollidesButton.Checked) SetCollisionAtWorld(cell.X, cell.Y, allowed: !erase);
                    if (erase)
                    {
                        // Blank tile: what shows through depends on the layers beneath, which the
                        // full re-render on mouse-up works out; mark the cell so it's repainted.
                    }
                    else
                    {
                        MapRenderer.DrawTileInto(g, _currentTilesets, tileId, flipX, flipY, cell.X, cell.Y);
                    }
                    dirty = dirty.IsEmpty ? new Rectangle(cell, new Size(TileSize, TileSize)) : Rectangle.Union(dirty, new Rectangle(cell, new Size(TileSize, TileSize)));
                }
            }

            if (!dirty.IsEmpty)
            {
                _dirty = true;
                mapPictureBox.Invalidate(dirty);
            }
        }

        // End of a stroke: rebuild the map picture properly (layer priority, blending, the tile
        // list and usage index) from the ROM, without touching the entity list -- a plain
        // map reload would drop anything placed this session that hasn't been saved yet.
        private void FinishTileStroke()
        {
            _painting = false;
            PushCurrentStroke(); // this stroke becomes one undo step
            if (_rom == null || _game == null || _currentEntry == null) return;

            // A collision stroke already updated the displayed grid cell by cell -- nothing to re-render.
            if (_paintCollisionButton.Checked)
            {
                mapPictureBox.Invalidate();
                UpdateStatusLabel();
                return;
            }

            RerenderMapView();
            UpdateStatusLabel();
        }

        // Which background layers to draw. Normally the "Layers" menu's checkboxes; while painting
        // tiles with "Show only edited layer" on, JUST the layer being painted -- so what you see is
        // exactly what you're editing, with nothing above it hiding your strokes.
        private MapRenderOptions BuildRenderOptions()
        {
            bool isolate = _isolateLayerButton is { Checked: true } && _paintTilesButton is { Checked: true };
            int edited = _paintLayerCombo?.SelectedIndex ?? -1;
            bool Show(int i) => isolate ? i == edited : (_layerItems[i]?.Checked ?? true);
            return new MapRenderOptions { ShowBG0 = Show(0), ShowBG1 = Show(1), ShowBG2 = Show(2), ShowBG3 = Show(3) };
        }

        // Re-renders the map picture from the ROM (layer priority, blending, layer visibility) plus
        // the tile usage index, WITHOUT touching the entity list -- a full map reload would drop
        // anything placed this session that hasn't been saved yet.
        private void RerenderMapView()
        {
            if (_rom == null || _game == null || _currentEntry == null || _currentMapBitmap == null) return;

            MapRenderer.ResetCache(_game.Config);
            var (bitmap, tilesets, _, hasUnsupportedLayer, collisionGrid) =
                MapRenderer.RenderMap(_rom, _game.Config, _currentMapOffset, BuildRenderOptions());

            _currentMapBitmap?.Dispose();
            _currentMapBitmap = bitmap;
            mapPictureBox.Size = bitmap.Size; // a resized map changes the picture's size
            _currentTilesets = tilesets;
            _hasUnsupportedLayer = hasUnsupportedLayer;
            _collisionGrid = collisionGrid;
            _tileUsageIndex = MapRenderer.BuildTileUsageIndex(_rom, _currentMapOffset);
            mapPictureBox.Invalidate();
            UpdateSelectedTileInfo(); // the use count for the selected tile may have changed
        }

        // Fills the layer picker for the map just opened: which of BG0-3 can actually be painted
        // (empty layers and the unsupported 0x4FCD format can't), and defaults to the first that can.
        // Painting into a layer that isn't editable used to fail silently -- now it's labelled.
        private void RefreshPaintLayerChoices()
        {
            if (_paintLayerCombo == null || _rom == null || _currentEntry == null) return;

            var labels = new string[4];
            int firstEditable = -1;
            var editable = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                _rom.PushPosition(_currentMapOffset + 0x14 + (i * 4));
                int address = _rom.ReadPointer();
                _rom.PopPosition();

                editable[i] = TileLayerEditor.Open(_rom, address) != null;
                labels[i] = editable[i] ? $"BG{i}" : (address == 0 ? $"BG{i} (empty)" : $"BG{i} (unsupported)");
                if (editable[i] && firstEditable < 0) firstEditable = i;
            }

            int keep = _paintLayerCombo.SelectedIndex;
            _suppressLayerEvents = true;
            _paintLayerCombo.Items.Clear();
            _paintLayerCombo.Items.AddRange(labels);
            _paintLayerCombo.SelectedIndex = keep >= 0 && editable[keep] ? keep : Math.Max(0, firstEditable);
            _suppressLayerEvents = false;
        }

        // Up/Down in the map tree step from map to map, straight across the Zone/Area header
        // rows. Those headers aren't maps and can't be selected (see mapTreeView_BeforeSelect),
        // so the tree's own arrow-key handling used to just stop at the first one instead of
        // continuing into the next zone.
        private void MapTreeView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Up && e.KeyCode != Keys.Down) return;

            var maps = new List<TreeNode>();
            CollectMapNodes(mapTreeView.Nodes, maps);
            if (maps.Count == 0) return;

            int current = mapTreeView.SelectedNode == null ? -1 : maps.IndexOf(mapTreeView.SelectedNode);
            int next = current < 0 ? 0 : Math.Clamp(current + (e.KeyCode == Keys.Down ? 1 : -1), 0, maps.Count - 1);

            mapTreeView.SelectedNode = maps[next];
            maps[next].EnsureVisible(); // also expands the zone/area it sits in
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private static void CollectMapNodes(TreeNodeCollection nodes, List<TreeNode> into)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is MapEntry) into.Add(node);
                CollectMapNodes(node.Nodes, into);
            }
        }

        // "Required level" editor shown in the Properties tab for a selected Level Gate. Built
        // in code (not the Designer) and only visible for that entity kind. The requirement is
        // one byte at +6 of the gate's 8-byte record (x s16, y s16, template, style, level,
        // scanFlag -- see EntityReader.ReadLevelGates).
        private Panel _levelGatePanel = null!;
        private NumericUpDown _levelGateNud = null!;
        private bool _suppressLevelGateEvents;

        private void BuildLevelGatePanel()
        {
            _levelGateNud = new NumericUpDown { Minimum = 0, Maximum = 255, Width = 70, Location = new Point(110, 8) };
            var label = new Label { Text = "Required level:", AutoSize = true, Location = new Point(8, 11) };
            _levelGatePanel = new Panel { Dock = DockStyle.Top, Height = 40, Visible = false };
            _levelGatePanel.Controls.Add(label);
            _levelGatePanel.Controls.Add(_levelGateNud);
            propertiesTabPage.Controls.Add(_levelGatePanel);
            _levelGateNud.ValueChanged += LevelGateNud_ValueChanged;
        }

        private void LevelGateNud_ValueChanged(object? sender, EventArgs e)
        {
            if (_suppressLevelGateEvents || _rom == null) return;
            if (_selectedIndex < 0 || _selectedIndex >= _currentEntities.Count) return;

            var entity = _currentEntities[_selectedIndex];
            if (entity.Kind != EntityKind.LevelGate || entity.SourceAddress == 0) return;

            byte level = (byte)_levelGateNud.Value;
            _rom.PatchByte(entity.SourceAddress + 6, level); // live, like drags -- Save ROM As copies _rom
            _currentEntities[_selectedIndex] = entity with { TypeId = level };
            _dirty = true;

            UpdatePropertiesPanel();
            mapPictureBox.Invalidate();
        }

        private void DragonRadarUI_KeyDown(object? sender, KeyEventArgs e)
        {
            // Ctrl+Z undoes the last tile/collision stroke (not while typing in a text/number box,
            // where it means "undo typing").
            if (e.Control && ActiveControl is not (TextBoxBase or NumericUpDown))
            {
                // Ctrl+Z undo; Ctrl+Y or Ctrl+Shift+Z redo.
                if (e.KeyCode == Keys.Z && !e.Shift) { UndoLastStroke(); e.Handled = e.SuppressKeyPress = true; return; }
                if (e.KeyCode == Keys.Y || (e.KeyCode == Keys.Z && e.Shift)) { RedoLastStroke(); e.Handled = e.SuppressKeyPress = true; return; }
            }

            // Delete removes the selected trigger/object/NPC/level gate -- but never while you're
            // typing somewhere (a text box, the level-gate box, the map tree, a list), where Delete
            // means "delete a character/row" and must be left alone.
            if (e.KeyCode == Keys.Delete)
            {
                // (TreeView/ListView deliberately aren't here: they don't use Delete, and clicking
                // the map leaves them as the active control, which would swallow the key.)
                bool typing = ActiveControl is TextBoxBase or NumericUpDown or ComboBox;
                if (!typing && mainViewTabControl.SelectedTab == mapViewerTabPage
                    && _selectedIndex >= 0 && _selectedIndex < _currentEntities.Count)
                {
                    DeleteEntityAt(_selectedIndex);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                return;
            }

            if (e.KeyCode != Keys.Escape) return;

            if (_placingItemId.HasValue)
            {
                _placingItemId = null;
                _placingCustomPickup = false;
                UpdateStatusLabel();
            }

            if (_placingCharacterSpriteId.HasValue)
            {
                _placingCharacterSpriteId = null;
                UpdateStatusLabel();
            }

            if (_placingEnemy.HasValue)
            {
                _placingEnemy = null;
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
        private readonly ImageList _npcThumbnails = new ImageList
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

        // Same idea as _placingItemId, for the NPCs tab -- next map click places a new
        // EntityKind.Character (spriteId = _placingCharacterSpriteId) instead of
        // selecting/dragging. See EntityWriter.PersistNewCharacters for why saving this
        // only works on maps that already have at least one character.
        private int? _placingCharacterSpriteId;

        // Armed by clicking an entry in the NPCs tab while its mode box is on "Enemies": the
        // next map click drops a new EntityKind.Enemy with this (statIndex, spriteId) pair --
        // always a pair the game itself already ships (see EnemyRecords.FindTemplates).
        private (int StatIndex, int SpriteId)? _placingEnemy;
        private List<EnemyRecords.Template>? _enemyTemplates;

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

            // Always shown (even with 0 or 1 existing templates) so "Custom..." is always
            // reachable: reuse an existing pickup script from the ROM, or author a new one.
            var menu = new ContextMenuStrip();
            if (templates.Count == 0)
                menu.Items.Add("Default (no pickup script)", null, (_, _) => ArmPlacement(itemId, 0, 0));

            foreach (var t in templates)
            {
                string label = string.IsNullOrEmpty(t.MapName) ? $"onPickup=0x{t.OnPickup:X} msg=0x{t.CollectionMsg:X}" : $"{t.MapName} (onPickup=0x{t.OnPickup:X} msg=0x{t.CollectionMsg:X})";
                var onPickup = t.OnPickup;
                var collectionMsg = t.CollectionMsg;
                menu.Items.Add(label, null, (_, _) => ArmPlacement(itemId, onPickup, collectionMsg));
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Custom...", null, (_, _) =>
            {
                ArmPlacement(itemId, GenericPickupHandler, 0);
                _placingCustomPickup = true;
                statusLabel.Text = $"Placing item {itemId} with a custom pickup script — click the map to place it (Legacy opens right after), Esc to cancel";
            });
            menu.Show(menuOwner, menuAnchor);
        }

        // Set by the placement menu's "Custom..." entry: the next map click places the item
        // as usual and THEN opens Legacy on it (see BeginCustomPickupEditing).
        private bool _placingCustomPickup;

        // MapObject_OnPickup_RunCollectionMsg (0x800FFE6, Thumb so |1) -- the native handler
        // every pickup uses; it runs the object's collectionMsg as a dialog sequence (confirmed
        // via IDA). A custom pickup is therefore just: this handler + a new collectionMsg.
        private const int GenericPickupHandler = 0x0800FFE7;

        /// <summary>
        /// The "Custom..." flow, run right after the item is placed on the map: gives it a fresh
        /// pickup sequence (one empty script entry -- the thing you then fill in), writes the new
        /// object into the ROM now (so Legacy has a real object to open), reloads the map, and
        /// launches Legacy focused on that sequence through the same path as Edit Script --
        /// including its live save-watch, so each Save in Legacy reaches this window at once.
        /// </summary>
        private void BeginCustomPickupEditing(int entityIndex)
        {
            if (_rom == null || _game == null || _currentEntry == null) return;
            if (entityIndex < 0 || entityIndex >= _currentEntities.Count) return;

            int entryIndex = -1;
            for (int i = 0; i < _game.MapEntries.Count; i++)
            {
                if (ReferenceEquals(_game.MapEntries[i], _currentEntry)) { entryIndex = i; break; }
            }
            if (entryIndex < 0) return;
            int mapEntryAddress = _game.Config.MapEntriesOffset + (entryIndex * MapEntry.RecordSize);

            var placed = _currentEntities[entityIndex];
            int itemId = placed.TypeId;

            // A single mode-0 entry holding just END (0x11): an empty script Legacy shows as a
            // blank script node to type into. The generic pickup handler runs it on pickup.
            int sequenceAddress = DrGero.Quests.DialogWriter.WriteSequence(_rom, new[] { DrGero.Quests.DialogLine.Script(new byte[] { 0x11 }) });
            _currentEntities[entityIndex] = placed with { OnPickup = GenericPickupHandler, CollectionMsg = sequenceAddress };

            try
            {
                foreach (var e2 in _currentEntities)
                    WriteEntityPositionLive(e2);

                // Same persist calls Save ROM As makes -- all pending new objects/characters go
                // in together so the reload below can't drop any of them.
                EntityWriter.PersistNewObjects(_rom, _currentEntry, mapEntryAddress, _currentEntities);
                EntityWriter.PersistNewCharacters(_rom, _currentEntry, mapEntryAddress, _currentEntities);
                EnemyRecords.PersistNewEnemies(_rom, _game!.MapEntries, _currentEntry, mapEntryAddress, _currentEntities);
                _currentEntry.RefreshFrom(_rom, mapEntryAddress);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Couldn't place the item", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _dirty = true;
            RescanQuestsAndFlags();
            if (mapTreeView.SelectedNode != null)
                mapTreeView_AfterSelect(this, new TreeViewEventArgs(mapTreeView.SelectedNode));

            int idx = _currentEntities.FindIndex(e => e.Kind == EntityKind.Object && e.TypeId == itemId && e.CollectionMsg == sequenceAddress);
            if (idx < 0)
            {
                MessageBox.Show("The item was written to the ROM but couldn't be found on the reloaded map, so Legacy wasn't opened.",
                    "Custom pickup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _selectedIndex = idx;
            UpdateStatusLabel();
            UpdatePropertiesPanel(); // resolves the object's pickup-sequence field for the launch below
            mapPictureBox.Invalidate();

            EditSelectedPayload();
        }

        private void ArmPlacement(int itemId, int onPickup, int collectionMsg)
        {
            _placingItemId = itemId;
            _placingCustomPickup = false; // the Custom... menu entry sets this AFTER arming
            _placingTemplate = (onPickup, collectionMsg);
            statusLabel.Text = $"Placing item {itemId} — click the map to place it, Esc to cancel";
        }

        // Zone/Area group nodes (built by MapTreeBuilder -- Tag is null for anything
        // that isn't an actual map leaf) aren't maps and have nothing to show -- cancel
        // the selection outright so clicking one leaves whatever map was already open
        // in place instead of clearing the view.
        private void mapTreeView_BeforeSelect(object? sender, TreeViewCancelEventArgs e)
        {
            if (e.Node?.Tag is not MapEntry)
            {
                // Zone/Area headers can't be "opened" as a map, but clicking one should still do
                // something useful: expand or collapse it.
                e.Cancel = true;
                e.Node?.Toggle();
            }
        }

        private void toolStrip_RefreshMap_Click(object? sender, EventArgs e)
        {
            if (mapTreeView.SelectedNode != null)
                mapTreeView_AfterSelect(this, new TreeViewEventArgs(mapTreeView.SelectedNode));
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
            SyncVariantBox();

            _rom.PushPosition(entry.VariationArray);
            int mapOffset = _rom.ReadPointer();
            _rom.PopPosition();
            _currentMapOffset = mapOffset;

            RefreshPaintLayerChoices(); // needs _currentMapOffset (set above); picks the first editable layer
            var (bitmap, tilesets, usedTilesets, hasUnsupportedLayer, collisionGrid) = MapRenderer.RenderMap(_rom, _game.Config, mapOffset, BuildRenderOptions());
            _currentMapBitmap = bitmap;
            _currentTilesets = tilesets;           // for the brush's live preview
            _currentUsedTilesets = usedTilesets;   // to restore this map's own tile list after browsing another map's
            _tileEditors.Clear();                  // layer chunk caches belong to the previous map
            _undoStack.Clear();                    // and so does the undo/redo history
            _redoStack.Clear();
            _currentStroke = new();
            UpdateUndoButton();
            _collisionEditor = null;
            _atlasToSlot.Clear();
            _sourceAtlasIndices = null;            // tile source back to "this map"
            SyncTileSourceBox();
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

            UpdateSelectedTileInfo(); // the palette just changed (new map, or another map's tiles)
        }

        // Highlights every occurrence of the clicked tile on the map (see
        // MapRenderer.BuildTileUsageIndex and mapPictureBox_Paint's highlight box).
        private void listView1_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int? selected = listView1.SelectedItems.Count > 0 && listView1.SelectedItems[0].Tag is int tileId ? tileId : null;

            // The selected tile is also the paint brush. While "Paint tiles" is on the
            // highlight boxes are suppressed -- they'd just clutter the brush.
            if (selected.HasValue) _brushTileId = selected.Value;
            _highlightedTileId = _paintTilesButton is { Checked: true } || _tileSourceMap >= 0 && _sourceAtlasIndices != null ? null : selected;
            UpdateSelectedTileInfo();
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
            EntityKind.Enemy => Color.OrangeRed,
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
                //
                // Only the part being repainted (snapped out to whole 8x8 cells): building a
                // full-map-sized overlay on EVERY repaint made click-and-drag painting crawl.
                var bounds = new Rectangle(0, 0, _currentMapBitmap.Width, _currentMapBitmap.Height);
                var clip = Rectangle.Intersect(bounds, Rectangle.FromLTRB(
                    (e.ClipRectangle.Left / TileSize) * TileSize, (e.ClipRectangle.Top / TileSize) * TileSize,
                    ((e.ClipRectangle.Right + TileSize - 1) / TileSize) * TileSize, ((e.ClipRectangle.Bottom + TileSize - 1) / TileSize) * TileSize));
                if (clip.Width > 0 && clip.Height > 0)
                {
                    using var overlay = new Bitmap(clip.Width, clip.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    MapRenderer.DrawCollisionOverlay(_collisionGrid, overlay, clip.X / TileSize, clip.Y / TileSize);
                    g.DrawImage(overlay, clip.X, clip.Y);
                }
            }

            if (_showGridCheck.Checked) DrawGrid(g, e.ClipRectangle);

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
                    // Character.TypeId IS the real spriteId (see ReadMapScripts) -- same
                    // value CharacterIconReader/the NPCs tab placement flow already use.
                    Bitmap? icon = _rom == null || _game == null ? null : entity.Kind switch
                    {
                        EntityKind.Object => ItemIconReader.GetIcon(_rom, _game.Config, entity.TypeId),
                        EntityKind.Character or EntityKind.Enemy => CharacterIconReader.GetIcon(_rom, _game.Config, entity.TypeId),
                        _ => null
                    };

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

            // Live preview while dragging out a new trigger's zone (see toolStrip_AddTrigger_Click).
            if (_placingTriggerStart != null && _placingTriggerCurrent != null)
            {
                var start = _placingTriggerStart.Value;
                var end = _placingTriggerCurrent.Value;
                int left = Math.Min(start.X, end.X);
                int top = Math.Min(start.Y, end.Y);
                int width = Math.Abs(end.X - start.X);
                int height = Math.Abs(end.Y - start.Y);

                using var previewPen = new Pen(Color.Yellow, 1) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(previewPen, left, top, width, height);
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

        // 1-based position of the entity among entities of its own kind, in map/array order
        // ("Object #2 of 5") -- so several triggers/objects on one map are told apart in menus
        // and the Properties panel, and match the numbering Legacy's tree uses.
        private (int Ordinal, int Total) OrdinalOf(int index)
        {
            var kind = _currentEntities[index].Kind;
            int ordinal = 1 + _currentEntities.Take(index).Count(e => e.Kind == kind);
            int total = _currentEntities.Count(e => e.Kind == kind);
            return (ordinal, total);
        }

        private string DescribeEntityAt(int index)
        {
            var entity = _currentEntities[index];
            int n = OrdinalOf(index).Ordinal;
            return entity.Kind switch
            {
                // TypeId for Trigger is the raw vTable dispatch pointer read in ReadMapTriggers/
                // ReadVariationTriggers -- shown as a hex address since we don't have a confirmed
                // mapping from vtable address to trigger sub-type (DualRect/Dialog/Script/
                // SavePoint) yet. Not fabricating a friendly name we can't back up.
                EntityKind.Trigger => $"Trigger #{n} ({entity.Width}x{entity.Height}) vtable=0x{entity.TypeId:X}",
                EntityKind.Object => $"Object #{n} — item id {entity.TypeId}",
                EntityKind.Item => $"Item #{n} — id {entity.TypeId} (position approximate)",
                EntityKind.Decoration => $"Decoration #{n} — frame {entity.TypeId}",
                EntityKind.LevelGate => $"Level Gate #{n} — requires level {entity.TypeId}",
                EntityKind.Character => $"NPC #{n} — sprite {entity.TypeId}",
                EntityKind.Enemy => $"Enemy #{n} — sprite {entity.TypeId}, stat {entity.StatIndex}",
                EntityKind.SpawnScript => $"Spawn Script #{n}",
                _ => entity.Kind.ToString()
            };
        }

        // Right-click, VS-style "select at cursor" menu: lists every entity overlapping the
        // click point so you can pick exactly which one, rather than only ever getting
        // whichever is topmost. Complements Ctrl+Click (which passes through triggers to
        // whatever's underneath in one step) -- this covers picking between anything else
        // that overlaps too, trigger or not.
        private void ShowEntityContextMenu(Point location)
        {
            var hits = HitTestAll(location);
            if (hits.Count == 0) return;

            // Flat menu -- no layers to dig through, so right-clicking anywhere inside a
            // (transparent) trigger works. With one thing under the cursor it's just
            //     Edit Trigger code... / Select / Delete
            // and with several overlapping, each action lists them by name + number.
            bool multi = hits.Count > 1;
            string Name(int idx) => multi ? DescribeEntityAt(idx) : KindName(_currentEntities[idx].Kind);

            var menu = new ContextMenuStrip();

            var editable = hits.Where(i => _currentEntities[i].Kind is EntityKind.Trigger or EntityKind.Object or EntityKind.Character or EntityKind.Enemy).ToList();
            foreach (int idx in editable)
            {
                int captured = idx;
                menu.Items.Add($"Edit {Name(captured)} code...", null, (_, _) =>
                {
                    SelectEntity(captured);
                    EditSelectedPayload();
                });
            }
            if (editable.Count > 0) menu.Items.Add(new ToolStripSeparator());

            foreach (int idx in hits)
            {
                int captured = idx;
                menu.Items.Add(multi ? $"Select {Name(captured)}" : "Select", null, (_, _) => SelectEntity(captured));
            }
            menu.Items.Add(new ToolStripSeparator());

            foreach (int idx in hits)
            {
                int captured = idx;
                menu.Items.Add(multi ? $"Delete {Name(captured)}" : "Delete", null, (_, _) => DeleteEntityAt(captured));
            }

            menu.Show(mapPictureBox, location);
        }

        private static string KindName(EntityKind kind) => kind switch
        {
            EntityKind.Character => "NPC",
            EntityKind.Enemy => "Enemy",
            EntityKind.LevelGate => "Level Gate",
            EntityKind.SpawnScript => "Spawn Script",
            _ => kind.ToString()
        };

        private void SelectEntity(int index)
        {
            _selectedIndex = index;
            UpdateStatusLabel();
            UpdatePropertiesPanel();
            mapPictureBox.Invalidate();
        }

        /// <summary>
        /// Removes one entity from the current map. Not-yet-saved placements just leave the
        /// list; saved triggers/objects/NPCs/level gates are removed from their array in the
        /// ROM (rebuilt one slot shorter in free space, count decremented -- the removed record
        /// and old array become unreachable bytes, same no-reclaim approach as everywhere else
        /// here). Kinds without a known array format (items, decorations) are refused with a
        /// message rather than half-deleted. There's no undo: the in-memory ROM changes at once,
        /// but nothing reaches disk until Save ROM As.
        /// </summary>
        private void DeleteEntityAt(int index)
        {
            if (_rom == null || index < 0 || index >= _currentEntities.Count) return;
            var entity = _currentEntities[index];
            string what = DescribeEntityAt(index);

            if (entity.SourceAddress == 0)
            {
                _currentEntities.RemoveAt(index);
                _selectedIndex = -1;
                _dirty = true;
                UpdateStatusLabel();
                UpdatePropertiesPanel();
                mapPictureBox.Invalidate();
                statusLabel.Text = $"Deleted {what} (it hadn't been saved yet).";
                return;
            }

            int? mapEntryAddressOrNull = CurrentMapEntryAddress();
            if (mapEntryAddressOrNull == null || _currentEntry == null) return;
            int mapEntryAddress = mapEntryAddressOrNull.Value;
            var entry = _currentEntry;

            // (array pointer field, count field, element stride, slot in that array) per kind.
            int ptrField, countField, stride, arrayBase, count, slot = -1;
            switch (entity.Kind)
            {
                case EntityKind.Trigger:
                    ptrField = 0x10; countField = 0x03; stride = 8; arrayBase = entry.MapTriggers; count = entry.TriggerCount;
                    slot = (entity.SourceAddress - arrayBase) / stride;
                    break;
                case EntityKind.Object:
                    ptrField = 0x1C; countField = 0x06; stride = 4; arrayBase = entry.MapObjects; count = entry.ObjectCount;
                    slot = (entity.SourceAddress - arrayBase) / stride; // SourceAddress is the array slot itself
                    break;
                case EntityKind.LevelGate:
                    ptrField = 0x20; countField = 0x07; stride = 8; arrayBase = entry.NpcArray; count = entry.NpcCount;
                    slot = (entity.SourceAddress - arrayBase) / stride;
                    break;
                case EntityKind.Character:
                case EntityKind.Enemy:
                case EntityKind.SpawnScript:
                    // mapScripts[] is a pointer array; the entity's SourceAddress is the RECORD the
                    // pointer refers to, so find the slot whose pointer matches it.
                    ptrField = 0x14; countField = 0x04; stride = 4; arrayBase = entry.MapScripts; count = entry.ScriptCount;
                    for (int i = 0; i < count; i++)
                    {
                        _rom.PushPosition(arrayBase + i * 4);
                        int recordAddress = _rom.ReadPointer();
                        _rom.PopPosition();
                        if (recordAddress == entity.SourceAddress) { slot = i; break; }
                    }
                    break;
                default:
                    MessageBox.Show($"Deleting a {entity.Kind} isn't supported yet.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
            }

            if (arrayBase <= 0 || slot < 0 || slot >= count)
            {
                MessageBox.Show($"{what} isn't in this map's own array (it may come from the map's variation data), so it can't be deleted from here yet.",
                    "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            byte[] oldArray = _rom.ReadBytesAt(arrayBase, count * stride);
            byte[] newArray = new byte[oldArray.Length - stride];
            Buffer.BlockCopy(oldArray, 0, newArray, 0, slot * stride);
            Buffer.BlockCopy(oldArray, (slot + 1) * stride, newArray, slot * stride, newArray.Length - slot * stride);

            // Emptied array: leave the pointer alone -- every reader checks the count first and
            // never dereferences it when it's 0.
            if (newArray.Length > 0)
            {
                int newOffset = _rom.AllocateFreeSpace(newArray.Length);
                _rom.WriteBytesAt(newOffset, newArray);
                _rom.PatchInt32(mapEntryAddress + ptrField, 0x08000000 | newOffset);
            }
            _rom.PatchByte(mapEntryAddress + countField, (byte)(count - 1));
            entry.RefreshFrom(_rom, mapEntryAddress); // keep the in-memory entry in step with the bytes just patched

            // Reload from the ROM, but carry over anything placed this session that hasn't been
            // saved yet (SourceAddress 0) -- a plain reload would silently drop those.
            var pending = _currentEntities.Where(e => e.SourceAddress == 0).ToList();
            _dirty = true;
            RescanQuestsAndFlags(); // a deleted trigger/object may have been what set a quest flag
            toolStrip_RefreshMap_Click(this, EventArgs.Empty);
            _currentEntities.AddRange(pending);
            _selectedIndex = -1;
            UpdateStatusLabel();
            UpdatePropertiesPanel();
            mapPictureBox.Invalidate();
            statusLabel.Text = $"Deleted {what}.";
        }

        private void mapPictureBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (_currentMapBitmap == null) return;

            if (e.Button == MouseButtons.Right)
            {
                ShowEntityContextMenu(e.Location);
                return;
            }

            // Paint mode: click and HOLD paints (see PaintStrokeTo); nothing gets selected/dragged.
            if ((_paintTilesButton.Checked || _paintCollisionButton.Checked) && e.Button == MouseButtons.Left)
            {
                _highlightedTileId = null; // highlight boxes vanish while brushing
                mapPictureBox.Capture = true; // keep getting moves if the cursor leaves the map mid-stroke
                _currentStroke = new();       // a fresh undo step
                _painting = true;
                _lastPaintPoint = null;
                PaintStrokeTo(e.Location);
                return;
            }

            if (_placingNewTrigger)
            {
                _placingTriggerStart = e.Location;
                _placingTriggerCurrent = e.Location;
                mapPictureBox.Invalidate();
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

                if (_placingCustomPickup)
                {
                    _placingCustomPickup = false;
                    int placedIndex = _selectedIndex;
                    // Deferred so this mouse handler finishes (and the placed item paints) first.
                    BeginInvoke(new Action(() => BeginCustomPickupEditing(placedIndex)));
                }
                return;
            }

            if (_placingEnemy.HasValue)
            {
                var (statIndex, enemySprite) = _placingEnemy.Value;
                _currentEntities.Add(new Entity(EntityKind.Enemy, e.Location.X, e.Location.Y, enemySprite, SourceAddress: 0, StatIndex: statIndex));
                _selectedIndex = _currentEntities.Count - 1;
                _placingEnemy = null;
                _dirty = true;

                UpdateStatusLabel();
                mapPictureBox.Invalidate();
                return;
            }

            if (_placingCharacterSpriteId.HasValue)
            {
                _currentEntities.Add(new Entity(EntityKind.Character, e.Location.X, e.Location.Y, _placingCharacterSpriteId.Value, SourceAddress: 0));
                _selectedIndex = _currentEntities.Count - 1;
                _placingCharacterSpriteId = null;
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
            if (_painting)
            {
                PaintStrokeTo(e.Location);
                return;
            }

            if (_placingTriggerStart != null)
            {
                _placingTriggerCurrent = e.Location;
                mapPictureBox.Invalidate();
                return;
            }

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

                var resizedEntity = resizingEntity with { X = resizedX, Y = resizedY, Width = newWidth, Height = newHeight };
                _currentEntities[_selectedIndex] = resizedEntity;
                WriteEntityPositionLive(resizedEntity);
                _dirty = true;

                UpdateStatusLabelFast();
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
            if (_snapGridCheck.Checked) // Snap to grid: things you drag land on 8-pixel tile boundaries
            {
                newX = (int)Math.Round(newX / (double)TileSize) * TileSize;
                newY = (int)Math.Round(newY / (double)TileSize) * TileSize;
            }

            if (newX == entity.X && newY == entity.Y) return;

            var movedEntity = entity with { X = newX, Y = newY };
            _currentEntities[_selectedIndex] = movedEntity;
            WriteEntityPositionLive(movedEntity);
            _dirty = true;

            UpdateStatusLabelFast();
            mapPictureBox.Invalidate();
        }

        // FIXED 2026-09-19: dragging/resizing an entity used to only update the in-memory
        // _currentEntities list -- the ROM bytes weren't patched until "Save ROM As..."
        // explicitly ran EntityWriter.WritePosition over everything. That's a real
        // inconsistency with every other edit in this app (triggers, dialogue via Legacy)
        // which all patch _rom immediately: anything that re-reads entities fresh from
        // _rom in between -- toolStrip_AddTrigger_Click's refresh, or Dragon Radar's own
        // FileSystemWatcher reload after a Legacy save -- silently discarded whatever
        // position edit hadn't been "saved" yet, which is exactly what was reported
        // ("added a new one and it reset my size", "saved in Legacy and it all reset").
        // Now every drag/resize sample patches _rom directly, same as everything else, so
        // _rom is always the authoritative live state and nothing can wipe it out from
        // underneath an in-progress edit. Skips entities with SourceAddress == 0 (not yet
        // persisted to the ROM at all -- WritePosition would refuse those anyway; they're
        // still committed the first time via Save ROM As's PersistNewObjects/Characters).
        private void WriteEntityPositionLive(Entity entity)
        {
            if (_rom == null || entity.SourceAddress == 0) return;
            try { EntityWriter.WritePosition(_rom, entity, entity.X, entity.Y); }
            catch (NotSupportedException) { /* kind without write-back support yet -- display-only, same as before */ }
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
            if (_painting)
            {
                FinishTileStroke();
                return;
            }

            if (_placingTriggerStart != null)
            {
                var start = _placingTriggerStart.Value;
                var end = _placingTriggerCurrent ?? e.Location;

                int left = Math.Min(start.X, end.X);
                int top = Math.Min(start.Y, end.Y);
                int width = Math.Abs(end.X - start.X);
                int height = Math.Abs(end.Y - start.Y);

                // A plain click (no real drag) falls back to the old fixed-size default,
                // anchored at the click point, instead of creating a degenerate sliver --
                // same minimum this app already uses for resize (TileSize).
                if (width < TileSize || height < TileSize)
                {
                    width = 16;
                    height = 16;
                }

                CreateNewTrigger((short)left, (short)top, (short)(left + width), (short)(top + height));

                _placingNewTrigger = false;
                _placingTriggerStart = null;
                _placingTriggerCurrent = null;
                mapPictureBox.Cursor = Cursors.Default;
                mapPictureBox.Invalidate();
                return;
            }

            bool wasDraggingOrResizing = _dragging || _resizing;
            _dragging = false;
            _resizing = false;
            _resizeHandle = ResizeHandle.None;

            // Full refresh (quest/dialog rescans etc, see UpdateStatusLabelFast's comment)
            // happens once here, now that the drag/resize is actually done, rather than on
            // every MouseMove sample during it.
            if (wasDraggingOrResizing)
                UpdateStatusLabel();
        }

        private void toolStrip_ShowCollision_CheckedChanged(object sender, EventArgs e)
        {
            mapPictureBox.Invalidate();
        }

        // Cheap variant for high-frequency callers (every MouseMove sample while dragging/
        // resizing an entity) -- the full UpdateStatusLabel/UpdatePropertiesPanel path
        // re-runs ResolveTriggerScriptPayload + FindQuestsSetByScript (scans quest flags
        // against the script bytes) + DialogScanner.LooksLikeDialogSequence on EVERY call;
        // none of that changes just because the entity moved a pixel, so calling it dozens
        // of times per second while dragging was the actual cause of dragging/resizing
        // feeling glitchy/slow. This only updates the position text; the full rescan runs
        // once, in mapPictureBox_MouseUp, when the drag/resize actually finishes.
        private void UpdateStatusLabelFast()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _currentEntities.Count) return;
            var entity = _currentEntities[_selectedIndex];
            statusLabel.Text = $"{entity.Kind} @ ({entity.X},{entity.Y}) -- dragging... [unsaved]";
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
                EntityKind.Character => $"spriteId {entity.TypeId} (= script charIdx {entity.TypeId})",
                EntityKind.Enemy => $"spriteId {entity.TypeId}, stat entry {entity.StatIndex}",
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
            _selectedTriggerPayloadFileOffset = null;
            editScriptButton.Enabled = false;
            editDialogueButton.Enabled = false;
            duplicateTriggerButton.Enabled = false;
            deleteTriggerButton.Enabled = false;
            addDialogueTriggerButton.Enabled = false;
            _levelGatePanel.Visible = false; // re-shown below for a Level Gate

            if (!hasSelection)
            {
                propertiesLabel.Text = "No selection.\r\n\r\nClick an entity on the map, or right-click for a menu when more than one overlaps.";
                return;
            }

            var entity = _currentEntities[_selectedIndex];
            addDialogueTriggerButton.Enabled = (entity.Kind is EntityKind.Character or EntityKind.Enemy) && entity.SourceAddress != 0;
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

                    var payload = ResolveTriggerScriptPayload(entity);
                    _selectedTriggerPayloadFileOffset = payload?.PayloadFieldOffset;
                    editScriptButton.Enabled = _selectedTriggerPayloadFileOffset != null;
                    editDialogueButton.Enabled = _selectedTriggerPayloadFileOffset != null;
                    // Duplicate/Delete only need the trigger's own slot to be resolvable
                    // (entity.SourceAddress inside the current map's trigger array) --
                    // they don't touch the payload at all, so they're not gated on
                    // ResolveTriggerScriptPayload succeeding.
                    duplicateTriggerButton.Enabled = _currentEntry != null && entity.SourceAddress != 0;
                    deleteTriggerButton.Enabled = _currentEntry != null && entity.SourceAddress != 0;

                    if (payload != null)
                    {
                        lines.Add($"Script/payload address: 0x{(0x08000000 | payload.Value.ScriptAddress):X8}");

                        var questMatches = FindQuestsSetByScript(payload.Value.ScriptAddress);
                        if (questMatches.Count > 0)
                        {
                            lines.Add("");
                            lines.Add("Quest link found -- this trigger's script sets/clears a flag a quest checks:");
                            foreach (var m in questMatches) lines.Add($"  {m}");
                        }

                        if (DrGero.Quests.DialogScanner.LooksLikeDialogSequence(_rom!, payload.Value.ScriptAddress))
                        {
                            lines.Add("");
                            lines.Add("This looks like a dialog sequence, not a single flat script -- \"Edit Script\" will let you replace it with a plain script (it can't edit dialog sequences yet).");
                        }
                    }
                    break;
                case EntityKind.Object:
                    lines.Add($"Item ID: {entity.TypeId} (g_ItemsInGame index)");

                    // FIXED 2026-09-19: entity.OnPickup/CollectionMsg were always 0 here --
                    // ReadObjectArray never actually populated them (see its own fix note).
                    // "Edit Script"/"Edit Dialogue" now work on an object's OnPickup the
                    // same way they already do on a trigger's payload -- the Math
                    // Book/Golden Capsule/etc "give the player an item, run a script,
                    // maybe show a message" step is genuinely the same kind of payload,
                    // just reached through an object instead of a trigger.
                    var objPayload = ResolveObjectScriptPayload(entity);
                    _selectedTriggerPayloadFileOffset = objPayload?.PayloadFieldOffset;
                    editScriptButton.Enabled = _selectedTriggerPayloadFileOffset != null;
                    editDialogueButton.Enabled = _selectedTriggerPayloadFileOffset != null;

                    if (objPayload != null)
                    {
                        lines.Add($"Pickup dialogue (collectionMsg) address: 0x{(0x08000000 | objPayload.Value.ScriptAddress):X8}");

                        var questMatches = FindQuestsSetByScript(objPayload.Value.ScriptAddress, dialogOnly: true);
                        if (questMatches.Count > 0)
                        {
                            lines.Add("");
                            lines.Add("Quest link found -- picking this up sets/clears a flag a quest checks:");
                            foreach (var m in questMatches) lines.Add($"  {m}");
                        }
                    }
                    else
                    {
                        lines.Add("No pickup dialogue on this object (e.g. a plain decorative rock).");
                    }
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
                    _suppressLevelGateEvents = true;
                    _levelGateNud.Value = Math.Clamp(entity.TypeId, 0, 255);
                    _suppressLevelGateEvents = false;
                    _levelGatePanel.Visible = entity.SourceAddress != 0; // a not-yet-saved gate has no ROM record to patch
                    break;
                case EntityKind.Enemy:
                    lines.Add($"Sprite ID: {entity.TypeId}");
                    lines.Add($"Enemy stat entry: {entity.StatIndex} (g_ScouterStatDatabase)");
                    lines.Add("");
                    if (entity.SourceAddress != 0 && _rom != null)
                    {
                        int actionObj = EnemyRecords.ActionObject(_rom, entity.SourceAddress);
                        if (actionObj > 0 && actionObj + 8 <= _rom.Length)
                        {
                            int func = BitConverter.ToInt32(_rom.ReadBytesAt(actionObj, 4), 0);
                            lines.Add(func switch
                            {
                                EnemyRecords.RunDialogFunc => "Action: opens a dialog sequence.",
                                EnemyRecords.RunScriptFunc => "Action: runs a script.",
                                _ => $"Action: 0x{func:X8}."
                            });
                            editScriptButton.Enabled = true;
                            editDialogueButton.Enabled = true;
                        }
                    }
                    lines.Add("Dragging it also drags its patrol waypoints.");
                    lines.Add("");
                    lines.Add("Edit Script/Dialogue edits the enemy's own action (exactly when the game fires it isn't fully traced). For dialogue you can rely on, use \"Add Dialogue Trigger Here\".");
                    break;
                case EntityKind.Character:
                    lines.Add($"Sprite ID: {entity.TypeId}");
                    lines.Add("");
                    // CONFIRMED via IDA (Entity_GetByCharIndex -> Character_GetSpriteId,
                    // 2026-09): this is the EXACT same id space Legacy/Zenkai scripts pass
                    // as charIdx to WalkToPosition/FlyToPosition/SetEntityFacing/etc, not
                    // just a display-only sprite lookup.
                    lines.Add(entity.TypeId switch
                    {
                        0 => "Script charIdx 0 = the player. Same value used by WalkToPosition/FlyToPosition in Legacy scripts.",
                        >= 1 and <= 6 => $"Script charIdx {entity.TypeId} = party roster slot {entity.TypeId} (dynamic -- depends on current save state, not a fixed sprite). Same value used by WalkToPosition/FlyToPosition.",
                        _ => $"Script charIdx {entity.TypeId} -- same value used by WalkToPosition/FlyToPosition/etc in Legacy scripts to command this NPC. Only resolves if this NPC is actually spawned on the map the script runs on."
                    });
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

            RescanQuestsAndFlags();
            PopulateQuestList();

            PopulateMapTree();
            PopulateItemList();
            PopulateNpcList();
        }

        private void PopulateQuestList()
        {
            questListView.Items.Clear();
            foreach (var quest in _quests)
            {
                var item = new ListViewItem(new[]
                {
                    quest.Index.ToString(),
                    quest.Priority.ToString(),
                    quest.Name,
                    quest.AvailableCondition,
                    quest.CompleteCondition,
                    string.Join(", ", quest.FlagIds),
                })
                { Tag = quest };
                questListView.Items.Add(item);
            }
        }

        // Whichever flag id has a "test this flag" single-flag condition is safely
        // regenerable (see QuestWriter's class doc) -- true only when the decoded text is
        // the bare "Flag(N)" shape, not something wrapped in AND/NOT that this simple
        // editor would silently destroy if it rewrote it.
        private static bool HasSingleFlagCondition(List<int> flagIds, string conditionText) =>
            flagIds.Count == 1 && conditionText == $"Flag({flagIds[0]})";

        private DrGero.Quests.QuestEntry? _selectedQuest;
        private bool _suppressQuestPanelEvents;

        private void questListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var quest = questListView.SelectedItems.Count > 0 ? questListView.SelectedItems[0].Tag as DrGero.Quests.QuestEntry : null;
            _selectedQuest = quest;

            _suppressQuestPanelEvents = true;
            try
            {
                bool has = quest != null && _rom != null;
                questNameTextBox.Enabled = has;
                questPriorityCombo.Enabled = has;
                questFindFlagUsageButton.Enabled = has;

                questNameTextBox.Text = quest?.Name ?? "";
                questPriorityCombo.SelectedIndex = quest != null && quest.Priority is >= 0 and <= 2 ? quest.Priority : -1;

                bool availEditable = quest != null && HasSingleFlagCondition(quest.AvailableFlagIds, quest.AvailableCondition);
                questAvailableFlagCombo.Enabled = availEditable;
                questAvailableFlagCombo.Text = quest == null ? "" : availEditable ? quest.AvailableFlagIds[0].ToString() : quest.AvailableCondition;

                bool completeEditable = quest != null && HasSingleFlagCondition(quest.CompleteFlagIds, quest.CompleteCondition);
                questCompleteFlagCombo.Enabled = completeEditable;
                questCompleteFlagCombo.Text = quest == null ? "" : completeEditable ? quest.CompleteFlagIds[0].ToString() : quest.CompleteCondition;

                RepopulateKnownFlagItems();
            }
            finally
            {
                _suppressQuestPanelEvents = false;
            }
        }

        // Convenience dropdown contents for the two flag combos -- every flag id this ROM
        // is confirmed to actually use anywhere (set/cleared by a trigger, or tested by any
        // quest), sorted, so you're picking from real flags instead of guessing a number.
        private void RepopulateKnownFlagItems()
        {
            var known = _flagUsages.Select(u => u.FlagId)
                .Concat(_quests.SelectMany(q => q.FlagIds))
                .Distinct().OrderBy(f => f).Select(f => (object)f.ToString()).ToArray();

            questAvailableFlagCombo.Items.Clear();
            questAvailableFlagCombo.Items.AddRange(known);
            questCompleteFlagCombo.Items.Clear();
            questCompleteFlagCombo.Items.AddRange(known);
        }

        private void questNameTextBox_Leave(object? sender, EventArgs e)
        {
            if (_suppressQuestPanelEvents || _rom == null || _selectedQuest == null) return;
            if (questNameTextBox.Text == _selectedQuest.Name) return;

            DrGero.Quests.QuestWriter.WriteName(_rom, _selectedQuest, questNameTextBox.Text);
            _dirty = true;
            RescanQuestsAndFlags();
            RefreshQuestsAfterEdit();
        }

        private void questPriorityCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressQuestPanelEvents || _rom == null || _selectedQuest == null) return;
            if (questPriorityCombo.SelectedIndex < 0 || questPriorityCombo.SelectedIndex == _selectedQuest.Priority) return;

            DrGero.Quests.QuestWriter.WritePriority(_rom, _selectedQuest, questPriorityCombo.SelectedIndex);
            _dirty = true;
            RescanQuestsAndFlags();
            RefreshQuestsAfterEdit();
        }

        private void questAvailableFlagCombo_Leave(object? sender, EventArgs e)
        {
            if (_suppressQuestPanelEvents || _rom == null || _selectedQuest == null || !questAvailableFlagCombo.Enabled) return;
            if (!int.TryParse(questAvailableFlagCombo.Text, out int flagId)) return;
            if (_selectedQuest.AvailableFlagIds.Count == 1 && _selectedQuest.AvailableFlagIds[0] == flagId) return;

            DrGero.Quests.QuestWriter.WriteAvailableFlag(_rom, _selectedQuest, flagId);
            _dirty = true;
            RescanQuestsAndFlags();
            RefreshQuestsAfterEdit();
        }

        private void questCompleteFlagCombo_Leave(object? sender, EventArgs e)
        {
            if (_suppressQuestPanelEvents || _rom == null || _selectedQuest == null || !questCompleteFlagCombo.Enabled) return;
            if (!int.TryParse(questCompleteFlagCombo.Text, out int flagId)) return;
            if (_selectedQuest.CompleteFlagIds.Count == 1 && _selectedQuest.CompleteFlagIds[0] == flagId) return;

            DrGero.Quests.QuestWriter.WriteCompleteFlag(_rom, _selectedQuest, flagId);
            _dirty = true;
            RescanQuestsAndFlags();
            RefreshQuestsAfterEdit();
        }

        // FIXED 2026-09-19 (real report: "SetStoryFlag(3) does set that flag, but Find Flag
        // Usage doesn't see it"): _quests/_flagUsages were only ever computed ONCE, when a
        // ROM is first opened -- neither was refreshed after any LATER edit (a script saved
        // through Legacy, Add/Duplicate/Delete Trigger, or a quest edit itself), so Find
        // Flag Usage kept answering from a stale scan that predated whatever you'd just
        // written. Every place that changes trigger/script/quest data now calls this
        // afterward. FlagUsageScanner walks every map's triggers fresh each time -- cheap
        // enough (a few hundred maps) to just re-run rather than try to patch the cache
        // incrementally.
        private void RescanQuestsAndFlags()
        {
            if (_rom == null || _game == null) return;
            _quests = DrGero.Quests.QuestReader.ReadAll(_rom, _game.Config);
            _flagUsages = DrGero.Quests.FlagUsageScanner.ScanAll(_rom, _game.MapEntries);
        }

        // Re-reads the quest table fresh (RecordAddress/pointers don't move on these edits,
        // but re-decoding is cheap and guarantees the list/panel show exactly what's now in
        // the ROM rather than a stale in-memory copy) and re-selects the same quest by index
        // so the edit doesn't feel like it lost your place.
        private void RefreshQuestsAfterEdit()
        {
            int? selectedIndex = _selectedQuest?.Index;
            PopulateQuestList();
            if (selectedIndex != null)
            {
                foreach (ListViewItem item in questListView.Items)
                {
                    if (item.Tag is DrGero.Quests.QuestEntry q && q.Index == selectedIndex.Value)
                    {
                        item.Selected = true;
                        item.EnsureVisible();
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// The tracking tool requested alongside the editable Name/Priority/Flags: shows
        /// every place in the whole game that sets/clears the selected quest's flag(s), plus
        /// every OTHER quest that also tests the same flag -- so "which trigger/dialogue
        /// actually advances this quest" is a lookup instead of a manual ROM search.
        /// </summary>
        private void questFindFlagUsageButton_Click(object? sender, EventArgs e)
        {
            if (_selectedQuest == null) return;
            if (_selectedQuest.FlagIds.Count == 0)
            {
                MessageBox.Show("This quest's conditions don't test any flag this scanner recognizes.", "Find Flag Usage",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var quest = _selectedQuest;
            using var form = new Form
            {
                Text = $"Flag usage for \"{quest.Name}\"",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(820, 420),
                MinimizeBox = false,
                ShowIcon = false
            };

            var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
            list.Columns.Add("Flag", 50);
            list.Columns.Add("Effect", 60);
            list.Columns.Add("Where", 130);
            list.Columns.Add("Source", 300);
            list.Columns.Add("Notes", 240);

            foreach (int flagId in quest.FlagIds)
            {
                var others = _quests.Where(q => q.Index != quest.Index && q.FlagIds.Contains(flagId)).Select(q => q.Name).ToList();
                string notes = others.Count > 0 ? $"Also tested by: {string.Join(", ", others)}" : "";

                var setters = _flagUsages.Where(u => u.FlagId == flagId).ToList();
                if (setters.Count == 0)
                {
                    list.Items.Add(new ListViewItem(new[] { flagId.ToString(), "", "", "Nothing in the ROM sets or clears this flag", notes }) { ForeColor = Color.Gray });
                    continue;
                }

                foreach (var u in setters)
                {
                    string source = u.Source == "Object"
                        ? $"Object (item {u.ItemId}) -- picking it up @0x{(0x08000000 | u.TriggerDataAddress):X8}"
                        : $"Trigger @0x{(0x08000000 | u.TriggerDataAddress):X8}";
                    list.Items.Add(new ListViewItem(new[] { flagId.ToString(), u.IsSet ? "sets" : "clears", MapLabel(u), source, notes }) { Tag = u });
                }
            }

            var goTo = new Button { Text = "Go to in Map Viewer", Dock = DockStyle.Bottom, Height = 34, Enabled = false };
            var hint = new Label { Text = "Double-click a row (or select it and press the button) to jump to it in the Map Viewer.", Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };

            FlagUsageScanner_FlagUsage? chosen = null;
            void Choose()
            {
                if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not FlagUsageScanner_FlagUsage u) return;
                chosen = u;
                form.Close();
            }
            list.SelectedIndexChanged += (_, _) => goTo.Enabled = list.SelectedItems.Count > 0 && list.SelectedItems[0].Tag != null;
            list.DoubleClick += (_, _) => Choose();
            goTo.Click += (_, _) => Choose();

            form.Controls.Add(list);
            form.Controls.Add(hint);
            form.Controls.Add(goTo);
            form.ShowDialog(this);

            if (chosen != null)
                GoToFlagUsage(chosen);
        }

        // "Zone 2 Area 23 -- <map name>" for a usage row.
        private string MapLabel(FlagUsageScanner_FlagUsage u)
        {
            string name = _game != null && u.MapIndex >= 0 && u.MapIndex < _game.MapEntries.Count ? _game.MapEntries[u.MapIndex].Name : "";
            return string.IsNullOrEmpty(name) ? $"Z{u.Zone} A{u.Area}" : $"Z{u.Zone} A{u.Area} {name}";
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

        /// <summary>
        /// Switches to the Map Viewer, opens the map a flag-usage hit lives in, selects the
        /// exact trigger/object that sets the flag, and scrolls it into view.
        /// </summary>
        private void GoToFlagUsage(FlagUsageScanner_FlagUsage u)
        {
            if (_game == null || u.MapIndex < 0 || u.MapIndex >= _game.MapEntries.Count) return;

            var node = FindMapNode(mapTreeView.Nodes, _game.MapEntries[u.MapIndex]);
            if (node == null)
            {
                MessageBox.Show("Couldn't find that map in the map tree.", "Go to", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            mainViewTabControl.SelectedTab = mapViewerTabPage;
            if (mapTreeView.SelectedNode == node)
                mapTreeView_AfterSelect(this, new TreeViewEventArgs(node)); // already selected: force a reload so the entity list is fresh
            else
                mapTreeView.SelectedNode = node; // AfterSelect loads the map
            node.EnsureVisible();

            var kind = u.Source == "Object" ? EntityKind.Object : EntityKind.Trigger;
            int idx = _currentEntities.FindIndex(e => e.Kind == kind && e.SourceAddress == u.EntitySlot);
            if (idx < 0) return;

            _selectedIndex = idx;
            UpdateStatusLabel();
            UpdatePropertiesPanel();
            mapPictureBox.Invalidate();

            var entity = _currentEntities[idx];
            mapScrollPanel.AutoScrollPosition = new Point(
                Math.Max(0, entity.X - mapScrollPanel.ClientSize.Width / 2),
                Math.Max(0, entity.Y - mapScrollPanel.ClientSize.Height / 2));
        }

        /// <summary>
        /// Best-effort link from a script address (a trigger's action pointer, an entry/exit
        /// script, etc.) to any quest whose condition tests a flag that script SETS or
        /// CLEARS (opcode 27/28) -- see DrGero.Quests.InstructionDecoder.FindFlagWrites.
        /// Reads a generous 512-byte window since script length isn't stored anywhere; the
        /// decoder stops at END/an unknown opcode well before that in practice.
        ///
        /// A trigger's payload slot can hold either raw bytecode (Script-type triggers) or
        /// a dialog sequence[] array whose mode-0 entries embed bytecode between lines of
        /// conversation (Dialog-type triggers -- see DrGero.Quests.DialogScanner and
        /// Dialog_Format.md). Tries both interpretations rather than guessing which variant
        /// this trigger is; each interpretation only produces results if the bytes actually
        /// look like that shape, so trying both is safe.
        /// </summary>
        private List<string> FindQuestsSetByScript(int scriptAddress, bool dialogOnly = false)
        {
            var matches = new List<string>();
            // scriptAddress is a plain ROM file offset here (from ROM.ReadPointer(), which
            // already strips the 0x08 top byte) -- FIXED 2026-09-19: this used to check
            // for that top byte still being present, which is always false on a value
            // ReadPointer() already produced, so this silently returned zero matches for
            // every real trigger. See DrGero.Quests.DialogScanner's doc comment.
            if (_rom == null || scriptAddress <= 0 || scriptAddress >= _rom.Length)
                return matches;

            var writes = new List<(int FlagId, bool IsSet)>();

            if (!dialogOnly)
            {
                int readLen = Math.Min(512, _rom.Length - scriptAddress);
                byte[] bytes = _rom.ReadBytesAt(scriptAddress, readLen);
                var instructions = DrGero.Quests.InstructionDecoder.Decode(bytes);
                writes.AddRange(DrGero.Quests.InstructionDecoder.FindFlagWrites(instructions));
            }

            if (DrGero.Quests.DialogScanner.LooksLikeDialogSequence(_rom, scriptAddress))
                writes.AddRange(DrGero.Quests.DialogScanner.FindFlagWritesInDialog(_rom, scriptAddress));

            foreach (var (flagId, isSet) in writes)
            {
                foreach (var quest in _quests)
                {
                    if (quest.FlagIds.Contains(flagId))
                        matches.Add($"\"{quest.Name}\" ({(isSet ? "sets" : "clears")} flag {flagId})");
                }
            }

            return matches.Distinct().ToList();
        }

        private readonly record struct TriggerScriptPayload(int PayloadFieldOffset, int ScriptAddress);

        /// <summary>
        /// For a selected Trigger entity, resolves the file offset of its 4-byte
        /// script/payload pointer (dataPtr+0x10 -- see the corrected offset comment
        /// that used to live where FindQuestsSetByScript is now called from) and the
        /// pointer's current value. Returns null if the entity isn't a trigger or the
        /// chain doesn't resolve to a real ROM pointer. PayloadFieldOffset is what
        /// editScriptButton_Click patches with PatchInt32 after compiling a new script.
        ///
        /// FIXED 2026-09-19: dataPtr/scriptAddress are plain file offsets (ROM.ReadPointer()
        /// already strips the 0x08 top byte before returning them) -- the old
        /// "(x & 0xFF000000) == 0x08000000" validity checks here were always false on a
        /// value that already came from ReadPointer(), so this returned null for every
        /// real trigger and Edit Script/Edit Dialogue were never actually enabled.
        /// </summary>
        private TriggerScriptPayload? ResolveTriggerScriptPayload(Entity entity)
        {
            if (_rom == null || entity.Kind != EntityKind.Trigger || entity.SourceAddress == 0)
                return null;

            _rom.PushPosition(entity.SourceAddress);
            _rom.Skip(4);
            int dataPtr = _rom.ReadPointer();
            _rom.PopPosition();

            if (dataPtr <= 0)
                return null;

            int payloadFieldFileOffset = dataPtr + 0x10;
            _rom.PushPosition(payloadFieldFileOffset);
            int scriptAddress = _rom.ReadPointer();
            _rom.PopPosition();

            if (scriptAddress <= 0)
                return null;

            return new TriggerScriptPayload(payloadFieldFileOffset, scriptAddress);
        }

        /// <summary>
        /// Same idea as ResolveTriggerScriptPayload, for a selected Object entity's OnPickup
        /// field instead of a trigger's payload -- the Math Book/Golden Capsule/etc "give
        /// the player the item, then run a script (set a flag) and/or show a message" step
        /// (see ReadObjectArray's fix note: objectPtr+0x10/+0x14 are OnPickup/CollectionMsg,
        /// confirmed via the existing FindPickupTemplates/PersistNewObjects code that
        /// already read/wrote them, just never surfaced to the entity list before now).
        /// entity.SourceAddress here is the mapObjects[] array SLOT itself (a flat array of
        /// pointers, unlike a trigger's {vTable,dataPtr} pair) -- see ReadObjectArray -- so
        /// this reads straight through it, no Skip(4) first.
        /// </summary>
        private TriggerScriptPayload? ResolveObjectScriptPayload(Entity entity)
        {
            if (_rom == null || entity.Kind != EntityKind.Object || entity.SourceAddress == 0)
                return null;

            _rom.PushPosition(entity.SourceAddress);
            int objectPtr = _rom.ReadPointer();
            _rom.PopPosition();

            if (objectPtr <= 0)
                return null;

            // +0x10 is onPickup, a NATIVE function pointer (confirmed via IDA); the dialogue
            // it runs -- and where flag writes live -- is +0x14 collectionMsg.
            int payloadFieldFileOffset = objectPtr + 0x14;
            _rom.PushPosition(payloadFieldFileOffset);
            int scriptAddress = _rom.ReadPointer();
            _rom.PopPosition();

            if (scriptAddress <= 0)
                return null;

            return new TriggerScriptPayload(payloadFieldFileOffset, scriptAddress);
        }

        // Both buttons now hand off to Legacy.exe instead of the embedded ScriptEditorForm/
        // DialogEditorForm dialogs above -- per explicit request, "the edit logic should
        // open Legacy" with startup params that auto-focus the right Zone/Area (and, when
        // resolvable, the exact trigger's conversation). Legacy is the real full IDE
        // (syntax highlighting, autocomplete, the whole tree of every script/dialogue in
        // the game); these two forms were always a smaller stand-in.
        //
        // Legacy runs as a SEPARATE process with its OWN in-memory ROM copy, so this isn't
        // real IPC -- there's no live sync while both are open, and editing the same
        // trigger in both at once will just have one overwrite the other on reload. What
        // this DOES guarantee: Legacy is launched against a temp snapshot of whatever's
        // currently in Dragon Radar's memory (including unsaved edits), and the moment
        // Legacy's process exits, that same temp file is read back into Dragon Radar and
        // deleted -- so nothing edited in Legacy is silently lost, and nothing accumulates
        // in %TEMP%. FIXED 2026-09-19: both were bugs in the first cut of this (temp files
        // were never cleaned up, and Legacy's edits never made it back).
        //
        // Edit Script / Edit Dialogue / right-click "Edit X code..." all open the MICRO EDITOR:
        // a small Legacy.exe window (Legacy.exe --micro-edit ...) launched as its own process
        // on a temp copy of the ROM and watched for saves exactly like the full Legacy hand-off
        // below -- each Apply in it reaches this window straight away. Legacy owns the editor UI
        // and the Zenkai tooling, so Dragon Radar doesn't need to reference it.
        private void editScriptButton_Click(object? sender, EventArgs e) => EditSelectedPayload();

        private void editDialogueButton_Click(object? sender, EventArgs e) => EditSelectedPayload();

        private async void EditSelectedPayload()
        {
            if (_rom == null || _selectedIndex < 0 || _selectedIndex >= _currentEntities.Count) return;
            var entity = _currentEntities[_selectedIndex];
            string title = $"Edit {DescribeEntityAt(_selectedIndex)}";

            string kind;
            int field;
            switch (entity.Kind)
            {
                case EntityKind.Character:
                    // An NPC's "code" is its spawn condition: the record's flags field (+4).
                    if (entity.SourceAddress == 0)
                    {
                        MessageBox.Show("Save this NPC into the ROM first (it hasn't been written yet).", "Edit NPC code", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    kind = "npc";
                    field = entity.SourceAddress + 4;
                    title += " spawn condition";
                    break;

                case EntityKind.Enemy:
                    {
                        int actionObj = entity.SourceAddress == 0 ? 0 : EnemyRecords.ActionObject(_rom, entity.SourceAddress);
                        if (actionObj <= 0 || actionObj + 8 > _rom.Length)
                        {
                            MessageBox.Show("Save this enemy into the ROM first (it hasn't been written yet).", "Edit enemy action", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        kind = "enemy";
                        field = actionObj + 4; // the payload pointer; the micro editor also sets the func word at field-4
                        title += " action";
                        break;
                    }

                case EntityKind.Trigger:
                case EntityKind.Object:
                    {
                        var payload = entity.Kind == EntityKind.Trigger ? ResolveTriggerScriptPayload(entity) : ResolveObjectScriptPayload(entity);
                        if (payload == null)
                        {
                            MessageBox.Show("This has no script or dialogue to edit (or it hasn't been saved into the ROM yet).",
                                "Edit script/dialogue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        kind = entity.Kind == EntityKind.Trigger ? "trigger" : "object";
                        field = payload.Value.PayloadFieldOffset;
                        break;
                    }

                default:
                    return;
            }

            await LaunchLegacyForSelectedEntity($"--kind={kind} --field=0x{field:X} --title=\"{title.Replace('"', '\'')}\"");
        }

        // Generalized 2026-09-19 (was LaunchLegacyForSelectedTrigger, Trigger-only) to also
        // cover EntityKind.Object -- per explicit request: the Math Book/Golden Capsule/etc
        // "OnPickup" payload is the same kind of thing as a trigger's payload (a script
        // that can set/clear a flag, optionally shaped as dialogue), just reached through
        // an object instead of a trigger, and there's no good reason to build a second,
        // separate editor in Dragon Radar for it when Legacy already IS the shared script/
        // dialogue editor for triggers.
        // microArgs != null launches Legacy's micro editor (--micro-edit) instead of the full IDE.
        private async Task LaunchLegacyForSelectedEntity(string? microArgs = null)
        {
            if (_rom == null || _currentEntry == null) return;
            if (_selectedIndex < 0 || _selectedIndex >= _currentEntities.Count) return;
            var entity = _currentEntities[_selectedIndex];
            if (microArgs == null && entity.Kind != EntityKind.Trigger && entity.Kind != EntityKind.Object) return;

            uint? focusAddr = null;
            if (_selectedTriggerPayloadFileOffset != null)
            {
                _rom.PushPosition(_selectedTriggerPayloadFileOffset.Value);
                int scriptAddr = _rom.ReadPointer();
                _rom.PopPosition();
                if (scriptAddr > 0) focusAddr = 0x08000000 | (uint)scriptAddr;
            }

            // Explicit flush before Legacy gets a copy of this ROM -- drag/resize already
            // writes to _rom live (see WriteEntityPositionLive), so in practice this is a
            // no-op safety net, but it's here deliberately per the same request that fixed
            // the live-write gap: whatever's on screen should be what Legacy actually opens.
            foreach (var e2 in _currentEntities)
                WriteEntityPositionLive(e2);

            string tempRomPath = Path.Combine(Path.GetTempPath(), $"dragonradar_legacy_{Guid.NewGuid():N}.gba");
            File.WriteAllBytes(tempRomPath, _rom.ToArray());

            string legacyExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Legacy.exe");
            if (!File.Exists(legacyExePath))
            {
                MessageBox.Show($"Couldn't find Legacy.exe next to Dragon Radar (looked at: {legacyExePath}).",
                    "Legacy not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                File.Delete(tempRomPath);
                return;
            }

            var args = microArgs != null
                ? $"--micro-edit --rom=\"{tempRomPath}\" {microArgs}"
                : $"--rom=\"{tempRomPath}\" --zone={_currentEntry.Zone} --area={_currentEntry.Area}";
            if (microArgs == null && focusAddr != null) args += $" --focus=0x{focusAddr.Value:X8}";

            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = legacyExePath,
                Arguments = args,
                UseShellExecute = false
            });
            if (process == null)
            {
                File.Delete(tempRomPath);
                return;
            }

            statusLabel.Text = "Editing in Legacy -- changes appear here automatically each time you save there.";

            // The "IPC" here is deliberately just a filesystem watch on the shared temp
            // ROM, not a socket/pipe -- Legacy's own Save already writes straight to this
            // exact path with no dialog (its _saveTargetPath was set from --rom= on
            // startup), so watching that one file for writes is a genuine live notification
            // without needing either process to know anything about talking to the other.
            //
            // FIXED 2026-09-19 (this used to only reload when Legacy's whole PROCESS
            // exited, per explicit request that a Save in Legacy should reach Dragon Radar
            // immediately without needing to close Legacy first).
            var reloadGate = new object();
            DateTime lastReload = DateTime.MinValue;

            byte[]? TryReadTempRom()
            {
                // Legacy may still have the file open/mid-write the instant Changed fires --
                // a short retry loop is simpler and more robust here than trying to coordinate
                // exact write-completion between two independent processes.
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try { return File.ReadAllBytes(tempRomPath); }
                    catch (IOException) { System.Threading.Thread.Sleep(100); }
                }
                return null;
            }

            void ApplyReload(byte[] bytes)
            {
                _rom = ROM.FromBytes(bytes);
                _dirty = true;
                RescanQuestsAndFlags(); // a Legacy save can change/add a flag set/clear -- Find Flag Usage must see it
                toolStrip_RefreshMap_Click(this, EventArgs.Empty);
                statusLabel.Text = $"Reloaded -- Legacy saved changes at {DateTime.Now:T}.";
            }

            using var watcher = new FileSystemWatcher(Path.GetTempPath(), Path.GetFileName(tempRomPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            watcher.Changed += (s, e) =>
            {
                // A single Save can raise more than one Changed event (e.g. size then
                // write-time) -- coalesce bursts within 300ms into one reload.
                lock (reloadGate)
                {
                    if ((DateTime.UtcNow - lastReload).TotalMilliseconds < 300) return;
                    lastReload = DateTime.UtcNow;
                }
                byte[]? bytes = TryReadTempRom();
                if (bytes != null) BeginInvoke((MethodInvoker)(() => ApplyReload(bytes)));
            };

            // Doesn't block the UI thread -- Dragon Radar stays fully usable, and picks up
            // every Save via the watcher above, while Legacy is open. Resumes here (back on
            // the UI thread, courtesy of WinForms' SynchronizationContext) once Legacy's
            // process actually exits.
            await process.WaitForExitAsync();
            watcher.EnableRaisingEvents = false;

            // One last read in case Legacy's final Save (or the only Save, if it only saved
            // once right before closing) raced its own process exit.
            try
            {
                byte[]? finalBytes = TryReadTempRom();
                if (finalBytes != null) ApplyReload(finalBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Legacy closed, but its changes couldn't be read back: {ex.Message}",
                    "Reload from Legacy failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                try { File.Delete(tempRomPath); } catch { /* best-effort cleanup -- a leftover temp file isn't worth surfacing an error for */ }
            }

            UpdateStatusLabel();
        }

        /// <summary>
        /// Resolves the current map's own ROM address the same way toolStrip_SaveROM_Click
        /// already does for placing new objects/characters (_game.MapEntries[i] matched by
        /// reference against _currentEntry, address = MapEntriesOffset + i*RecordSize) --
        /// reused here so trigger create/delete can patch the MapEntry's own TriggerCount/
        /// MapTriggers fields directly.
        /// </summary>
        private int? CurrentMapEntryAddress()
        {
            if (_currentEntry == null || _game == null) return null;
            for (int i = 0; i < _game.MapEntries.Count; i++)
            {
                if (ReferenceEquals(_game.MapEntries[i], _currentEntry))
                    return _game.Config.MapEntriesOffset + (i * MapEntry.RecordSize);
            }
            return null;
        }

        // FIXED 2026-09-19 (real black-screen crash report): this used to "harvest" the
        // vTable field from the FIRST trigger found anywhere in the ROM with a nonzero
        // vTable, on the theory that -- like condition/function -- it'd be a universal,
        // freely-reusable constant. A byte-level diff of a saved ROM against the original
        // proved that theory wrong: a scan of every real dialog/script trigger in the
        // shipped game (condition/function == DialogHandlerA/B) found vTable is 0 for 18 of
        // them (confirmed on Z1A1/Z1A2/Z1A4/Z1A5, the very first maps in the game) and a
        // small, clustered-looking nonzero value (0x10000-0x30000 range) for 58 others,
        // concentrated on LATER maps (Z2A1 onward) -- i.e. it's NOT a fixed constant like
        // condition/function, it's per-trigger/per-map data, and the harvest was grabbing
        // an unrelated map's value and writing it into a trigger on a completely different
        // map. That mismatch is the most likely cause of the reported black screen: e.g.
        // MapTrigger_InitBase/call_ctor-style init code may read/dispatch through vTable at
        // map-load time (not lazily on interaction), so a wrong value there can break a map
        // immediately on load, not just when the player walks into the new trigger.
        //
        // 0 is the correct default: it's the exact value 18 real, currently-shipped,
        // definitely-working triggers already use, so it's the only value here that's
        // actually confirmed safe rather than borrowed from an unrelated context.
        private const int NewTriggerVTable = 0;

        // FIXED 2026-09-19 (explicit request: "It should probably let me draw a box where
        // I want it" instead of always dropping a fixed 16x16 box at the map origin).
        // Clicking the toolbar button now just ARMS placement mode -- the actual trigger
        // isn't created until the user drags out a rectangle on the map, same interaction
        // shape as _placingItemId/_placingCharacterSpriteId already use for point entities,
        // just with a drag instead of a single click since a trigger needs a rectangle.
        // See mapPictureBox_MouseDown/_MouseMove/_MouseUp for the drag capture, and
        // mapPictureBox_Paint for the live preview rectangle.
        private bool _placingNewTrigger;
        private Point? _placingTriggerStart;
        private Point? _placingTriggerCurrent;

        private void toolStrip_AddTrigger_Click(object? sender, EventArgs e)
        {
            if (_rom == null || _currentEntry == null) return;

            _placingNewTrigger = true;
            mapPictureBox.Cursor = Cursors.Cross;
            statusLabel.Text = "Click and drag on the map to place the new trigger's zone (click without dragging for a default 16x16 box).";
        }

        /// <summary>
        /// CREATE, genuinely from scratch -- unlike Duplicate, needs no existing trigger
        /// selected (works even when the current map has zero triggers). Builds a brand
        /// new MapTriggerScript-shaped record using condition = 0x0800D7E1 and
        /// function = 0x0800D765.
        ///
        /// CONFIRMED, not guessed: Legacy.cs (the separate Legacy IDE app) independently
        /// hardcodes these exact same two values --
        /// "const uint DialogHandlerA = 0x0800D7E1; // MapTrigger_OnEnter+1" and
        /// "const uint DialogHandlerB = 0x0800D765; // sub_800D764+1" -- and uses them as
        /// a strict filter (skip any trigger whose condition/function don't match) when
        /// scanning ALL 327 maps for dialog/script-capable triggers. Every such trigger in
        /// the entire game shares this exact pair, so it's a universal type-tag, not a
        /// per-trigger customized value -- which is exactly why a from-scratch trigger can
        /// use it directly instead of needing a template to clone.
        ///
        /// vTable is always 0 -- CONFIRMED 2026-09-19 by scanning every real dialog/script
        /// trigger already shipped in the game: 18 of them use exactly 0 (including the
        /// very first maps in the game), the rest use small per-map values that are NOT a
        /// reusable constant (an earlier version of this method "harvested" one of those
        /// from a random other map and it caused a real black-screen crash, most likely
        /// from map-load-time init code dispatching through the wrong value). 0 is the only
        /// value here that's actually confirmed safe.
        ///
        /// The payload defaults to a single-byte 0x11 (END) script -- an empty, valid
        /// script -- via the same +0x10 "scriptEntry" pointer field Edit Script already
        /// writes through (see ResolveTriggerScriptPayload/editScriptButton_Click).
        /// </summary>
        private void CreateNewTrigger(short x1, short y1, short x2, short y2)
        {
            if (_rom == null || _currentEntry == null) return;

            int? mapEntryAddress = CurrentMapEntryAddress();
            if (mapEntryAddress == null) return;

            const int ConditionPtr = unchecked((int)0x0800D7E1); // DialogHandlerA, confirmed universal (see summary above)
            const int FunctionPtr = unchecked((int)0x0800D765);  // DialogHandlerB, confirmed universal

            // Written as a genuine one-entry DIALOG_SCRIPT sequence (via the same
            // DialogWriter every real conversation edit already goes through), NOT a bare
            // script pointer -- Legacy's own PopulateDialogTree (WalkDialogArray) only
            // creates a tree node for a payload that LOOKS like a dialog table (its first
            // pointer must decode to a plausible mode-0..5 entry); a raw script address
            // fails that check and would leave a freshly-created trigger with nothing to
            // select in Legacy. A one-entry script sequence satisfies that check AND still
            // runs exactly the given opcode (here: nothing -- just END) with no text box,
            // same as any other DIALOG_SCRIPT step -- so Edit Script/Edit Dialogue in
            // Dragon Radar and Legacy's own tree both work on it immediately.
            var stubLine = DrGero.Quests.DialogLine.Script(new byte[] { 0x11 }); // END
            int scriptPtrFull = DrGero.Quests.DialogWriter.WriteSequence(_rom, new[] { stubLine });

            byte[] dataBytes = new byte[20];
            BitConverter.GetBytes(ConditionPtr).CopyTo(dataBytes, 0);
            BitConverter.GetBytes(FunctionPtr).CopyTo(dataBytes, 4);
            BitConverter.GetBytes(x1).CopyTo(dataBytes, 8);
            BitConverter.GetBytes(y1).CopyTo(dataBytes, 10);
            BitConverter.GetBytes(x2).CopyTo(dataBytes, 12);
            BitConverter.GetBytes(y2).CopyTo(dataBytes, 14);
            BitConverter.GetBytes(scriptPtrFull).CopyTo(dataBytes, 16);

            int newDataFileOffset = _rom.AllocateFreeSpace(dataBytes.Length);
            _rom.WriteBytesAt(newDataFileOffset, dataBytes);
            int newDataPtrFull = 0x08000000 | newDataFileOffset;

            int oldCount = _currentEntry.TriggerCount;
            byte[] existingArray = oldCount > 0
                ? _rom.ReadBytesAt(_currentEntry.MapTriggers, oldCount * 8)
                : Array.Empty<byte>();

            byte[] newArray = new byte[existingArray.Length + 8];
            Buffer.BlockCopy(existingArray, 0, newArray, 0, existingArray.Length);
            BitConverter.GetBytes(NewTriggerVTable).CopyTo(newArray, existingArray.Length);
            BitConverter.GetBytes(newDataPtrFull).CopyTo(newArray, existingArray.Length + 4);

            int newArrayFileOffset = _rom.AllocateFreeSpace(newArray.Length);
            _rom.WriteBytesAt(newArrayFileOffset, newArray);

            _rom.PatchInt32(mapEntryAddress.Value + 0x10, 0x08000000 | newArrayFileOffset);
            _rom.PatchByte(mapEntryAddress.Value + 3, (byte)(oldCount + 1));

            _currentEntry.MapTriggers = newArrayFileOffset;
            _currentEntry.TriggerCount = (byte)(oldCount + 1);

            _dirty = true;
            RescanQuestsAndFlags();
            int newTriggerFileOffset = newArrayFileOffset + existingArray.Length;
            toolStrip_RefreshMap_Click(this, EventArgs.Empty);

            for (int i = 0; i < _currentEntities.Count; i++)
            {
                if (_currentEntities[i].Kind == EntityKind.Trigger && _currentEntities[i].SourceAddress == newTriggerFileOffset)
                {
                    _selectedIndex = i;
                    break;
                }
            }
            UpdateStatusLabel();
            mapPictureBox.Invalidate();
        }

        /// <summary>
        /// CREATE. Clones the selected trigger's full record into free space and appends
        /// it as a new slot in the current map's trigger array.
        ///
        /// CONFIRMED via IDA 2026-09-19 (search_structs / type_inspect): every known
        /// trigger variant (MapTriggerDialog/Script/SavePoint/DualRect) shares the same
        /// leading shape -- condition (a function pointer, called via Condition_Evaluate),
        /// function (ALSO a function pointer -- MapTrigger_InitBase passes it through
        /// call_ctor, which is a trivial thunk that just calls it: `a1(a1)`), then
        /// x1/y1/x2/y2 as signed int16 corners. The largest confirmed variant
        /// (MapTriggerDualRect) is 24 bytes, so that's what gets cloned regardless of
        /// which variant this particular trigger actually is -- copying a few extra
        /// bytes nobody reads is harmless; guessing the wrong variant and copying too few
        /// would not be. condition/function are copied VERBATIM rather than synthesized:
        /// they're executable-code pointers, not data, and this way the clone is
        /// guaranteed to use the exact same (already working) condition-test and
        /// constructor as the trigger it was copied from.
        /// </summary>
        private void duplicateTriggerButton_Click(object? sender, EventArgs e)
        {
            if (_rom == null) return;
            if (_selectedIndex < 0 || _selectedIndex >= _currentEntities.Count) return;
            var entity = _currentEntities[_selectedIndex];
            if (entity.Kind != EntityKind.Trigger || entity.SourceAddress == 0) return;

            int? mapEntryAddress = CurrentMapEntryAddress();
            if (mapEntryAddress == null || _currentEntry == null) return;

            // FIXED 2026-09-19: ROM.ReadPointer() strips the 0x08 top byte before
            // returning -- vTable/dataPtr here are plain file offsets, NOT full
            // 0x08xxxxxx pointers. The old "(dataPtr & 0xFF000000) == 0x08000000" check
            // was always false (so this silently did nothing), AND vTable was being
            // written straight into the new array entry without its top byte -- the new
            // trigger's vtable field would have pointed at EWRAM/garbage instead of ROM.
            // Both are reconstructed to full addresses below before being stored anywhere
            // the GAME will read them back (MapEntry.MapTriggers/entity SourceAddress
            // fields, by contrast, are conventionally stored as plain offsets throughout
            // this codebase -- see EntityReader/MapEntry.Read -- so those stay unmasked).
            _rom.PushPosition(entity.SourceAddress);
            int vTable = _rom.ReadPointer();
            int dataPtr = _rom.ReadPointer();
            _rom.PopPosition();
            if (dataPtr <= 0 || vTable <= 0) return;
            int vTableFull = 0x08000000 | vTable;

            byte[] cloneBytes = _rom.ReadBytesAt(dataPtr, 24);

            // Nudge the clone's rectangle so it isn't drawn EXACTLY on top of the
            // original -- without this, "Duplicate" visibly appears to do nothing (the
            // new trigger is real, it's just perfectly hidden behind the one it was
            // copied from). x1/x2 are signed int16 at +8/+12 in every known variant.
            const short offset = 16;
            short x1 = BitConverter.ToInt16(cloneBytes, 8);
            short x2 = BitConverter.ToInt16(cloneBytes, 12);
            BitConverter.GetBytes((short)(x1 + offset)).CopyTo(cloneBytes, 8);
            BitConverter.GetBytes((short)(x2 + offset)).CopyTo(cloneBytes, 12);

            int newDataPtrFileOffset = _rom.AllocateFreeSpace(cloneBytes.Length);
            _rom.WriteBytesAt(newDataPtrFileOffset, cloneBytes);
            int newDataPtrFull = 0x08000000 | newDataPtrFileOffset;

            int oldCount = _currentEntry.TriggerCount;
            byte[] existingArray = oldCount > 0
                ? _rom.ReadBytesAt(_currentEntry.MapTriggers, oldCount * 8)
                : Array.Empty<byte>();

            byte[] newArray = new byte[existingArray.Length + 8];
            Buffer.BlockCopy(existingArray, 0, newArray, 0, existingArray.Length);
            BitConverter.GetBytes(vTableFull).CopyTo(newArray, existingArray.Length);
            BitConverter.GetBytes(newDataPtrFull).CopyTo(newArray, existingArray.Length + 4);

            int newArrayFileOffset = _rom.AllocateFreeSpace(newArray.Length);
            _rom.WriteBytesAt(newArrayFileOffset, newArray);

            _rom.PatchInt32(mapEntryAddress.Value + 0x10, 0x08000000 | newArrayFileOffset);
            _rom.PatchByte(mapEntryAddress.Value + 3, (byte)(oldCount + 1));

            // Keep the in-memory MapEntry (shared with the tree node's Tag) in sync --
            // EntityReader reads entry.MapTriggers/TriggerCount (these C# fields, not a
            // fresh ROM parse) to know where/how many triggers to read, so without this
            // the refresh below would still show the old count. Stored as a plain offset
            // (matching MapEntry.Read's own convention via ROM.ReadPointer()), NOT the
            // 0x08-prefixed value just patched into the ROM.
            _currentEntry.MapTriggers = newArrayFileOffset;
            _currentEntry.TriggerCount = (byte)(oldCount + 1);

            _dirty = true;
            RescanQuestsAndFlags(); // the clone carries over whatever script/dialogue the original had
            int newTriggerFileOffset = newArrayFileOffset + existingArray.Length; // the slot just appended
            toolStrip_RefreshMap_Click(this, EventArgs.Empty);

            // Re-select the new trigger -- toolStrip_RefreshMap_Click's reload always
            // resets _selectedIndex to -1, which (combined with the offset above) was
            // the other half of "Duplicate looks like it does nothing": even once you
            // could see the new rectangle, nothing told you it was the one just created.
            for (int i = 0; i < _currentEntities.Count; i++)
            {
                if (_currentEntities[i].Kind == EntityKind.Trigger && _currentEntities[i].SourceAddress == newTriggerFileOffset)
                {
                    _selectedIndex = i;
                    break;
                }
            }
            UpdateStatusLabel();
            mapPictureBox.Invalidate();
        }

        /// <summary>
        /// DELETE. Removes the selected trigger's slot from the current map's trigger
        /// array (rebuilt one slot shorter in free space; the removed slot's own record
        /// bytes and the old array are simply left as unreachable bytes -- no reclaim,
        /// same as everywhere else in this codebase).
        /// </summary>
        private void deleteTriggerButton_Click(object? sender, EventArgs e)
        {
            if (_rom == null) return;
            if (_selectedIndex < 0 || _selectedIndex >= _currentEntities.Count) return;
            var entity = _currentEntities[_selectedIndex];
            if (entity.Kind != EntityKind.Trigger || entity.SourceAddress == 0) return;

            int? mapEntryAddress = CurrentMapEntryAddress();
            if (mapEntryAddress == null || _currentEntry == null) return;

            // arrayBase/entity.SourceAddress are both plain file offsets (see the fix
            // note on duplicateTriggerButton_Click) -- the & mask here is a harmless
            // no-op on values that are already stripped, kept only for clarity.
            int oldCount = _currentEntry.TriggerCount;
            int arrayBase = _currentEntry.MapTriggers & 0x00FFFFFF;
            int slotIndex = (entity.SourceAddress - arrayBase) / 8;
            if (slotIndex < 0 || slotIndex >= oldCount) return; // not actually in this map's array -- refuse rather than guess

            byte[] existingArray = _rom.ReadBytesAt(arrayBase, oldCount * 8);
            byte[] newArray = new byte[existingArray.Length - 8];
            Buffer.BlockCopy(existingArray, 0, newArray, 0, slotIndex * 8);
            Buffer.BlockCopy(existingArray, (slotIndex + 1) * 8, newArray, slotIndex * 8, newArray.Length - slotIndex * 8);

            int newCount = oldCount - 1;
            if (newCount > 0)
            {
                int newArrayFileOffset = _rom.AllocateFreeSpace(newArray.Length);
                _rom.WriteBytesAt(newArrayFileOffset, newArray);
                _rom.PatchInt32(mapEntryAddress.Value + 0x10, 0x08000000 | newArrayFileOffset);
                // Plain offset stored here, NOT the 0x08-prefixed value just patched into
                // the ROM -- see duplicateTriggerButton_Click's fix note; MapEntry.MapTriggers
                // is conventionally a plain offset throughout this codebase.
                _currentEntry.MapTriggers = newArrayFileOffset;
            }
            // newCount == 0: leave MapTriggers pointing wherever it already does -- every
            // reader (EntityReader.ReadMapTriggers, Map_LoadInternal's real engine
            // equivalent) checks TriggerCount == 0 first and never dereferences the
            // pointer in that case, so there's nothing to patch.

            _rom.PatchByte(mapEntryAddress.Value + 3, (byte)newCount);
            _currentEntry.TriggerCount = (byte)newCount;

            _dirty = true;
            RescanQuestsAndFlags(); // the deleted trigger may have been the one setting/clearing a flag
            toolStrip_RefreshMap_Click(this, EventArgs.Empty);
        }

        // Live previews decoded straight from the ROM (see CharacterIconReader) rather than
        // a pre-extracted asset folder -- an earlier version used a "Character IDs" folder
        // of separately-extracted portrait art, but that turned out to be dialog-box
        // portraits, a completely different asset from the overworld sprite
        // MapScript_CreateCharacter actually spawns, so it wasn't a valid preview for
        // placement anyway. Requires a ROM to be loaded; called from toolStrip_OpenROM_Click.
        //
        // CharacterIconReader's native output is 16x32 (2x4 tiles) -- upscaled 2x to 32x64
        // here (nearest-neighbor, so it stays pixel-exact) purely for the thumbnail list.
        // Character frames are NOT all the same size (CONFIRMED via IDA -- see
        // CharacterIconReader's class doc: frames range from 8x8 up to 64x64, plus at least
        // one non-square 32x16 case), so this scales each icon to FIT inside a fixed square
        // box while preserving its real aspect ratio (centered), rather than force-stretching
        // every icon to one fixed size -- which is what made bigger characters (Cell, robots,
        // T-Rex) look squashed/distorted in the list.
        private const int NpcThumbnailBox = 64;

        private void PopulateNpcList()
        {
            npcListView.Items.Clear();
            _npcThumbnails.Images.Clear();
            _npcThumbnails.ImageSize = new Size(NpcThumbnailBox, NpcThumbnailBox);
            npcListView.LargeImageList = _npcThumbnails;
            npcListView.View = View.LargeIcon;

            if (_rom == null || _game == null) return;

            var images = new List<Image>();
            var items = new List<ListViewItem>();

            // Enemies mode lists the (stat, sprite) pairs the game ships, most common first;
            // NPC mode lists every drawable sprite id. Tag is the Template (enemy) or the id (NPC).
            var palette = new List<(int SpriteId, string Label, object Tag)>();
            if (npcPlaceModeCombo.SelectedIndex == 1)
            {
                _enemyTemplates ??= EnemyRecords.FindTemplates(_rom, _game.MapEntries);
                palette.AddRange(_enemyTemplates.Select(t => (t.SpriteId, $"Enemy {t.StatIndex} x{t.Count}", (object)t)));
            }
            else
            {
                palette.AddRange(CharacterIconReader.EnumerateValidSpriteIds(_rom, _game.Config).Select(id => (id, $"NPC {id}", (object)id)));
            }

            foreach (var (spriteId, label, tag) in palette)
            {
                var icon = CharacterIconReader.GetIcon(_rom, _game.Config, spriteId);
                if (icon == null) continue;

                float scale = Math.Min((float)NpcThumbnailBox / icon.Width, (float)NpcThumbnailBox / icon.Height);
                int drawWidth = Math.Max(1, (int)Math.Round(icon.Width * scale));
                int drawHeight = Math.Max(1, (int)Math.Round(icon.Height * scale));
                int drawX = (NpcThumbnailBox - drawWidth) / 2;
                int drawY = (NpcThumbnailBox - drawHeight) / 2;

                var thumbnail = new Bitmap(NpcThumbnailBox, NpcThumbnailBox);
                using (var g = Graphics.FromImage(thumbnail))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(icon, drawX, drawY, drawWidth, drawHeight);
                }

                images.Add(thumbnail);
                items.Add(new ListViewItem(label, images.Count - 1) { Tag = tag });
            }

            _npcThumbnails.Images.AddRange(images.ToArray());

            npcListView.BeginUpdate();
            npcListView.Items.AddRange(items.ToArray());
            npcListView.EndUpdate();
        }

        private void npcPlaceModeCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _placingEnemy = null;
            _placingCharacterSpriteId = null;
            PopulateNpcList();
        }

        /// <summary>
        /// Links an NPC or enemy to dialogue the only way the ROM supports (IDA 2026-09-19: no
        /// character entity's interact path reaches a conversation): a Dialog trigger zone
        /// covering it. Builds the zone around the selected NPC/enemy's sprite, then opens the
        /// new trigger's dialogue editor -- the same flow as Add Trigger, then Edit Dialogue.
        /// </summary>
        private void addDialogueTriggerButton_Click(object? sender, EventArgs e)
        {
            if (_rom == null || _game == null || _currentEntry == null) return;
            if (_selectedIndex < 0 || _selectedIndex >= _currentEntities.Count) return;

            var entity = _currentEntities[_selectedIndex];
            if (entity.Kind is not (EntityKind.Character or EntityKind.Enemy) || entity.SourceAddress == 0) return;

            var icon = CharacterIconReader.GetIcon(_rom, _game.Config, entity.TypeId);
            int w = icon?.Width ?? 16, h = icon?.Height ?? 32;
            const int pad = 16; // reach a little past the sprite so the player triggers it standing next to it

            CreateNewTrigger(
                (short)Math.Max(0, entity.X - (w / 2) - pad), (short)Math.Max(0, entity.Y - (h / 2) - pad),
                (short)(entity.X + (w / 2) + pad), (short)(entity.Y + (h / 2) + pad));

            EditSelectedPayload(); // CreateNewTrigger leaves the new trigger selected
        }

        private void npcListView_MouseDown(object? sender, MouseEventArgs e)
        {
            var item = npcListView.GetItemAt(e.X, e.Y);
            if (item?.Tag is EnemyRecords.Template enemy)
            {
                _placingEnemy = (enemy.StatIndex, enemy.SpriteId);
                _placingCharacterSpriteId = null;
                statusLabel.Text = $"Placing enemy (stat {enemy.StatIndex}, sprite {enemy.SpriteId}) — click the map to place it, Esc to cancel";
                return;
            }
            if (item?.Tag is not int spriteId) return;
            _placingEnemy = null;

            _placingCharacterSpriteId = spriteId;
            statusLabel.Text = $"Placing NPC (sprite {spriteId}) — click the map to place it, Esc to cancel";
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
                        placed += EnemyRecords.PersistNewEnemies(editedRom, _game.MapEntries, _currentEntry, mapEntryAddress, _currentEntities);

                        // The persist calls patch the ROM bytes of this map entry (new array
                        // pointer + count); re-read them into the in-memory entry or the reload
                        // below walks the OLD array and the newly-saved object "disappears".
                        _currentEntry.RefreshFrom(editedRom, mapEntryAddress);
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

            // Make the just-saved ROM the live one, then reload the current map through
            // the exact same path selecting it in the tree already uses -- re-reads every
            // entity fresh, so newly-placed items/characters pick up their real ROM
            // SourceAddress/PositionAddress and can keep being edited immediately, no
            // reopen required (previously required re-opening the saved file for this).
            _rom = editedRom;
            if (mapTreeView.SelectedNode != null)
                mapTreeView_AfterSelect(this, new TreeViewEventArgs(mapTreeView.SelectedNode));

            // Set after the reload above -- mapTreeView_AfterSelect ends by calling
            // UpdateStatusLabel() itself, which would otherwise overwrite this immediately.
            statusLabel.Text = $"Saved {written} entity position(s) and {placed} newly-placed item(s) to {Path.GetFileName(dialog.FileName)}.";
        }
    }
}
