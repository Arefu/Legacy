using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using DrGero.Catalog;
using DrGero.Config;
using DrGero.Engine;
using DrGero.IO;
using DrGero.Loader;
using DrGero.Rendering;

namespace DBZKit
{
    /// <summary>
    /// Puts every asset kind in the ROM on the main tab bar (characters, portraits, items, ability icons, maps, objects, tile atlas,
    /// text, and the resource blocks nobody has identified yet). The lists come from DrGero.Catalog.AssetCatalog, which reads the
    /// game's own tables through the game profile (games/&lt;CODE&gt;.json), so other game codes (LoG1, Buu's Fury, other regions and
    /// languages) work as soon as they have a profile; without one, only the resource scan is shown.
    ///
    /// Sound and boot screens are in the catalog too (for the ROM map) but have their own tabs (Sound, Intro, Attract Mode, Title Screen).
    ///
    /// Threads: the catalog is built and indexed off the UI thread on private ROM copies (indexing runs in parallel and is saved per
    /// ROM + profile hash, so re-opening a ROM is instant); previews run one at a time on a third copy. The EditSession's ROM is only
    /// read when loading and written by the Replace actions.
    /// </summary>
    internal sealed class RomBrowser : IDisposable
    {
        private static readonly HashSet<string> OwnTabs = ["Sound", "Boot screens"];

        private readonly EditSession _session;
        private readonly TabControl _host;
        private readonly ToolStripStatusLabel _status;
        private readonly ToolStripProgressBar _progress;
        private readonly Action<string, AssetNode?> _openEditor;
        private readonly List<CategoryPage> _pages = [];
        private readonly System.Windows.Forms.Timer _rebuildDelay = new() { Interval = 1500 };

        private AssetCatalog? _catalog;
        private CancellationTokenSource? _cancel;
        private ROM? _viewRom;
        private Game? _viewGame;
        private readonly SemaphoreSlim _viewLock = new(1, 1);
        internal readonly WaveOutPlayer Player = new();

        /// <summary>The profile of the loaded ROM (an empty one with just the game code if there is no games/&lt;CODE&gt;.json).</summary>
        public Game? Game { get; private set; }
        public AssetCatalog? Catalog => _catalog;

        public RomBrowser(EditSession session, TabControl host, ToolStripStatusLabel status, ToolStripProgressBar progress, Action<string, AssetNode?> openEditor)
        {
            _session = session;
            _host = host;
            _status = status;
            _progress = progress;
            _openEditor = openEditor;
            _rebuildDelay.Tick += (_, _) => { _rebuildDelay.Stop(); Reload(); };
            CreatePages();
            _host.SelectedIndex = 0;
            session.Loaded += () => { _openOnFirst = true; Reload(); };
            session.Changed += source => { if (!ReferenceEquals(source, this)) { _rebuildDelay.Stop(); _rebuildDelay.Start(); } };
        }

        public void Dispose()
        {
            _cancel?.Cancel();
            Player.Dispose();
            _rebuildDelay.Dispose();
        }

        // ---- profile ------------------------------------------------------------------------------------------------------------

        public static string GameCode(byte[] rom) => rom.Length >= 0xB0 ? Encoding.ASCII.GetString(rom, 0xAC, 4) : "";

        /// <summary>The game profile for the ROM's code (header 0xAC), or an empty one so the resource scan still works.</summary>
        public static (Game Game, string? Problem) LoadProfile(byte[] rom)
        {
            string code = GameCode(rom);
            string dir = Path.Combine(AppContext.BaseDirectory, "games");
            Game? game = null;
            try { if (Directory.Exists(dir)) game = GameLibrary.FindByCode(dir, code); }
            catch (Exception ex) { return (new Game { GAME_CODE = code }, $"games/{code}.json couldn't be read: {ex.Message}"); }
            return game != null ? (game, null) : (new Game { GAME_CODE = code }, $"No games/{code}.json profile for game code \"{code}\": only the resource scan is available.");
        }

        // ---- loading ------------------------------------------------------------------------------------------------------------

        // Every asset tab, in order. They are on the tab bar from the start (empty until a ROM is read) so the bar doesn't change
        // shape while a ROM loads; a kind the game profile doesn't cover keeps its tab and says so inside.
        private static readonly string[] TabNames =
            ["Characters", "Portraits", "Items", "Ability icons", "Maps", "Objects", "Scouter", "Area maps", "Menus", "Tile atlas", "Text", AssetCatalog.Unclassified];
        private bool _openOnFirst;

        private const string NoRom = "Open a ROM (File > Open Rom) to see its assets.";

        /// <summary>
        /// The asset tabs are made once and never removed: loading a ROM only refills them. Adding or removing tab pages makes the
        /// tab control jump between tabs and redraw the whole bar, which is what made loading look like it was flicking through tabs.
        /// </summary>
        private void CreatePages()
        {
            for (int i = 0; i < TabNames.Length; i++)
            {
                var page = new CategoryPage(this, new AssetCategory(TabNames[i]) { Note = NoRom });
                _pages.Add(page);
                _host.TabPages.Insert(i, page);
            }
        }

        private void EmptyPages(string note)
        {
            foreach (var page in _pages) page.Show(new AssetCategory(page.CategoryName) { Note = note }, keepSelection: false);
        }

        private async void Reload()
        {
            _cancel?.Cancel();
            Player.Stop();
            _catalog = null;

            // A newly opened ROM starts on Characters with the old ROM's assets cleared; a rebuild after an edit leaves the tabs
            // as they are until the new tables are ready, and keeps the open tab and the selected asset.
            bool opened = _openOnFirst;
            _openOnFirst = false;
            var rom = _session.Rom;
            if (rom == null) { EmptyPages(NoRom); _status.Text = ""; _progress.Visible = false; return; }
            if (opened)
            {
                EmptyPages("Reading asset tables...");
                _host.SelectedTab = _pages[0];
            }

            var cancel = _cancel = new CancellationTokenSource();
            byte[] bytes = rom.ToArray();
            var (game, problem) = LoadProfile(bytes);
            Game = game;
            _viewRom = ROM.FromBytes((byte[])bytes.Clone());
            _viewGame = game.Clone();

            _status.Text = "Reading asset tables...";
            AssetCatalog catalog;
            try { catalog = await Task.Run(() => AssetCatalog.Build(ROM.FromBytes((byte[])bytes.Clone()), game, cancel.Token)); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _status.Text = "Couldn't read this ROM: " + ex.Message; return; }
            if (cancel.IsCancellationRequested) return;

            _catalog = catalog;
            // Every kind keeps its tab; one the profile doesn't cover yet is empty and says why.
            foreach (var page in _pages)
                page.Show(catalog.Categories.FirstOrDefault(c => c.Name == page.CategoryName) ?? new AssetCategory(page.CategoryName), keepSelection: !opened);
            _status.Text = (problem ?? $"{game.GAME_CODE}: {catalog.Blocks.Count} resource blocks.") + " Indexing which asset uses each block...";

            // Index: decode every asset once (in parallel, on private copies) so each resource block learns its owner. The result is
            // saved per ROM + profile hash: re-opening the same ROM skips it, another version or an edited ROM gets its own.
            string indexFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DBZKit", "index",
                AssetCatalog.IndexKey(bytes, game) + ".json");
            bool cached = await Task.Run(() => catalog.TryLoadIndex(indexFile));
            if (cancel.IsCancellationRequested) return;
            if (!cached)
            {
                _progress.Value = 0;
                _progress.Visible = true;
                var progress = new Progress<(int Done, int Total, string What)>(p =>
                {
                    if (cancel.IsCancellationRequested) return;
                    _progress.Maximum = Math.Max(1, p.Total);
                    _progress.Value = Math.Min(p.Done, _progress.Maximum);
                });
                var clock = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    await Task.Run(() =>
                    {
                        catalog.Index(bytes, game, progress, cancel.Token);
                        try { catalog.SaveIndex(indexFile); } catch { /* no cache this time; still works */ }
                    });
                }
                catch (OperationCanceledException) { return; }
                catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException)) { return; }
                catch (Exception ex) { _status.Text = "Indexing failed: " + ex.Message; _progress.Visible = false; return; }
                if (cancel.IsCancellationRequested) return;
                problem = (problem != null ? problem + " " : "") + $"Indexed in {clock.Elapsed.TotalSeconds:0.0} s.";
            }

            _progress.Visible = false;
            int unclaimed = catalog.Blocks.Count(b => b.Owner == null);
            _status.Text = (problem != null ? problem + " " : "") +
                $"{game.GAME_CODE}: {catalog.Blocks.Count} resource blocks, {catalog.Blocks.Count - unclaimed} belong to a known asset, {unclaimed} not identified yet.";
            foreach (var page in _pages) page.Refresh(catalog);
        }

        public void ExportRomMap(IWin32Window owner)
        {
            if (_catalog == null || !_catalog.Indexed) { _status.Text = "The ROM map is ready once indexing has finished."; return; }
            using var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"{_catalog.Game.GAME_CODE}_rom_map.csv" };
            if (dialog.ShowDialog(owner) != DialogResult.OK) return;
            File.WriteAllText(dialog.FileName, _catalog.ToCsv());
            _status.Text = "ROM map written to " + dialog.FileName;
        }

        /// <summary>
        /// Writes every known asset into a folder (one folder per kind, PNG/TXT + unpacked and as-stored blocks + manifest.json):
        /// see DrGero.Catalog.AssetExporter. Runs off the UI thread with the status bar showing progress.
        /// </summary>
        public async void ExportAllAssets(IWin32Window owner)
        {
            if (_catalog == null || !_catalog.Indexed || _viewRom == null || _viewGame == null) { _status.Text = "Assets can be exported once indexing has finished."; return; }
            using var dialog = new FolderBrowserDialog { Description = "Choose the folder to export every asset into (one folder per kind, plus manifest.json)", UseDescriptionForTitle = true };
            if (dialog.ShowDialog(owner) != DialogResult.OK) return;
            byte[] bytes = _viewRom.ToArray();
            var catalog = _catalog;
            var game = _viewGame.Clone();
            _progress.Value = 0;
            _progress.Visible = true;
            var progress = new Progress<(int Done, int Total, string What)>(p =>
            {
                _progress.Maximum = Math.Max(1, p.Total);
                _progress.Value = Math.Min(p.Done, _progress.Maximum);
                _status.Text = $"Exporting {p.Done} / {p.Total}: {p.What}";
            });
            try
            {
                var result = await Task.Run(() => AssetExporter.Export(bytes, game, catalog, dialog.SelectedPath, null, progress));
                _status.Text = $"Exported {result.Assets} assets ({result.Blocks} resource blocks, {result.Files} files) to {dialog.SelectedPath}." +
                    (result.Problems.Count > 0 ? $" {result.Problems.Count} couldn't be drawn; see manifest.json." : "");
            }
            catch (Exception ex) { _status.Text = "Export failed: " + ex.Message; }
            finally { _progress.Visible = false; }
        }

        /// <summary>Runs a preview on the view ROM, one at a time, off the UI thread.</summary>
        internal async Task<AssetPreview> RunPreview(AssetNode node)
        {
            var rom = _viewRom ?? throw new InvalidOperationException("No ROM.");
            var game = _viewGame!;
            await _viewLock.WaitAsync();
            try { return await Task.Run(() => node.Preview!(rom, game)); }
            finally { _viewLock.Release(); }
        }

        internal Color[] Palette(string which)
        {
            if (_viewRom == null || _viewGame == null || which == "Greyscale") return Enumerable.Range(0, 256).Select(i => Color.FromArgb(i, i, i)).ToArray();
            _viewLock.Wait();
            try
            {
                return which == "Menu palette" && _viewGame.MenuPaletteOffset != 0 ? AssetCatalog.ReadPalette(_viewRom, _viewGame.MenuPaletteOffset)
                     : which == "BG palette" && _viewGame.BGPaletteOffset != 0 ? MapRenderer.ReadBGPalette(_viewRom, _viewGame)
                     : _viewGame.OBJPaletteOffset != 0 ? ItemIconReader.ReadOBJPalette(_viewRom, _viewGame)
                     : Enumerable.Range(0, 256).Select(i => Color.FromArgb(i, i, i)).ToArray();
            }
            finally { _viewLock.Release(); }
        }

        /// <summary>Switches to an editor tab, opened on this asset where the editor supports it (the Sprite editor picks the sprite).</summary>
        internal void OpenEditor(string tab, AssetNode? node) => _openEditor(tab, node);
        internal void Say(string text) => _status.Text = text;

        // ---- per-kind actions ---------------------------------------------------------------------------------------------------

        internal sealed record Action(string Text, Func<AssetNode?, bool> Visible, Action<CategoryPage, AssetNode?> Run);

        internal IEnumerable<Action> ActionsFor(string category) => category switch
        {
            "Portraits" => [new("Replace...", n => n?.Preview != null, (p, n) => ReplacePortrait(p, n!))],
            "Characters" => [
                // The dump folder is the Sprite editor's import format, and that editor is still US-only (SpriteDump.DumpSprite reads the US index).
                new("Dump this sprite...", n => n?.Preview != null && SpriteEditing, (p, n) => DumpSprites(p, [SpriteIdOf(n!)])),
                new("Dump all sprites...", _ => SpriteEditing, (p, _) => DumpSprites(p, null)),
            ],
            _ => [],
        };

        internal bool SpriteEditing => Game?.HasLayout("ALFE-sprites") == true;

        private static int SpriteIdOf(AssetNode node) => node.Id;

        /// <summary>
        /// Replaces a portrait with a 64x64 PNG (matched to the OBJ palette; an indexed PNG is used as-is) or a 4096-byte BIN. Stored
        /// uncompressed (ResourceHeader mode 0 = plain copy) in free space and the portrait table entry repointed.
        /// </summary>
        private void ReplacePortrait(CategoryPage page, AssetNode node)
        {
            var rom = _session.Rom;
            var game = Game;
            if (rom == null || game == null || game.PortraitTableOffset == 0) return;
            int index = node.Id;

            using var open = new OpenFileDialog { Filter = "64x64 PNG or 4096-byte BIN|*.png;*.bin" };
            if (open.ShowDialog(page) != DialogResult.OK) return;
            byte[] raw;
            try
            {
                if (open.FileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) raw = File.ReadAllBytes(open.FileName);
                else
                {
                    using var bmp = new Bitmap(open.FileName);
                    if (bmp.Width != 64 || bmp.Height != 64) { Say($"A portrait must be 64x64 (that PNG is {bmp.Width}x{bmp.Height})."); return; }
                    raw = ImageImport.ToIndices(bmp, ItemIconReader.ReadOBJPalette(rom, game), transparentZero: true);
                }
                if (raw.Length != 64 * 64) { Say($"A portrait must be {64 * 64} bytes (that file is {raw.Length})."); return; }
            }
            catch (Exception ex) { Say(ex.Message); return; }

            int at = ImageImport.WriteStored(rom, raw);
            rom.PatchInt32(game.PortraitTableOffset + index * 4, (int)(0x08000000u | (uint)at));
            _session.Apply();
            _session.NotifyChanged(null);
            Say($"Portrait {index} replaced. File > Save As (Ctrl+S) to keep it.");
        }

        private async void DumpSprites(CategoryPage page, List<int>? ids)
        {
            if (_viewRom == null) return;
            using var dialog = new FolderBrowserDialog { Description = "Choose a folder: one sprite_NNN folder (PNG + BIN + manifest) per sprite, importable in the Sprite editor tab" };
            if (dialog.ShowDialog(page) != DialogResult.OK) return;
            string root = dialog.SelectedPath;
            var rom = ROM.FromBytes(_viewRom.ToArray());
            ids ??= SpriteDump.SpriteIds(rom);
            int frames = 0;
            await Task.Run(() => { foreach (int id in ids) frames += SpriteDump.DumpSprite(rom, id, root); });
            Say($"Dumped {frames} frame(s) from {ids.Count} sprite(s) to {root}. Import them in the Sprite editor tab.");
        }

        // ---- one tab per category ---------------------------------------------------------------------------------------------

        internal sealed class CategoryPage : TabPage
        {
            private readonly RomBrowser _owner;
            private AssetCategory _category;
            private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Search", Margin = new Padding(0, 0, 0, 6) };
            private readonly System.Windows.Forms.Timer _searchDelay = new() { Interval = 200 };

            // A clean list: no dotted lines, a full-width highlight, a little more room per row.
            private readonly TreeView _tree = new()
            {
                Dock = DockStyle.Fill, HideSelection = false, ShowLines = false, FullRowSelect = true, HotTracking = true, ItemHeight = 22,
                BorderStyle = BorderStyle.None, Indent = 14,
            };
            private static readonly Font Mono = new("Consolas", 9f);
            private readonly Label _note = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DimGray, Visible = false };
            private readonly FlowLayoutPanel _images = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
            private readonly TextBox _text = new() { Dock = DockStyle.Bottom, Height = 220, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9f), Visible = false };

            // The details panel on the right: what the asset is, its facts, and everything you can do with it.
            private const int SideWidth = 290, SideInner = SideWidth - 48; // room for the padding and a scrollbar
            private readonly Panel _side = new() { Dock = DockStyle.Right, Width = SideWidth, Padding = new Padding(14, 12, 10, 12), AutoScroll = true };
            private readonly FlowLayoutPanel _sideFlow = new() { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            private readonly Label _title = new() { AutoSize = true, MaximumSize = new Size(SideInner, 0), Font = new Font("Segoe UI", 12f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 2) };
            private readonly Label _state = new() { AutoSize = true, MaximumSize = new Size(SideInner, 0), ForeColor = Color.DimGray, Margin = new Padding(0, 0, 0, 8) };
            private readonly TableLayoutPanel _facts = new() { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            private readonly Label _about = new() { AutoSize = true, MaximumSize = new Size(SideInner, 0), ForeColor = Color.DimGray, Margin = new Padding(0, 0, 0, 10) };
            private readonly FlowLayoutPanel _buttons = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            private readonly TableLayoutPanel _view = new() { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };

            private readonly Button _png = Btn("Export PNG..."), _bin = Btn("Export BIN..."), _wav = Btn("Export WAV..."), _play = Btn("Play"), _stop = Btn("Stop"), _open = Btn("");
            private readonly List<(Button Button, Action Action)> _extra = [];
            private readonly ComboBox _zoom = Combo(["Auto", "1x", "2x", "3x", "4x"]);
            private readonly ComboBox _depth = Combo(["8bpp", "4bpp"]), _layout = Combo(["Tiled", "Linear"]), _palette = Combo(["BG palette", "OBJ palette", "Menu palette", "Greyscale"]);
            private readonly NumericUpDown _width = new() { Minimum = 1, Maximum = 256, Value = 16, Width = 140 };
            private readonly NumericUpDown _bank = new() { Minimum = 0, Maximum = 15, Value = 0, Width = 140 };
            private readonly List<Control> _rawRows = [];
            // A map with alternate layouts: one row in the list, and this box switches which layout is shown.
            private readonly ComboBox _variant = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            private readonly TableLayoutPanel _variantRow = new() { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, 8), Visible = false };
            private bool _fillingVariants;

            private AssetNode? _node;
            private AssetNode? _shown; // _node, or the variant of it picked in _variant
            private AssetPreview? _preview;
            private int _request;

            private static Button Btn(string text) => new() { Text = text, Width = SideInner, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0), Margin = new Padding(0, 0, 0, 4) };
            private static ComboBox Combo(string[] items)
            {
                var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
                box.Items.AddRange(items);
                box.SelectedIndex = 0;
                return box;
            }

            public CategoryPage(RomBrowser owner, AssetCategory category) : base(category.Name)
            {
                _owner = owner;
                _category = category;
                Padding = new Padding(0);
                UseVisualStyleBackColor = true;

                var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
                // SplitterDistance only sticks once the control has its real size (a hidden tab starts out tiny).
                void SetSplit(object? s, EventArgs e)
                {
                    if (split.Width < 700) return;
                    split.SplitterDistance = 300;
                    split.SizeChanged -= SetSplit;
                }
                split.SizeChanged += SetSplit;
                split.Panel1.Controls.Add(_tree);
                split.Panel1.Controls.Add(_search);
                split.Panel1.Padding = new Padding(6, 6, 0, 0);
                _search.TextChanged += (_, _) => { _searchDelay.Stop(); _searchDelay.Start(); };
                _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); Fill(); };
                split.Panel1.Controls.Add(_note);

                // Primary action first (Edit / open the editor), then exports, then the category's own actions.
                _open.Font = new Font(_open.Font, FontStyle.Bold);
                foreach (var action in owner.ActionsFor(category.Name))
                {
                    var button = Btn(action.Text);
                    button.Click += (_, _) => action.Run(this, _node);
                    _extra.Add((button, action));
                }
                // Added one at a time, in this order: AddRange of controls that already have a parent can reorder them.
                foreach (var b in new[] { _open, _play, _stop }.Concat(_extra.Select(e => e.Button)).Concat([_png, _bin, _wav])) _buttons.Controls.Add(b);

                void Row(TableLayoutPanel table, string caption, Control control, bool raw = false)
                {
                    var label = new Label { Text = caption, AutoSize = true, ForeColor = Color.DimGray, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 12, 4) };
                    control.Anchor = AnchorStyles.Left;
                    control.Margin = new Padding(0, 2, 0, 2);
                    table.Controls.Add(label);
                    table.Controls.Add(control);
                    if (raw) _rawRows.AddRange([label, control]);
                }
                Row(_view, "Zoom", _zoom);
                Row(_view, "Format", _depth, raw: true);
                Row(_view, "Layout", _layout, raw: true);
                Row(_view, "Width", _width, raw: true);
                Row(_view, "Palette", _palette, raw: true);
                Row(_view, "4bpp bank", _bank, raw: true);

                _variantRow.Controls.Add(new Label { Text = "Variation", AutoSize = true, ForeColor = Color.DimGray, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 12, 4) });
                _variantRow.Controls.Add(_variant);
                _variant.SelectedIndexChanged += (_, _) =>
                {
                    if (_fillingVariants || _node == null) return;
                    int i = _variant.SelectedIndex;
                    LoadPreview(i <= 0 ? _node : _node.Variants[i - 1]);
                };

                _sideFlow.Controls.AddRange([_title, _state, _variantRow, _facts, _about, _buttons, _view]);
                _side.Controls.Add(_sideFlow);
                _side.Paint += (_, e) => e.Graphics.DrawLine(SystemPens.ControlLight, 0, 0, 0, _side.Height); // a quiet divider from the preview

                split.Panel2.Controls.Add(_images);
                split.Panel2.Controls.Add(_text);
                split.Panel2.Controls.Add(_side);
                Controls.Add(split);

                // Right-click an asset to copy its name or address.
                var menu = new ContextMenuStrip();
                menu.Items.Add("Copy name", null, (_, _) => { if (_tree.SelectedNode?.Tag is AssetNode n) Clipboard.SetText(n.Name); });
                var copyAddress = menu.Items.Add("Copy ROM address", null, (_, _) => { if (_tree.SelectedNode?.Tag is AssetNode { Address: >= 0 } n) Clipboard.SetText($"0x{0x08000000 | n.Address:X8}"); });
                menu.Opening += (_, e) => { copyAddress.Enabled = _tree.SelectedNode?.Tag is AssetNode { Address: >= 0 }; e.Cancel = _tree.SelectedNode == null; };
                _tree.ContextMenuStrip = menu;
                _tree.NodeMouseClick += (_, e) => { if (e.Button == MouseButtons.Right) _tree.SelectedNode = e.Node; };

                _tree.AfterSelect += (_, e) => Select(e.Node?.Tag as AssetNode);
                _png.Click += (_, _) => ExportPng();
                _bin.Click += (_, _) => ExportBin();
                _wav.Click += (_, _) => ExportWav();
                _play.Click += (_, _) => Play();
                _stop.Click += (_, _) => _owner.Player.Stop();
                _open.Click += (_, _) => _owner.OpenEditor((string)_open.Tag!, _node);
                _zoom.SelectedIndexChanged += (_, _) => ShowImages();
                foreach (var box in new[] { _depth, _layout, _palette }) box.SelectedIndexChanged += (_, _) => ShowImages();
                _width.ValueChanged += (_, _) => ShowImages();
                _bank.ValueChanged += (_, _) => ShowImages();
                AppSettings.Changed += Relabel;
                Disposed += (_, _) => AppSettings.Changed -= Relabel;

                Fill();
                ShowDetails(null, null);
                ShowActions();
            }

            /// <summary>The right-hand panel: title, a status line, the asset's facts, and a short note.</summary>
            private void ShowDetails(AssetNode? node, string? state)
            {
                _sideFlow.SuspendLayout();
                _title.Text = node?.Name ?? (_category.Nodes.Count > 0 ? "Pick something on the left" : _category.Name);
                _state.Text = state ?? "";
                _state.Visible = !string.IsNullOrEmpty(state);
                _facts.SuspendLayout();
                foreach (Control c in _facts.Controls) c.Dispose();
                _facts.Controls.Clear();
                var facts = new List<(string Label, string Value)>(node?.Details ?? []);
                // A short fact the preview worked out (a map's size) goes in with the rest.
                if (_preview?.Text is { Length: > 0 and < 60 } extra && node?.Kind == AssetKind.Image && !extra.Contains('\n')) facts.Add(("Size", extra));
                if (node is { Address: >= 0 } && AppSettings.Current.ShowAddresses) facts.Add(("ROM address", $"0x{0x08000000 | node.Address:X8}"));
                foreach (var (label, value) in facts)
                {
                    _facts.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 2, 12, 2) });
                    _facts.Controls.Add(new Label { Text = value, AutoSize = true, MaximumSize = new Size(SideInner - 110, 0), Margin = new Padding(0, 2, 0, 2) });
                }
                _facts.Visible = facts.Count > 0;
                _facts.ResumeLayout();
                _about.Text = node?.Info ?? "";
                _about.Visible = _about.Text.Length > 0;
                _sideFlow.ResumeLayout();
            }

            /// <summary>Redraws the list labels after "Show ROM addresses" changes, keeping the selection.</summary>
            private void Relabel()
            {
                if (IsDisposed) return;
                void Walk(TreeNodeCollection nodes)
                {
                    foreach (TreeNode t in nodes)
                    {
                        if (t.Tag is AssetNode a) t.Text = Label(a);
                        Walk(t.Nodes);
                    }
                }
                _tree.BeginUpdate();
                Walk(_tree.Nodes);
                _tree.EndUpdate();
                ShowDetails(_shown ?? _node, null);
            }

            public string CategoryName => _category.Name;

            /// <summary>Shows another catalog's version of this category (a ROM was opened or changed). The tab itself stays.</summary>
            public void Show(AssetCategory category, bool keepSelection)
            {
                string? wanted = keepSelection ? _node?.Name : null;
                _request++;
                _node = null; _shown = null; _variantRow.Visible = false;
                DisposePreview();
                _category = category;
                Fill();
                ShowDetails(null, null);
                ShowActions();
                if (wanted != null && Find(_tree.Nodes, wanted) is { } again) _tree.SelectedNode = again;

                static TreeNode? Find(TreeNodeCollection nodes, string name)
                {
                    foreach (TreeNode n in nodes)
                    {
                        if (n.Tag is AssetNode { Preview: not null } a && a.Name == name) return n;
                        if (Find(n.Nodes, name) is { } inner) return inner;
                    }
                    return null;
                }
            }

            public void Refresh(AssetCatalog catalog)
            {
                _category = catalog.Categories.First(c => c.Name == _category.Name);
                if (_category.Name == AssetCatalog.Unclassified) Fill();
                else SetTitle();
            }

            // The title only shows a count once there is one, and is only assigned when it changes: every change re-lays-out the tab bar.
            private void SetTitle()
            {
                string title = _category.Nodes.Count > 0 || _category.Count > 0 ? $"{_category.Name} ({_category.Count})" : _category.Name;
                if (Text != title) Text = title;
            }

            private void Fill()
            {
                SetTitle();
                string query = _search.Text.Trim();
                var selected = _node;
                _tree.BeginUpdate();
                _tree.Nodes.Clear();
                // Flat lists (Characters, Scouter) get no expand column; grouped ones (Maps by zone) do.
                _tree.ShowRootLines = _category.Nodes.Any(n => n.Children.Count > 0);
                foreach (var node in _category.Nodes)
                    if (Filtered(node, query) is { } tree) _tree.Nodes.Add(tree);
                if (_tree.Nodes.Count == 1 || query.Length > 0) _tree.ExpandAll();
                if (selected != null && FindNode(_tree.Nodes, selected) is { } again) _tree.SelectedNode = again;
                _tree.EndUpdate();
                _note.Text = _category.Nodes.Count == 0 ? _category.Note ?? "" : _tree.Nodes.Count == 0 ? $"Nothing matches \"{query}\"." : "";
                _note.Visible = _note.Text.Length > 0;
                _tree.Visible = !_note.Visible;
                _search.Visible = _category.Nodes.Count > 0;

                static TreeNode? FindNode(TreeNodeCollection nodes, AssetNode wanted)
                {
                    foreach (TreeNode n in nodes)
                    {
                        if (ReferenceEquals(n.Tag, wanted)) return n;
                        if (FindNode(n.Nodes, wanted) is { } inner) return inner;
                    }
                    return null;
                }
            }

            /// <summary>The node as a tree item if it or something under it matches the search (name or any detail value); null if not.</summary>
            private static TreeNode? Filtered(AssetNode node, string query)
            {
                if (query.Length == 0) return ToTree(node);
                bool self = node.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || node.Details.Any(d => d.Value.Equals(query, StringComparison.OrdinalIgnoreCase))
                         || (node.Address >= 0 && $"{0x08000000 | node.Address:X8}".Contains(query.TrimStart('0', 'x', 'X'), StringComparison.OrdinalIgnoreCase) && query.Length >= 4);
                if (self) return ToTree(node);
                var children = node.Children.Select(c => Filtered(c, query)).OfType<TreeNode>().ToArray();
                if (children.Length == 0) return null;
                var tree = new TreeNode(Label(node)) { Tag = node };
                tree.Nodes.AddRange(children);
                return tree;
            }

            /// <summary>A list label: the name, with the count for a group and the address only when that setting is on.</summary>
            private static string Label(AssetNode node) =>
                node.Address < 0 ? $"{node.Name}  ({node.Children.Count})"
                : AppSettings.Current.ShowAddresses ? $"{node.Name}    0x{0x08000000 | node.Address:X8}" : node.Name;

            private static TreeNode ToTree(AssetNode node)
            {
                var tree = new TreeNode(Label(node)) { Tag = node };
                foreach (var child in node.Children) tree.Nodes.Add(ToTree(child));
                return tree;
            }

            private void Select(AssetNode? node)
            {
                _node = node;
                _fillingVariants = true;
                _variant.Items.Clear();
                if (node is { Variants.Count: > 0 })
                {
                    _variant.Items.Add("1 (normal)");
                    for (int i = 0; i < node.Variants.Count; i++) _variant.Items.Add($"{i + 2}");
                    _variant.SelectedIndex = 0;
                }
                _variantRow.Visible = _variant.Items.Count > 1;
                _fillingVariants = false;
                LoadPreview(node);
            }

            private async void LoadPreview(AssetNode? node)
            {
                int request = ++_request;
                _shown = node;
                DisposePreview();
                ShowDetails(node, null);
                ShowActions();
                if (node?.Preview == null) return;

                ShowDetails(node, "Loading...");
                AssetPreview preview;
                try { preview = await _owner.RunPreview(node); }
                catch (Exception ex)
                {
                    if (request == _request) ShowDetails(node, "Couldn't show this: " + ex.Message);
                    return;
                }
                // The tabs are rebuilt (and this page disposed) when another ROM is opened or the ROM changes, possibly while the
                // preview was still loading: drop the result then.
                if (request != _request || IsDisposed || Disposing) { preview.Dispose(); return; }

                // Set before _preview so the controls' change events (which redraw) do nothing yet.
                if (node.Kind == AssetKind.Resource && preview.Raw != null) GuessRawFormat(preview.Raw);
                _preview = preview;
                ShowDetails(node, null);
                ShowActions();
                ShowImages();
            }

            private void DisposePreview()
            {
                foreach (Control c in _images.Controls) c.Dispose();
                _images.Controls.Clear();
                _preview?.Dispose();
                _preview = null;
                _text.Visible = false;
            }

            private void ShowActions()
            {
                var kind = _node?.Kind;
                bool ready = _preview != null;
                // The editor for this kind of asset, opened on the selected one.
                (string Tab, string Text)? editor = _category.Name switch
                {
                    "Characters" when _owner.SpriteEditing => ("Sprite editor", "Edit frames"),
                    "Sound" => ("Sound", "Open in Sound editor"),
                    "Text" when _node?.Name == "Credits" => ("Credits", "Edit credits"),
                    _ => null,
                };
                // Worked out as values first: Control.Visible reads false for every child of a hidden panel, so it can't be read back.
                bool png = ready && (_preview!.Images.Count > 0 || kind == AssetKind.Resource);
                bool bin = ready && _preview!.Raw != null;
                bool audio = ready && _preview!.Audio != null;
                bool open = _node?.Preview != null && editor != null;
                var extras = _extra.Select(e => e.Action.Visible(_node)).ToList();

                _png.Visible = png;
                _bin.Visible = bin;
                _bin.Text = ready && _preview!.RawExtension != "bin" ? $"Export {_preview.RawExtension.ToUpperInvariant()}..." : "Export BIN...";
                _wav.Visible = _play.Visible = _stop.Visible = audio;
                _view.Visible = ready && (_preview!.Images.Count > 0 || kind == AssetKind.Resource);
                foreach (var c in _rawRows) c.Visible = kind == AssetKind.Resource;
                for (int i = 0; i < _extra.Count; i++) _extra[i].Button.Visible = extras[i];
                _open.Visible = open;
                _open.Text = editor?.Text ?? "";
                _open.Tag = editor?.Tab;
                _buttons.Visible = png || bin || audio || open || extras.Any(v => v);
                if (_buttons.Controls.GetChildIndex(_open) != 0) _buttons.Controls.SetChildIndex(_open, 0); // the main action leads
            }

            // ---- images ----

            // Auto zoom keeps a preview within 1.5x a GBA screen (360x240) so it fits a 1080p or laptop display: a full-screen image
            // is shown at 1.5x, small ones at up to 3x, and anything larger than that box (maps) at 1x.
            private const int PreviewWidth = 360, PreviewHeight = 240;

            private float Zoom(Size size, float most = 3f)
            {
                if (_zoom.SelectedIndex > 0) return _zoom.SelectedIndex;
                // Bigger than the preview area (the world map is 4096 x 3328): shrink it to fit the space there is, never below 1/8.
                int roomW = Math.Max(200, _images.ClientSize.Width - 40), roomH = Math.Max(200, _images.ClientSize.Height - 60);
                if (size.Width > roomW || size.Height > roomH)
                    return Math.Max(0.125f, Math.Min((float)roomW / size.Width, (float)roomH / size.Height));
                float fit = Math.Min((float)PreviewWidth / Math.Max(1, size.Width), (float)PreviewHeight / Math.Max(1, size.Height));
                fit = Math.Clamp(fit, 1f, most);
                return fit >= 2f ? MathF.Floor(fit) : fit >= 1.5f ? 1.5f : 1f;
            }

            private void ShowImages()
            {
                foreach (Control c in _images.Controls) c.Dispose();
                _images.Controls.Clear();
                if (_preview == null) return;

                _images.SuspendLayout();
                if (_node?.Kind == AssetKind.Resource && _preview.Raw != null)
                {
                    using var image = RenderRaw(_preview.Raw);
                    AddCard($"{_preview.Raw.Length} bytes", image);
                    _text.Text = Hex(_preview.Raw);
                    _text.Visible = true;
                }
                else
                {
                    // A control per image is fine for a few, but a sprite has up to ~300 frames and 900 WinForms controls take seconds
                    // to create and lay out, so many images are drawn onto one contact sheet instead.
                    // A sheet is for many small frames (sprites). A map's layers, tileset and masks are different things and keep their labels.
                    if (_preview.Images.Count > SheetThreshold && _preview.Images.All(i => i.Image.Width <= 128 && i.Image.Height <= 128)) AddSheet(_preview.Images);
                    else foreach (var (label, image) in _preview.Images) AddCard(label, image);
                    _text.Text = _preview.Text?.Replace("\r\n", "\n").Replace("\n", Environment.NewLine) ?? "";
                    _text.Visible = _preview.Text != null && (_preview.Images.Count == 0 || _preview.Text.Length >= 80);
                    // Prose (Scouter descriptions, credits) wraps; hex dumps and tables keep their columns.
                    _text.WordWrap = _node?.Kind is AssetKind.Image;
                    _text.Font = _text.WordWrap ? Font : Mono;
                    _text.ScrollBars = _text.WordWrap ? ScrollBars.Vertical : ScrollBars.Both;
                    if (_node?.Kind == AssetKind.Text && _images.Parent is { } host) _text.Height = Math.Max(220, host.Height - 100);
                }
                _images.ResumeLayout();
            }

            private void AddCard(string label, Bitmap image)
            {
                float zoom = Zoom(image.Size);
                var scaled = new Bitmap(Math.Max(1, (int)(image.Width * zoom)), Math.Max(1, (int)(image.Height * zoom)));
                using (var g = Graphics.FromImage(scaled))
                {
                    // Pixel art is enlarged with hard edges; a shrunk map is smoothed so detail averages out instead of dropping.
                    g.InterpolationMode = zoom < 1 ? InterpolationMode.HighQualityBilinear : InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(image, 0, 0, scaled.Width, scaled.Height);
                }
                var card = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(6), WrapContents = false };
                string shown = zoom < 1 ? $", shown at {zoom * 100:0}%" : "";
                card.Controls.Add(new Label { Text = $"{label}  ({image.Width}x{image.Height}{shown})", AutoSize = true });
                var box = new PictureBox { Image = scaled, SizeMode = PictureBoxSizeMode.AutoSize, BackColor = Color.FromArgb(40, 40, 48) };
                box.Disposed += (_, _) => scaled.Dispose();
                card.Controls.Add(box);
                _images.Controls.Add(card);
            }

            private const int SheetThreshold = 12;

            /// <summary>All images on one bitmap: a grid of equal cells (sized to the largest image), wrapped to the panel width. No labels.</summary>
            private void AddSheet(List<(string Label, Bitmap Image)> images)
            {
                int zoom = (int)Zoom(new Size(images.Max(i => i.Image.Width), images.Max(i => i.Image.Height)), 2f);
                const int pad = 4;
                int cellW = images.Max(i => i.Image.Width) * zoom + pad, cellH = images.Max(i => i.Image.Height) * zoom + pad;
                int columns = Math.Clamp((_images.ClientSize.Width - 30) / cellW, 1, images.Count);
                int rows = (images.Count + columns - 1) / columns;
                // GDI+ bitmaps above ~16k pixels a side fail; a sprite never gets near that, but stay safe.
                rows = Math.Min(rows, 16000 / cellH);
                var sheet = new Bitmap(columns * cellW, rows * cellH);
                using (var g = Graphics.FromImage(sheet))
                {
                    g.Clear(_images.BackColor);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    using var back = new SolidBrush(Color.FromArgb(40, 40, 48));
                    for (int i = 0; i < Math.Min(images.Count, rows * columns); i++)
                    {
                        var image = images[i].Image;
                        int x = (i % columns) * cellW, y = (i / columns) * cellH;
                        g.FillRectangle(back, x, y, image.Width * zoom, image.Height * zoom);
                        g.DrawImage(image, x, y, image.Width * zoom, image.Height * zoom);
                    }
                }
                var box = new PictureBox { Image = sheet, SizeMode = PictureBoxSizeMode.AutoSize, Margin = new Padding(0, 4, 0, 0) };
                box.Disposed += (_, _) => sheet.Dispose();
                // Label and sheet stacked in one card. A label with a flow break before the sheet made the flow panel give the label's row
                // the sheet's height, so the frames were drawn a whole sheet-height below the label.
                var card = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(6) };
                card.Controls.Add(new Label { Text = $"{images.Count} images", AutoSize = true });
                card.Controls.Add(box);
                _images.Controls.Add(card);
            }

            /// <summary>Draws an unidentified block with the chosen guess (8/4bpp, tiled/linear, width, palette).</summary>
            private Bitmap RenderRaw(byte[] data)
            {
                var palette = _owner.Palette((string)_palette.SelectedItem!);
                bool four = _depth.SelectedIndex == 1, tiled = _layout.SelectedIndex == 0;
                if (four) palette = [.. palette.Skip((int)_bank.Value * 16).Take(16)];
                int width = (int)_width.Value;
                if (tiled)
                {
                    int tileBytes = four ? 32 : 64, tiles = Math.Max(1, (data.Length + tileBytes - 1) / tileBytes);
                    int high = Math.Min(512, (tiles + width - 1) / width);
                    var padded = Pad(data, width * high * tileBytes);
                    return four ? GBA.Render4bppTiled(padded, width, high, palette) : GBA.Render8bppTiled(padded, width, high, palette);
                }
                if (width < 8) width *= 8; // a "tiles" width typed while in linear mode
                int rows = Math.Min(2048, Math.Max(1, (data.Length + width - 1) / width));
                return GBA.Render8bpp(Pad(data, width * rows), width, rows, four ? null : palette);
            }

            /// <summary>
            /// Picks a starting format for an unidentified block. Most of what is left unidentified is a flat 8bpp bitmap the game
            /// draws text and bars onto (the stat card is 208x88), so each width that divides the size evenly is tried and the one whose
            /// rows line up best (most pixels equal to the pixel above) wins. Anything that doesn't line up stays 8bpp tiles, 16 wide.
            /// </summary>
            private void GuessRawFormat(byte[] data)
            {
                int best = 0, tried = 0;
                double bestScore = 0, total = 0;
                for (int width = 8; width <= 240; width += 8)
                {
                    int rows = data.Length / width;
                    if (data.Length % width != 0 || rows < 4 || rows > 512) continue;
                    int same = 0;
                    for (int i = width; i < data.Length; i++) if (data[i] == data[i - width]) same++;
                    double score = (double)same / (data.Length - width);
                    total += score; tried++;
                    if (score > bestScore) { bestScore = score; best = width; }
                }
                // Rows only "line up" if that width clearly beats the others (a detailed picture scores low at every width,
                // but still much higher at its real one), or nearly every pixel matches the one above.
                if (tried == 0 || (bestScore < 0.6 && (bestScore < 0.2 || bestScore < 1.5 * (total / tried)))) best = 0;
                _depth.SelectedIndex = 0;
                _layout.SelectedIndex = best > 0 ? 1 : 0;
                _width.Value = best > 0 ? best : 16;
            }

            private static byte[] Pad(byte[] data, int length) => data.Length >= length ? data : [.. data, .. new byte[length - data.Length]];

            private static string Hex(byte[] data)
            {
                var sb = new StringBuilder();
                int shown = Math.Min(data.Length, 0x2000);
                for (int row = 0; row < shown; row += 16)
                {
                    sb.Append($"{row:X6}  ");
                    for (int i = 0; i < 16; i++) sb.Append(row + i < shown ? $"{data[row + i]:X2} " : "   ");
                    sb.Append(' ');
                    for (int i = 0; i < 16 && row + i < shown; i++) { byte b = data[row + i]; sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.'); }
                    sb.AppendLine();
                }
                if (shown < data.Length) sb.AppendLine($"... {data.Length - shown} more bytes (Export BIN for all of it)");
                return sb.ToString();
            }

            // ---- export / audio ----

            private string BaseName => string.Concat(((_shown ?? _node)?.Name ?? "asset").Split(Path.GetInvalidFileNameChars())).Replace(' ', '_').Replace("__", "_");

            private void ExportPng()
            {
                if (_preview == null) return;
                if (_node?.Kind == AssetKind.Resource && _preview.Raw != null)
                {
                    using var image = RenderRaw(_preview.Raw);
                    SaveOne(image, BaseName);
                    return;
                }
                if (_preview.Images.Count == 1) { SaveOne(_preview.Images[0].Image, BaseName); return; }

                using var folder = new FolderBrowserDialog { Description = $"Folder for the {_preview.Images.Count} images of {(_shown ?? _node)?.Name}" };
                if (folder.ShowDialog(this) != DialogResult.OK) return;
                int n = 0;
                foreach (var (label, image) in _preview.Images)
                {
                    string name = $"{BaseName}_{n++:D3}_{string.Concat(label.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_')}";
                    image.Save(Path.Combine(folder.SelectedPath, name[..Math.Min(name.Length, 120)] + ".png"), ImageFormat.Png);
                }
            }

            private void SaveOne(Bitmap image, string name)
            {
                using var dialog = new SaveFileDialog { Filter = "PNG (*.png)|*.png", FileName = name + ".png" };
                if (dialog.ShowDialog(this) == DialogResult.OK) image.Save(dialog.FileName, ImageFormat.Png);
            }

            private void ExportBin()
            {
                if (_preview?.Raw == null) return;
                string ext = _preview.RawExtension;
                using var dialog = new SaveFileDialog { Filter = $"{ext.ToUpperInvariant()} (*.{ext})|*.{ext}", FileName = $"{BaseName}.{ext}" };
                if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllBytes(dialog.FileName, _preview.Raw);
            }

            private void ExportWav()
            {
                if (_preview?.Audio is not { } clip) return;
                using var dialog = new SaveFileDialog { Filter = "WAV (*.wav)|*.wav", FileName = BaseName + ".wav" };
                if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllBytes(dialog.FileName, Wav16(clip));
            }

            private void Play()
            {
                if (_preview?.Audio is not { } clip) return;
                _owner.Player.Stop();
                _owner.Player.Load(clip.Pcm, clip.Channels, clip.Rate);
                _owner.Player.Play(0);
            }

            private static byte[] Wav16(AudioClip clip)
            {
                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                int bytes = clip.Pcm.Length * 2;
                w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
                w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)clip.Channels); w.Write(clip.Rate);
                w.Write(clip.Rate * clip.Channels * 2); w.Write((short)(clip.Channels * 2)); w.Write((short)16);
                w.Write("data"u8); w.Write(bytes);
                foreach (short s in clip.Pcm) w.Write(s);
                return ms.ToArray();
            }
        }
    }
}
