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
            _content.Controls.Add(new ApplyBar(session));
            Controls.Add(_content);
            Controls.Add(_noRom);
            session.Loaded += OnLoaded;
            session.Changed += source => { if (!ReferenceEquals(source, this) && _content.Visible) { FillSpriteCombo(); RefreshSlots(); } };
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
        private readonly Label _fInfo = new() { Dock = DockStyle.Top, Height = 74, Padding = new Padding(4) };
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
                RefreshSlots();
                _importStatus.ForeColor = Color.DimGray;
                _importStatus.Text = report + " Apply to DBZKit / Save ROM As to keep it.";
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        private static string GroupName(int n)
        {
            int off = FrameImport.FirstGroup + n * FrameImport.GroupSize;
            string what = off switch
            {
                0x4C => "walk / stand (first group)", 0x9C => "Ki Blast, Big Bang, Burning, Masenko, Scatter Shot", 0x16C => "Kamehameha, Special Beam, Sword Blast", 0x17C => "Spirit Bomb", 0x18C => "extra frames (idle / blink?)", _ => "other pose",
            };
            return $"Group {n}  (+{off}, 0x{off:X}) - {what}";
        }

        private static PictureBox NewPic() => new() { Size = new Size(160, 160), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(255, 0, 255), BorderStyle = BorderStyle.FixedSingle };

        private void BuildUi()
        {
            var page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = new Size(900, 800) };
            _fInfo.Text = "Pick a sprite and a 4-frame group. Each direction shows what is in the ROM now (top) and your PNG (bottom). Give PNGs only for the directions you want to change. " +
                          "Use the SAME size as the stock frame (shown under each picture) -- the game reserves sprite memory for stock sizes. Your transparent pink is fine (palette index 0). " +
                          "Frames are stored as the game's normal compressed format.";
            // the group list is rebuilt from the picked sprite's own record (FillGroupCombo); it starts on the first group
            _fGroup.Items.Add(GroupName(0));
            _fGroup.SelectedIndex = 0;

            // Two rows: sprite over group on the left, the options stacked two tall to their right (no scrollbar).
            var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 74, ColumnCount = 3, RowCount = 2, Padding = new Padding(4) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            Label Cap(string t) => new() { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) };
            _fSprite.Anchor = _fGroup.Anchor = AnchorStyles.Left;
            top.Controls.Add(Cap("Sprite id:"), 0, 0); top.Controls.Add(_fSprite, 1, 0);
            top.Controls.Add(Cap("Group:"), 0, 1); top.Controls.Add(_fGroup, 1, 1);
            FlowLayoutPanel Line(params Control[] c) { var f = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(16, 0, 0, 0) }; f.Controls.AddRange(c); return f; }
            foreach (var c in new Control[] { _fMirror, _fPad, _fAuto, _fAllFrames }) c.Margin = new Padding(0, 4, 12, 0);
            Label Small(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(0, 6, 4, 0) };
            top.Controls.Add(Line(_fMirror, _fPad, _fAuto), 2, 0);
            top.Controls.Add(Line(Small("Frame:"), _fFrame, _fFrameInfo, _fAllFrames, Small("x:"), _fX, Small("y:"), _fY), 2, 1);

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 420, ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                var card = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill, Padding = new Padding(4) };
                int slot = i;
                _fCur[i] = NewPic(); _fNew[i] = NewPic();
                _fLbl[i] = new Label { AutoSize = true, MaximumSize = new Size(240, 0), ForeColor = Color.DimGray };
                var choose = new Button { Text = "PNG...", AutoSize = true };
                choose.Click += (_, _) => ChooseSlotPng(slot);
                var clear = new Button { Text = "Clear", AutoSize = true };
                clear.Click += (_, _) => { _fPng[slot] = null; RefreshSlots(); };
                var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
                row.Controls.AddRange([choose, clear]);
                card.Controls.Add(new Label { Text = FrameImport.SlotNames[i], Font = new Font(Font, FontStyle.Bold), AutoSize = true });
                                card.Controls.AddRange([_fCur[i], _fNew[i], row, _fLbl[i]]);
                grid.Controls.Add(card, i, 0);
            }
            _fFrame.ValueChanged += (_, _) => { if (!_fFilling) { Array.Clear(_fPng); RefreshSlots(); } };
            _fAllFrames.CheckedChanged += (_, _) => RefreshSlots();
            _fMirror.CheckedChanged += (_, _) => RefreshSlots();
            _fPad.CheckedChanged += (_, _) => RefreshSlots();
            _fSprite.SelectedIndexChanged += (_, _) => { if (_fFilling) return; Array.Clear(_fPng); _fFrame.Value = 0; FillGroupCombo(); RefreshSlots(); };
            _fGroup.SelectedIndexChanged += (_, _) => { if (_fFilling) return; Array.Clear(_fPng); _fFrame.Value = 0; RefreshSlots(); };

            var write = new Button { Text = "Write into ROM copy", AutoSize = true };
            write.Click += (_, _) => WriteFrames();
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

            page.Controls.Add(bar); page.Controls.Add(grid); page.Controls.Add(top); page.Controls.Add(_fInfo);
            page.Scroll += (_, _) => { };
            page.VisibleChanged += (_, _) => { if (page.Visible) page.AutoScrollPosition = new Point(0, 0); };
            _content.Controls.Add(page);
        }

        private int SelectedSprite => _fSprite.SelectedItem is SpriteChoice c ? c.Id : 0;

        private sealed record SpriteChoice(int Id, string Label) { public override string ToString() => Label; }

        // Only sprites that already have a record with at least one real frame; labelled with the party slot / display row that uses them.
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
                if (users.Count == 0 && formRows.Count > 0) users.Add("display row " + string.Join(",", formRows.Take(3)));
                choices.Add(new SpriteChoice(id, users.Count > 0 ? $"{id} - {string.Join(", ", users)}" : $"{id}"));
            }
            _fFilling = true;
            _fSprite.Items.Clear();
            foreach (var c in choices) _fSprite.Items.Add(c);
            int idx = choices.FindIndex(c => c.Id == want);
            _fSprite.SelectedIndex = idx >= 0 ? idx : 0;   // no earlier pick: the first sprite in the list
            _fFilling = false;
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
            for (int i = 0; i < 4; i++)
            {
                var cur = FrameImport.RenderSlot(_rom, sprite, GroupOffset + i * 4, CurrentFrame);
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
            RefreshSlots();
            var problems = FrameImport.Inspect(_rom, sprite).Split('\n').Where(l => l.Contains($"group +0x{start:X}") && (l.Contains("PROBLEM") || l.Contains("BAD") || l.Contains("MISALIGNED"))).ToList();
            if (problems.Count > 0) Fail("Written, but the game may not accept this group:\n\n" + string.Join("\n", problems));
        }

    }
}
