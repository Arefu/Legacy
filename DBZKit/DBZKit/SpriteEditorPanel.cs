using DrGero.Engine;
using DrGero.IO;

namespace DBZKit
{
    /// <summary>
    /// Sprite editor: replaces a character sprite's animation frames from PNGs. Works on the shared <see cref="EditSession"/> ROM copy.
    /// A sprite record's slots each point at an ARRAY of descriptors (one per animation frame) -- see FrameImport.WriteFrameArray and
    /// Roster-and-Abilities.md ("Ability animations vs sprite records").
    /// </summary>
    internal sealed class SpriteEditorPanel : UserControl
    {
        private readonly EditSession _session;
        private ROM? _rom => _session.Rom;
        private readonly Label _noRom = new() { Text = "Open a ROM (File > Open Rom) to use the sprite editor.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        private readonly Panel _content = new() { Dock = DockStyle.Fill, Visible = false };

        public SpriteEditorPanel(EditSession session)
        {
            _session = session;
            Dock = DockStyle.Fill;
            BuildUi();
            Controls.Add(_content);
            Controls.Add(_noRom);
            session.Loaded += OnLoaded;
            session.Changed += source => { if (!ReferenceEquals(source, this) && _content.Visible) { _stripKey = (-1, -1); FillSpriteCombo(SelectedSprite); RefreshSlots(); } };
        }

        private void OnLoaded()
        {
            _relocated.Clear();
            bool ok = _rom != null && RosterTables.IsSupportedRom(_rom);
            _content.Visible = ok;
            _noRom.Visible = !ok;
            if (_rom != null && !ok) _noRom.Text = "This doesn't look like the US ROM the sprite tables were mapped from, so the editor is disabled.";
            if (ok) { FillSpriteCombo(); RefreshSlots(); }
        }

        private void Fail(string msg) => MessageBox.Show(this, msg, "Can't apply", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        // A sprite record holds 4-frame GROUPS (record + 0x4C + 16n): Down, Up, Left, Right. Stock "Right" is the Left image X-flipped, so you
        // only need 3 PNGs per group. Groups 0-4 are walking/standing poses; the groups an ability reads are +156 (n=5), +364 (n=18), +380 (n=19).
        private readonly ComboBox _fSprite = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        private readonly NumericUpDown _fX = new() { Minimum = -128, Maximum = 127, Value = 0, Width = 56 }, _fY = new() { Minimum = -128, Maximum = 127, Value = -24, Width = 56 };
        private readonly ComboBox _fGroup = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
        private readonly CheckBox _fAuto = new() { Text = "Auto x/y (matches stock)", Checked = true, AutoSize = true, Margin = new Padding(8, 6, 0, 0) };
        private readonly CheckBox _fPad = new() { Text = "Pad smaller PNGs to stock size", Checked = true, AutoSize = true, Margin = new Padding(8, 6, 0, 0) };
        private readonly CheckBox _fMirror = new() { Text = "Right = Left flipped", Checked = true, AutoSize = true, Margin = new Padding(8, 6, 0, 0) };
        private readonly PictureBox[] _fCur = new PictureBox[4], _fNew = new PictureBox[4];
        private readonly Label[] _fLbl = new Label[4];
        private readonly string?[] _fPng = new string?[4];
        private readonly HashSet<int> _relocated = [];
        // Each slot (Down / Up / Left / Right) holds an ARRAY of animation frames (a stand-still with a blink, a walk cycle, ...): this picks which one is
        // shown and written. "All frames" writes the same picture into every frame of the slot (fine for static poses such as ki blasts).
        private readonly NumericUpDown _fFrame = new() { Minimum = 0, Maximum = 0, Width = 56 };
        private readonly CheckBox _fAllFrames = new() { Text = "Write to ALL frames of the slot", AutoSize = true, Margin = new Padding(8, 6, 0, 0) };
        private readonly Label _fFrameInfo = new() { AutoSize = true, Margin = new Padding(4, 8, 0, 0), ForeColor = Color.DimGray };
        private readonly Label _importStatus = new() { AutoSize = true, Margin = new Padding(12, 9, 0, 0), ForeColor = Color.DimGray };

        // Every animation frame of the sprite picked above, as indexed PNG + BIN in a sprite_NNN folder (the same format the Sprite Viewer
        // dumps and the imports below read), taken from THIS tab's ROM copy so edits you have written show up in it.
        private void ExportThisSprite()
        {
            if (_rom == null) return;
            using var dlg = new FolderBrowserDialog { Description = $"Choose a folder: sprite_{SelectedSprite:D3} (PNG + BIN + manifest) is created inside it", UseDescriptionForTitle = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                int frames = SpriteDump.DumpSprite(_rom, SelectedSprite, dlg.SelectedPath);
                SpriteDump.ExportPalette(_rom, dlg.SelectedPath);
                _importStatus.ForeColor = Color.DimGray;
                _importStatus.Text = frames > 0 ? $"Exported {frames} frame(s) of sprite {SelectedSprite} to {Path.Combine(dlg.SelectedPath, SpriteDump.FolderName(SelectedSprite))}, plus obj_palette.gpl/.pal/.png (the colours to paint with)." : "That sprite has no frames to export.";
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        // Puts a sprite dump (Sprite Viewer > Dump...) back. One folder replaces the animations of the sprite chosen above -- it can be a
        // different sprite's dump, which copies its animations over; a whole dump root writes every sprite_NNN folder over sprite NNN.
        private void ImportFolder(bool all)
        {
            if (_rom == null) return;
            using var dlg = new FolderBrowserDialog
            {
                Description = all ? "Choose the dump folder that contains the sprite_NNN folders" : "Choose a sprite_NNN folder (or a dump folder to pick from)",
                UseDescriptionForTitle = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var dumps = SpriteDump.FindDumps(dlg.SelectedPath);
                if (dumps.Count == 0) { Fail("No sprite_NNN folder with a manifest.json was found there."); return; }
                string report;
                if (all)
                {
                    int done = 0;
                    foreach (var (id, folder) in dumps)
                    {
                        if (!FrameImport.HasRecord(_rom, id)) continue;
                        SpriteDump.ImportSprite(_rom, folder, id, _relocated);
                        done++;
                    }
                    report = $"Imported {done} sprite folder(s) over their own sprites.";
                }
                else
                {
                    var pick = dumps.FirstOrDefault(d => d.Id == SelectedSprite);
                    if (pick.Folder == null) pick = dumps[0];
                    report = SpriteDump.ImportSprite(_rom, pick.Folder, SelectedSprite, _relocated);
                }
                _session.NotifyChanged(this);
            _stripKey = (-1, -1); // frames changed: redraw the strip
                RefreshSlots();
                _importStatus.ForeColor = Color.DimGray;
                _importStatus.Text = report + " File > Save As (Ctrl+S) to keep it.";
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        private static string GroupName(int n)
        {
            int off = FrameImport.FirstGroup + n * FrameImport.GroupSize;
            // The ROM has no names for these: a group is just a slot in the sprite record. What we know of each comes from which code reads
            // it (walking reads group 0; the ability functions read the groups at +0x9C, +0x16C and +0x17C), so they are described by
            // that, not by game names that would need translating.
            string what = off switch
            {
                0x4C => "Walk / stand", 0x9C => "Ability pose A", 0x16C => "Ability pose B", 0x17C => "Ability pose C", _ => "Pose",
            };
            return $"Group {n}  -  {what}";
        }

        // Sprite frames are drawn on a checkerboard so their transparent pixels (palette index 0) are easy to tell apart.
        private static readonly Bitmap Checker = MakeChecker();
        private static Bitmap MakeChecker()
        {
            var b = new Bitmap(16, 16);
            using var g = Graphics.FromImage(b);
            g.Clear(Color.FromArgb(70, 70, 78));
            using var dark = new SolidBrush(Color.FromArgb(56, 56, 62));
            g.FillRectangle(dark, 0, 0, 8, 8);
            g.FillRectangle(dark, 8, 8, 8, 8);
            return b;
        }

        private static PictureBox NewPic(int side = 150) => new()
        {
            Size = new Size(side, side), SizeMode = PictureBoxSizeMode.Zoom, BackgroundImage = Checker, BackgroundImageLayout = ImageLayout.Tile, BorderStyle = BorderStyle.FixedSingle,
        };

        // Left: every sprite with a picture of it. Top right: group, the group's frames as a clickable strip, and Play.
        private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.LargeIcon, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.None };
        private readonly ImageList _thumbs = new() { ImageSize = new Size(40, 40), ColorDepth = ColorDepth.Depth32Bit };
        private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Find a sprite (id or party slot)" };
        private readonly FlowLayoutPanel _strip = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
        private readonly CheckBox _play = new() { Text = "Play", Appearance = Appearance.Button, AutoSize = false, Size = new Size(70, 28), TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(8, 0, 0, 0) };
        private readonly System.Windows.Forms.Timer _playTimer = new() { Interval = 160 };
        private readonly Label _title = new() { AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), Margin = new Padding(0, 2, 16, 0) };
        private int _playFrame;
        private CancellationTokenSource? _thumbCancel;

        private void BuildUi()
        {
            _fGroup.Items.Add(GroupName(0));
            _fGroup.SelectedIndex = 0;
            _fGroup.Width = 330;

            // left: search + thumbnails
            _list.LargeImageList = _thumbs;
            var left = new Panel { Dock = DockStyle.Left, Width = 250, Padding = new Padding(4) };
            left.Controls.Add(_list);
            left.Controls.Add(_search);
            _search.TextChanged += (_, _) => FillList();
            _list.SelectedIndexChanged += (_, _) =>
            {
                if (_fFilling || _list.SelectedItems.Count == 0) return;
                int id = (int)_list.SelectedItems[0].Tag!;
                int idx = _choices.FindIndex(c => c.Id == id);
                if (idx >= 0 && _fSprite.SelectedIndex != idx) _fSprite.SelectedIndex = idx;
            };

            // top: title, group, play; then the frame strip; then the options
            Label Small(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(0, 6, 4, 0) };
            var head = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, WrapContents = false, Padding = new Padding(4, 4, 4, 0) };
            head.Controls.AddRange([_title, Small("Group:"), _fGroup, _play]);
            var stripRow = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(4, 0, 4, 0) };
            var stripCap = new Label { Text = "Frames:", Dock = DockStyle.Left, Width = 56, TextAlign = ContentAlignment.MiddleLeft };
            stripRow.Controls.Add(_strip);
            stripRow.Controls.Add(stripCap);
            foreach (var c in new Control[] { _fMirror, _fPad, _fAuto, _fAllFrames }) c.Margin = new Padding(0, 4, 12, 0);
            var options = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, WrapContents = false, Padding = new Padding(4, 0, 4, 0) };
            options.Controls.AddRange([_fMirror, _fPad, _fAllFrames, _fAuto, Small("x:"), _fX, Small("y:"), _fY]);
            _fAuto.CheckedChanged += (_, _) => _fX.Enabled = _fY.Enabled = !_fAuto.Checked;
            _fX.Enabled = _fY.Enabled = false;

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 410, ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                var card = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill, Padding = new Padding(4) };
                int slot = i;
                _fCur[i] = NewPic(); _fNew[i] = NewPic();
                _fNew[i].Cursor = Cursors.Hand;
                _fNew[i].AllowDrop = true;
                _fNew[i].Click += (_, _) => ChooseSlotPng(slot);
                _fNew[i].DragEnter += (_, e) => e.Effect = DroppedPng(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
                _fNew[i].DragDrop += (_, e) => { if (DroppedPng(e) is { } path) { _fPng[slot] = path; RefreshSlots(); } };
                _fNew[i].Paint += (_, e) =>
                {
                    if (_fNew[slot].Image != null) return;
                    TextRenderer.DrawText(e.Graphics, slot == 3 && _fMirror.Checked ? "Left, flipped" : "Drop a PNG here\nor click", Font,
                        _fNew[slot].ClientRectangle, Color.Gainsboro, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                };
                _fLbl[i] = new Label { AutoSize = true, MaximumSize = new Size(240, 0), ForeColor = Color.DimGray };
                var clear = new Button { Text = "Clear", AutoSize = true };
                clear.Click += (_, _) => { _fPng[slot] = null; RefreshSlots(); };
                card.Controls.Add(new Label { Text = FrameImport.SlotNames[i], Font = new Font(Font, FontStyle.Bold), AutoSize = true });
                card.Controls.AddRange([_fCur[i], _fNew[i], clear, _fLbl[i]]);
                grid.Controls.Add(card, i, 0);
            }
            _fFrame.ValueChanged += (_, _) => { if (!_fFilling) { Array.Clear(_fPng); RefreshSlots(); } };
            _fAllFrames.CheckedChanged += (_, _) => RefreshSlots();
            _fMirror.CheckedChanged += (_, _) => RefreshSlots();
            _fPad.CheckedChanged += (_, _) => RefreshSlots();
            _fSprite.SelectedIndexChanged += (_, _) => { if (_fFilling) return; Array.Clear(_fPng); _fFrame.Value = 0; FillGroupCombo(); RefreshSlots(); SyncList(); };
            _fGroup.SelectedIndexChanged += (_, _) => { if (_fFilling) return; Array.Clear(_fPng); _fFrame.Value = 0; RefreshSlots(); };
            _play.CheckedChanged += (_, _) => { _playFrame = CurrentFrame; _playTimer.Enabled = _play.Checked; _play.Text = _play.Checked ? "Stop" : "Play"; if (!_play.Checked) RefreshSlots(); };
            _playTimer.Tick += (_, _) => PlayStep();

            var write = new Button { Text = "Write frames", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            write.Click += (_, _) => WriteFrames();
            var tips = new ToolTip { AutoPopDelay = 15000 };
            tips.SetToolTip(write, "Writes your PNGs over the chosen frame (or every frame of the direction). File > Save As (Ctrl+S) to keep it.");
            tips.SetToolTip(_fPad, "The game reserves sprite memory for the stock size shown under each direction. Smaller PNGs are centred in it.");
            tips.SetToolTip(_fMirror, "The game draws Right as Left flipped; leave this on and give only a Left PNG.");
            tips.SetToolTip(_fAllFrames, "Use the same picture for every frame of the direction (fine for still poses).");
            tips.SetToolTip(_fAuto, "Place the frame where the stock one sits. Turn off to set the x/y offset yourself.");
            for (int i = 0; i < 4; i++) tips.SetToolTip(_fNew[i], "Drop a PNG here or click to choose one. Paint transparent areas with palette colour 0.");
            var export = new Button { Text = "Export .bin files", AutoSize = true };
            export.Click += (_, _) => ConvertToBin();
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(4) };
            var importOne = new Button { Text = "Import sprite folder into this sprite...", AutoSize = true };
            importOne.Click += (_, _) => ImportFolder(all: false);
            var importAll = new Button { Text = "Import whole dump...", AutoSize = true };
            importAll.Click += (_, _) => ImportFolder(all: true);
            var exportSprite = new Button { Text = "Export this sprite's PNGs...", AutoSize = true };
            exportSprite.Click += (_, _) => ExportThisSprite();
            bar.Controls.AddRange([write, export, exportSprite, importOne, importAll, _importStatus]);

            var page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = new Size(760, 720) };
            page.Controls.Add(bar); page.Controls.Add(grid); page.Controls.Add(options); page.Controls.Add(stripRow); page.Controls.Add(head);
            page.VisibleChanged += (_, _) => { if (page.Visible) page.AutoScrollPosition = new Point(0, 0); };
            _content.Controls.Add(page);
            _content.Controls.Add(new Splitter { Dock = DockStyle.Left, Width = 4 });
            _content.Controls.Add(left);
        }

        private static string? DroppedPng(DragEventArgs e) =>
            e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && files[0].EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? files[0] : null;

        /// <summary>Animates the four directions of the group in the "ROM" boxes. Stops on its own when the sprite or group changes.</summary>
        private void PlayStep()
        {
            if (_rom == null) return;
            int frames = Math.Max(1, (int)_fFrame.Maximum + 1);
            _playFrame = (_playFrame + 1) % frames;
            for (int i = 0; i < 4; i++)
            {
                var old = _fCur[i].Image;
                _fCur[i].Image = FrameImport.RenderSlot(_rom, SelectedSprite, GroupOffset + i * 4, _playFrame) ?? FrameImport.RenderSlot(_rom, SelectedSprite, GroupOffset + i * 4, 0);
                old?.Dispose();
            }
            HighlightStrip(_playFrame);
        }

        /// <summary>One thumbnail per frame of the group (the Down direction, or the first direction that has frames). Click one to edit it.</summary>
        private void FillStrip(int frames)
        {
            foreach (Control c in _strip.Controls) c.Dispose();
            _strip.Controls.Clear();
            if (_rom == null) return;
            int slot = Enumerable.Range(0, 4).FirstOrDefault(i => FrameImport.SlotFrameCount(_rom, SelectedSprite, GroupOffset + i * 4) > 0);
            for (int f = 0; f < frames; f++)
            {
                int frame = f;
                var pic = NewPic(56);
                pic.Margin = new Padding(0, 0, 4, 0);
                pic.Cursor = Cursors.Hand;
                pic.Image = FrameImport.RenderSlot(_rom, SelectedSprite, GroupOffset + slot * 4, f);
                pic.Click += (_, _) => { if (_play.Checked) _play.Checked = false; _fFrame.Value = frame; };
                _strip.Controls.Add(pic);
            }
            HighlightStrip(CurrentFrame);
        }

        private void HighlightStrip(int frame)
        {
            for (int i = 0; i < _strip.Controls.Count; i++)
                if (_strip.Controls[i] is PictureBox p) { p.BorderStyle = i == frame ? BorderStyle.Fixed3D : BorderStyle.FixedSingle; p.Padding = new Padding(i == frame ? 2 : 0); p.BackColor = i == frame ? Color.DodgerBlue : SystemColors.Control; }
        }

        private int SelectedSprite => _fSprite.SelectedItem is SpriteChoice c ? c.Id : 0;

        private sealed record SpriteChoice(int Id, string Label) { public override string ToString() => Label; }

        private List<SpriteChoice> _choices = [];

        /// <summary>The sprite list on the left, filtered by the search box. Thumbnails are drawn in the background.</summary>
        private void FillList()
        {
            string q = _search.Text.Trim();
            bool was = _fFilling;
            _fFilling = true;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var c in _choices.Where(c => q.Length == 0 || c.Label.Contains(q, StringComparison.OrdinalIgnoreCase)))
                _list.Items.Add(new ListViewItem(c.Label.Replace(" - ", "\n"), _thumbs.Images.ContainsKey(c.Id.ToString()) ? c.Id.ToString() : "") { Tag = c.Id, ToolTipText = c.Label, ImageKey = c.Id.ToString() });
            _list.EndUpdate();
            _fFilling = was;
            SyncList();
        }

        private void SyncList()
        {
            bool was = _fFilling;
            _fFilling = true;
            foreach (ListViewItem item in _list.Items)
            {
                bool on = (int)item.Tag! == SelectedSprite;
                item.Selected = on;
                if (on) item.EnsureVisible();
            }
            _fFilling = was;
            int sprite = SelectedSprite;
            _title.Text = $"Sprite {sprite}";
        }

        private async void FillThumbnails()
        {
            if (_rom == null) return;
            _thumbCancel?.Cancel();
            var cancel = _thumbCancel = new CancellationTokenSource();
            var copy = ROM.FromBytes(_rom.ToArray());
            var ids = _choices.Select(c => c.Id).ToList();
            var thumbs = await Task.Run(() =>
            {
                var result = new List<(int Id, Bitmap Image)>();
                foreach (int id in ids)
                {
                    if (cancel.IsCancellationRequested) break;
                    using var frame = FrameImport.RenderSlot(copy, id, FrameImport.FirstGroup, 0);
                    var thumb = new Bitmap(40, 40);
                    if (frame != null)
                    {
                        using var g = Graphics.FromImage(thumb);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                        float scale = Math.Min(40f / frame.Width, 40f / frame.Height);
                        scale = scale >= 1 ? MathF.Floor(scale) : scale;
                        int w = (int)(frame.Width * scale), h = (int)(frame.Height * scale);
                        g.DrawImage(frame, (40 - w) / 2, (40 - h) / 2, w, h);
                    }
                    result.Add((id, thumb));
                }
                return result;
            });
            if (cancel.IsCancellationRequested || IsDisposed) { foreach (var (_, b) in thumbs) b.Dispose(); return; }
            _thumbs.Images.Clear();
            foreach (var (id, image) in thumbs) _thumbs.Images.Add(id.ToString(), image);
            _list.Invalidate();
        }

        // Only sprites that already have a record with at least one real frame; labelled with the party slot / display row that uses them.
        /// <summary>Opens the editor on this sprite (the Characters page's "Edit frames").</summary>
        public void SelectSprite(int id)
        {
            int idx = _choices.FindIndex(c => c.Id == id);
            if (idx >= 0 && _fSprite.SelectedIndex != idx) _fSprite.SelectedIndex = idx;
        }

        private void FillSpriteCombo(int keep = -1)
        {
            if (_rom == null) return;
            int want = keep >= 0 ? keep : SelectedSprite;
            var party = RosterTables.ReadDefaultParty(_rom);
            var rows = RosterTables.ReadDisplayRows(_rom);
            var choices = new List<SpriteChoice>();
            for (int id = 0; id < 256; id++)
            {
                if (!FrameImport.HasRecord(_rom, id)) continue;
                var users = new List<string>();
                for (int slot = 0; slot < party.Count; slot++)
                    if (party[slot].DisplayIndex < rows.Count && rows[party[slot].DisplayIndex].SpriteId == id) users.Add($"party slot {slot}");
                var formRows = Enumerable.Range(0, rows.Count).Where(r => rows[r].SpriteId == id).ToList();
                if (users.Count == 0 && formRows.Count > 0) users.Add("form " + string.Join(",", formRows.Take(3)));
                choices.Add(new SpriteChoice(id, users.Count > 0 ? $"{id} - {string.Join(", ", users)}" : $"{id}"));
            }
            // The party's sprites first (in party-slot order), then the other playable forms, then everything else by id.
            int Rank(SpriteChoice c)
            {
                for (int slot = 0; slot < party.Count; slot++)
                    if (party[slot].DisplayIndex < rows.Count && rows[party[slot].DisplayIndex].SpriteId == c.Id) return slot;
                return Enumerable.Range(0, rows.Count).Any(r => rows[r].SpriteId == c.Id) ? 100 : 1000;
            }
            choices = [.. choices.OrderBy(Rank).ThenBy(c => c.Id)];
            bool thumbsStale =!choices.Select(c => c.Id).SequenceEqual(_choices.Select(c => c.Id)) || keep < 0;
            _fFilling = true;
            _fSprite.Items.Clear();
            foreach (var c in choices) _fSprite.Items.Add(c);
            int idx = choices.FindIndex(c => c.Id == want);
            _fSprite.SelectedIndex = idx >= 0 ? idx : 0;   // no earlier pick: the first sprite in the list
            _fFilling = false;
            _choices = choices;
            FillList();
            if (thumbsStale) FillThumbnails();
            FillGroupCombo();
        }

        // The groups this sprite's record can hold frames in: exactly the groups "Dump all sprites" walks for it, so what you can export is what you can edit.
        private void FillGroupCombo()
        {
            if (_rom == null || _fSprite.SelectedItem == null) return;
            int keep = Math.Max(0, _fGroup.SelectedIndex);
            int groups = Math.Max(1, FrameImport.GroupsIn(_rom, SelectedSprite));
            bool was = _fFilling;
            _fFilling = true;
            _fGroup.Items.Clear();
            for (int n = 0; n < groups; n++) _fGroup.Items.Add(GroupName(n));
            _fGroup.SelectedIndex = Math.Min(keep, groups - 1);
            _fFilling = was;
        }

        private bool _fFilling;
        private (int Sprite, int Group) _stripKey = (-1, -1);

        private int CurrentFrame => (int)_fFrame.Value;

        private int GroupOffset => FrameImport.FirstGroup + Math.Max(0, _fGroup.SelectedIndex) * FrameImport.GroupSize;

        private void ChooseSlotPng(int slot)
        {
            using var dlg = new OpenFileDialog { Filter = "PNG (*.png)|*.png" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _fPng[slot] = dlg.FileName;
            RefreshSlots();
        }

        private static Image? Load(string? path) { try { return path == null ? null : Image.FromStream(new MemoryStream(File.ReadAllBytes(path))); } catch { return null; } }

        private void RefreshSlots()
        {
            if (_rom == null) return;
            int sprite = SelectedSprite;
            int frames = 0;
            for (int i = 0; i < 4; i++) frames = Math.Max(frames, FrameImport.SlotFrameCount(_rom, sprite, GroupOffset + i * 4));
            if (_fFrame.Maximum != Math.Max(0, frames - 1))
            {
                bool was = _fFilling;
                _fFilling = true;
                _fFrame.Maximum = Math.Max(0, frames - 1);
                if (_fFrame.Value > _fFrame.Maximum) _fFrame.Value = _fFrame.Maximum;
                _fFilling = was;
            }
            _fFrameInfo.Text = frames == 0 ? "(no frames)" : $"of {frames}";
            if (_play.Checked) _play.Checked = false;
            if (_strip.Controls.Count != frames || _stripKey != (sprite, GroupOffset)) { _stripKey = (sprite, GroupOffset); FillStrip(frames); }
            else HighlightStrip(CurrentFrame);
            for (int i = 0; i < 4; i++)
            {
                var cur = FrameImport.RenderSlot(_rom, sprite, GroupOffset + i * 4, CurrentFrame);
                _fCur[i].Image?.Dispose();
                _fCur[i].Image = cur;
                string curSize = cur == null ? "empty in ROM" : $"stock {cur.Width}x{cur.Height}";
                bool mirrored = i == 3 && _fMirror.Checked;
                Image? mine = Load(mirrored ? _fPng[2] : _fPng[i]);
                if (mine != null && mirrored) mine.RotateFlip(RotateFlipType.RotateNoneFlipX);
                _fNew[i].Image = mine;
                string note = curSize;
                bool fits = mine != null && cur != null && mine.Width <= cur.Width && mine.Height <= cur.Height;
                if (mine != null && cur != null && (mine.Width != cur.Width || mine.Height != cur.Height))
                    note += _fPad.Checked && fits ? $"  |  yours {mine.Width}x{mine.Height} -> will be padded to {cur.Width}x{cur.Height}" : $"  |  yours {mine.Width}x{mine.Height} - SIZE DIFFERS (risky)";
                else if (mine != null) note += $"  |  yours {mine.Width}x{mine.Height}";
                _fLbl[i].Text = note;
                _fLbl[i].ForeColor = note.Contains("differs", StringComparison.OrdinalIgnoreCase) || note.Contains("DIFFERS") ? Color.Firebrick : Color.DimGray;
            }
        }

        private List<(int Slot, FrameImport.Converted C)>? ConvertChosen(out string report)
        {
            report = "";
            var list = new List<(int, FrameImport.Converted)>();
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i == 3 && _fMirror.Checked) continue; // written together with Left
                    if (_fPng[i] == null) continue;
                    var c = FrameImport.FromPng(_fPng[i]!, _rom!, requireShape: !_fPad.Checked);
                    // Pad to the frame that is in the ROM now (Left's frame for a mirrored Right), so the sprite-memory size stays as the game expects.
                    if (_fPad.Checked)
                    {
                        using var stock = FrameImport.RenderSlot(_rom!, SelectedSprite, GroupOffset + i * 4, CurrentFrame);
                        if (stock != null && (stock.Width != c.Width || stock.Height != c.Height)) c = FrameImport.PadTo(c, stock.Width, stock.Height);
                    }
                    list.Add((i, c));
                    report += $"{FrameImport.SlotNames[i]}: {Path.GetFileName(_fPng[i])} {c.Width}x{c.Height} - {c.Report}\n";
                }
            }
            catch (Exception ex) { Fail(ex.Message); return null; }
            if (list.Count == 0) { Fail("Choose at least one PNG first."); return null; }
            return list;
        }

        private void ConvertToBin()
        {
            if (_rom == null) return;
            var all = ConvertChosen(out string report);
            if (all == null) return;
            foreach (var (slot, c) in all)
                File.WriteAllBytes(Path.ChangeExtension(_fPng[slot]!, ".bin"), FrameImport.ToBlob(c));
        }

        private void WriteFrames()
        {
            if (_rom == null) return;
            var all = ConvertChosen(out string report);
            if (all == null) return;
            int sprite = SelectedSprite, start = GroupOffset;
            try
            {
                if (_relocated.Add(sprite)) FrameImport.RelocateRecord(_rom, sprite);
                foreach (var (slot, c) in all)
                {
                    sbyte x = _fAuto.Checked ? (sbyte)(-(c.Width - 16) / 2) : (sbyte)_fX.Value, y = _fAuto.Checked ? (sbyte)(8 - c.Height) : (sbyte)_fY.Value;
                    FrameImport.WriteFrame(_rom, sprite, start + slot * 4, c, x, y, slot == 2 && _fMirror.Checked ? start + 12 : -1, _fAllFrames.Checked ? -1 : CurrentFrame);
                }
            }
            catch (Exception ex) { Fail(ex.Message); return; }
            _session.NotifyChanged(this);
            _stripKey = (-1, -1); // frames changed: redraw the strip
            RefreshSlots();
            var problems = FrameImport.Inspect(_rom, sprite).Split('\n').Where(l => l.Contains($"group +0x{start:X}") && (l.Contains("PROBLEM") || l.Contains("BAD") || l.Contains("MISALIGNED"))).ToList();
            if (problems.Count > 0) Fail("Written, but the game may not accept this group:\n\n" + string.Join("\n", problems));
        }

    }
}
