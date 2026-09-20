using System.Globalization;
using System.Reflection;
using DrGero.Engine;
using DrGero.IO;
using DrGero.Rendering;

namespace DBZKit
{
    /// <summary>
    /// Roster / ability / map-table / engine-patch editor, hosted as a tab in DBZKit. Works on a private copy of the ROM:
    /// "Apply to DBZKit" hands the edited bytes back to the main window, "Save ROM As..." writes them to disk.
    /// See Roster-and-Abilities.md and Engine-Notes.md.
    /// </summary>
    internal sealed class EngineToolsPanel : UserControl
    {
        private static readonly DrGero.Config.Game IconGame = new() { OBJPaletteOffset = 0x1DA6C8, CharacterSpriteIndexOffset = 0x3B4E74 };

        private readonly EditSession _session;
        private ROM? _rom => _session.Rom;
        private bool _filling; // suppresses selection/preview work while grids are being (re)populated

        private readonly Label _noRom = new() { Text = "Open a ROM (File > Open Rom) to use the engine tools.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        private readonly Panel _content = new() { Dock = DockStyle.Fill, Visible = false };

        private readonly DataGridView _party = NewGrid(), _rows = NewGrid(), _abil = NewGrid();
        private readonly PictureBox _preview = new() { Size = new Size(180, 180), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black, Dock = DockStyle.Right };
        private readonly Label _rowsInfo = new() { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
        private readonly Label _abilInfo = new() { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
        private readonly Label _mapInfo = new() { AutoSize = true, Padding = new Padding(4) };
        private readonly FlowLayoutPanel _patchPanel = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(6) };
        private readonly NumericUpDown _mapSource = Num(0, 509, 0), _mapZone = Num(0, 254, 90), _mapArea = Num(0, 254, 1), _mapVar = Num(0, 254, 1);
        private readonly Dictionary<int, Image?> _iconCache = [];

        public EngineToolsPanel(EditSession session)
        {
            _session = session;
            DoubleBuffered = true;
            Dock = DockStyle.Fill;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildPartyTab());
            tabs.TabPages.Add(BuildRowsTab());
            tabs.TabPages.Add(BuildAbilitiesTab());
            tabs.TabPages.Add(BuildEngineTab());

            _content.Controls.Add(tabs);
            _content.Controls.Add(new ApplyBar(session));
            Controls.Add(_content);
            Controls.Add(_noRom);
            session.Loaded += OnSessionLoaded;
            // A sprite-frame or sound edit elsewhere: only the display-row previews depend on it.
            session.Changed += source => { if (!ReferenceEquals(source, this)) { _iconCache.Clear(); _rows.Invalidate(); } };
        }

        private void OnSessionLoaded()
        {
            _iconCache.Clear();
            if (_rom == null) { _content.Visible = false; _noRom.Visible = true; return; }
            bool supported = RosterTables.IsSupportedRom(_rom);
            _noRom.Text = "This doesn't look like the US ROM these tables were mapped from, so the editor is disabled.";
            _content.Visible = supported;
            _noRom.Visible = !supported;
            if (supported) RefreshAll();
        }

        // ---- helpers -------------------------------------------------------------------------------------------
        private static DataGridView NewGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                // Auto-sizing every column on every Rows.Add was the main source of lag; size once after each fill instead.
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                RowTemplate = { Height = 22 },
            };
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(g, true);
            g.DataError += (_, e) => e.ThrowException = false;
            return g;
        }

        // The row the user means: the current row, else the first selected one.
        private static DataGridViewRow? Picked(DataGridView g) =>
            g.CurrentRow ?? g.SelectedRows.OfType<DataGridViewRow>().OrderBy(r => r.Index).FirstOrDefault();

        private static NumericUpDown Num(int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, Width = 64 };

        private static void AddCol(DataGridView g, string name, bool readOnly = false, int width = 90) =>
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = name, ReadOnly = readOnly, Width = width });

        private static string Hex(uint v) => "0x" + v.ToString("X8");
        private static string Hex(byte v) => "0x" + v.ToString("X2");

        private static bool TryHex(object? cell, out uint value)
        {
            string s = (cell?.ToString() ?? "").Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
            return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryInt(object? cell, int min, int max, out int value)
        {
            value = 0;
            return int.TryParse(cell?.ToString()?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
        }

        private static string AbilityName(int i) => i < RosterTables.OriginalAbilityNames.Length ? RosterTables.OriginalAbilityNames[i] : $"Custom ability {i}";

        private void Fail(string msg) => MessageBox.Show(this, msg, "Can't apply", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        // Setting FirstDisplayedScrollingRowIndex throws ("No room is available to display rows") when the grid is hidden or
        // too small; that used to surface as an error dialog right after a successful add.
        private static void ScrollIntoView(DataGridView g, int index)
        {
            if (!g.Visible || g.DisplayedRowCount(false) <= 0) return;
            try { g.FirstDisplayedScrollingRowIndex = Math.Max(0, index - 3); }
            catch (InvalidOperationException) { /* nothing to scroll */ }
        }

        // Populates a grid with layout and selection events suspended, then sizes columns once.
        private void Populate(DataGridView g, Action fill)
        {
            _filling = true;
            g.SuspendLayout();
            try { fill(); }
            finally
            {
                g.ResumeLayout();
                _filling = false;
            }
        }

        // ---- default party -------------------------------------------------------------------------------------
        private TabPage BuildPartyTab()
        {
            var page = new TabPage("Default party");
            foreach (var c in new[] { "Slot", "Display row", "Sprite", "Max HP", "Max EP", "Str", "Pow", "End", "EXP", "Flags" }) AddCol(_party, c, c is "Slot" or "Sprite", c == "Flags" ? 100 : 80);
            for (int i = 1; i <= 4; i++) _party.Columns.Add(new DataGridViewComboBoxColumn { Name = $"Ability {i}", HeaderText = $"Ability {i}", FlatStyle = FlatStyle.Flat, Width = 170 });

            var info = new Label
            {
                Dock = DockStyle.Top, Height = 58, Padding = new Padding(4),
                Text = "The 6 party slots a NEW GAME starts with (existing saves keep their own copy). Str/Pow/End are scaled: 256 = 1.0. " +
                       "A slot's abilities stay with the slot, but its Display row (sprite/portrait/forms) can be swapped in-game by script. " +
                       "6 slots is a hard engine limit; widen the pool with Display rows instead."
            };
            var apply = new Button { Text = "Apply to ROM copy", Dock = DockStyle.Bottom, Height = 32 };
            apply.Click += (_, _) => ApplyParty();
            page.Controls.Add(_party); page.Controls.Add(info); page.Controls.Add(apply);
            return page;
        }

        private void FillParty()
        {
            var party = RosterTables.ReadDefaultParty(_rom!);
            var rows = RosterTables.ReadDisplayRows(_rom!);
            int abilCount = RosterTables.AbilityCount(_rom!);
            var items = Enumerable.Range(0, abilCount).Select(i => $"{i} - {AbilityName(i)}").ToArray();
            Populate(_party, () =>
            {
                foreach (DataGridViewComboBoxColumn col in _party.Columns.OfType<DataGridViewComboBoxColumn>())
                { col.Items.Clear(); col.Items.AddRange(items); }
                _party.Rows.Clear();
                for (int i = 0; i < party.Count; i++)
                {
                    var s = party[i];
                    int r = _party.Rows.Add(i, s.DisplayIndex, s.DisplayIndex < rows.Count ? rows[s.DisplayIndex].SpriteId : "?", s.MaxHp, s.MaxEp, s.Str, s.Pow, s.End, s.Exp, Hex(s.Flags));
                    for (int a = 0; a < 4; a++)
                        _party.Rows[r].Cells[10 + a].Value = s.Abilities[a] < abilCount ? items[s.Abilities[a]] : null;
                }
            });
        }

        private void ApplyParty()
        {
            if (_rom == null) return;
            var updated = new List<PartySlot>();
            var existing = RosterTables.ReadDefaultParty(_rom);
            int abilCount = RosterTables.AbilityCount(_rom), rowCount = RosterTables.DisplayRowCount(_rom);
            foreach (DataGridViewRow r in _party.Rows)
            {
                string at = $"Slot {r.Cells[0].Value}";
                if (!TryInt(r.Cells[1].Value, 0, rowCount - 1, out int disp)) { Fail($"{at}: Display row must be 0-{rowCount - 1}."); return; }
                if (!TryInt(r.Cells[3].Value, 1, 65535, out int hp) || !TryInt(r.Cells[4].Value, 0, 65535, out int ep)) { Fail($"{at}: HP/EP must be 1-65535."); return; }
                if (!TryInt(r.Cells[5].Value, 0, 65535, out int str) || !TryInt(r.Cells[6].Value, 0, 65535, out int pow) || !TryInt(r.Cells[7].Value, 0, 65535, out int end)) { Fail($"{at}: Str/Pow/End must be 0-65535."); return; }
                if (!uint.TryParse(r.Cells[8].Value?.ToString(), out uint exp)) { Fail($"{at}: EXP must be a number."); return; }
                if (!TryHex(r.Cells[9].Value, out uint flags)) { Fail($"{at}: Flags must be hex."); return; }
                var abil = new byte[4];
                for (int a = 0; a < 4; a++)
                {
                    string? v = r.Cells[10 + a].Value?.ToString();
                    if (v == null || !int.TryParse(v.Split(' ')[0], out int idx) || idx >= abilCount) { Fail($"{at}: pick ability {a + 1}."); return; }
                    abil[a] = (byte)idx;
                }
                updated.Add(new PartySlot { CurrentHp = (ushort)hp, MaxHp = (ushort)hp, CurrentEp = (ushort)ep, MaxEp = (ushort)ep, DisplayIndex = (byte)disp, AbilityIndex = existing[updated.Count].AbilityIndex, Str = (ushort)str, Pow = (ushort)pow, End = (ushort)end, Exp = exp, Flags = flags, Abilities = abil });
            }
            for (int i = 0; i < updated.Count; i++) RosterTables.WriteDefaultSlot(_rom, i, updated[i]);
            FillParty(); // only the party grid changes
            var problems = new List<string>();
            var rowList = RosterTables.ReadDisplayRows(_rom);
            for (int i = 0; i < updated.Count; i++) problems.AddRange(AbilityRequirements.Check(_rom, updated[i], i, rowList));
            if (problems.Count > 0)
                MessageBox.Show(this, "Applied, but these combinations will probably crash or garble in-game (the ability's animation reads sprite frames the form doesn't have):\n\n" + string.Join("\n", problems.Take(12)) +
                    "\n\n(Heuristic: the untouched ROM trips this for Slot 0 Kamehameha and Slot 3 Sword Blast too, so treat a warning as a reason to test in an emulator, not proof of a crash.)\n\nRemember: default-party edits only affect a NEW GAME, and nothing is on disk until Save ROM As.", "Ability / sprite mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ---- display rows --------------------------------------------------------------------------------------
        private TabPage BuildRowsTab()
        {
            var page = new TabPage("Display rows (characters / forms)");
            foreach (var c in new[] { "Row", "Flags", "Sprite", "Portrait", "Unk3", "Transformed", "Detransformed", "MinFrames", "MaxFrames", "Transformed2", "Unk9", "UnkA", "UnkC", "VTable" })
                AddCol(_rows, c, c == "Row", c is "UnkC" or "VTable" ? 100 : c.Length > 8 ? 100 : 68);
            _rows.SelectionChanged += (_, _) => { if (!_filling) UpdatePreview(); };

            var apply = new Button { Text = "Apply to ROM copy", AutoSize = true };
            apply.Click += (_, _) => { if (ApplyRows()) FillRows(); };
            var add = new Button { Text = "Add row (copy of selected)", AutoSize = true };
            add.Click += (_, _) => AddRow();
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(4) };
            var transform = new Button { Text = "Add transformation ability...", AutoSize = true };
            transform.Click += (_, _) => AddTransformationAbility();
            buttons.Controls.AddRange([apply, add, transform]);

            page.Controls.Add(_rows); page.Controls.Add(_preview); page.Controls.Add(_rowsInfo); page.Controls.Add(buttons);
            return page;
        }

        private void FillRows()
        {
            var rows = RosterTables.ReadDisplayRows(_rom!);
            int keep = Picked(_rows)?.Index ?? 0;
            Populate(_rows, () =>
            {
                _rows.Rows.Clear();
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    _rows.Rows.Add(i, Hex(r.Flags), r.SpriteId, r.PortraitIndex, r.Unk3, r.TransformedIndex, r.DetransformedIndex, r.MinFrames, r.MaxFrames, r.Transformed2, r.Unk9, r.UnkA, Hex(r.UnkC), Hex(r.VTable));
                }
            });
            if (_rows.Rows.Count > 0) { _rows.ClearSelection(); _rows.Rows[Math.Min(keep, _rows.Rows.Count - 1)].Selected = true; }
            _rowsInfo.Text = $"{rows.Count} of {RosterTables.MaxDisplayRows} rows, table at file 0x{RosterTables.DisplayTableAddress(_rom!):X} " +
                             (RosterTables.DisplayRelocated(_rom!) ? "(relocated)." : "(original -- the first added row relocates it and repoints 18 code literals).") +
                             " Each row is one character FORM; a sprite must exist in the sprite table and a portrait in Portrait_Table. Copy a row of the same kind for VTable/Unk3.";
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var row = Picked(_rows);
            if (_rom == null || row == null || !TryInt(row.Cells[2].Value, 0, 255, out int sprite)) { _preview.Image = null; return; }
            if (!_iconCache.TryGetValue(sprite, out var img))
            {
                try { img = CharacterIconReader.GetIcon(_rom, IconGame, sprite); } catch { img = null; }
                _iconCache[sprite] = img; // decoding is the slow part; do it once per sprite
            }
            _preview.Image = img;
        }

        private bool ApplyRows()
        {
            if (_rom == null) return false;
            int n = RosterTables.DisplayRowCount(_rom);
            var parsed = new List<DisplayRow>();
            foreach (DataGridViewRow r in _rows.Rows)
            {
                string at = $"Row {r.Cells[0].Value}";
                var c = r.Cells;
                if (!TryHex(c[1].Value, out uint flags) || !TryInt(c[2].Value, 0, 255, out int sp) || !TryInt(c[3].Value, 0, 255, out int pt) || !TryInt(c[4].Value, 0, 255, out int u3)
                    || !TryInt(c[5].Value, 0, n - 1, out int tx) || !TryInt(c[6].Value, 0, n - 1, out int dx) || !TryInt(c[7].Value, 0, 255, out int mn) || !TryInt(c[8].Value, 0, 255, out int mx)
                    || !TryInt(c[9].Value, 0, 255, out int t2) || !TryInt(c[10].Value, 0, 255, out int u9) || !TryInt(c[11].Value, 0, 65535, out int ua) || !TryHex(c[12].Value, out uint uc) || !TryHex(c[13].Value, out uint vt) || flags > 255)
                { Fail($"{at}: a field is invalid (numbers 0-255, links must be valid rows 0-{n - 1}, hex for Flags/UnkC/VTable)."); return false; }
                parsed.Add(new DisplayRow { Flags = (byte)flags, SpriteId = (byte)sp, PortraitIndex = (byte)pt, Unk3 = (byte)u3, TransformedIndex = (byte)tx, DetransformedIndex = (byte)dx, MinFrames = (byte)mn, MaxFrames = (byte)mx, Transformed2 = (byte)t2, Unk9 = (byte)u9, UnkA = (ushort)ua, UnkC = uc, VTable = vt });
            }
            for (int i = 0; i < parsed.Count; i++) RosterTables.WriteDisplayRow(_rom, i, parsed[i]);
            return true;
        }

        private void AddRow()
        {
            if (_rom == null) return;
            var picked = Picked(_rows);
            if (picked == null) { Fail("Select a row to copy first."); return; }
            int pickedIndex = picked.Index;
            if (!ApplyRows()) return; // commit pending grid edits first
            try
            {
                var copy = RosterTables.ReadDisplayRows(_rom)[pickedIndex];
                int idx = RosterTables.AddDisplayRow(_rom, copy);
                // A new row changes: the rows grid, the party's display-row range info, and the engine tab's numbers.
                FillRows(); FillParty(); FillMap();
                _rows.ClearSelection(); _rows.Rows[idx].Selected = true;
                ScrollIntoView(_rows, idx);
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        // ---- abilities -----------------------------------------------------------------------------------------
        private TabPage BuildAbilitiesTab()
        {
            var page = new TabPage("Abilities");
            foreach (var c in new[] { "Index", "Name", "ReadyIcon", "CoolingIcon", "IsReady fn", "OnUse fn" }) AddCol(_abil, c, c is "Index" or "Name", c == "Name" ? 170 : c == "Index" ? 60 : 110);
            _abil.Columns.Add(new DataGridViewComboBoxColumn { Name = "Animation", HeaderText = "Animation (picks the sprite block it reads)", FlatStyle = FlatStyle.Flat, Width = 280 });
            var apply = new Button { Text = "Apply to ROM copy", AutoSize = true };
            apply.Click += (_, _) => { if (ApplyAbilities()) { FillAbilities(); } };
            var add = new Button { Text = "Add ability (copy of selected)", AutoSize = true };
            add.Click += (_, _) => AddAbility();
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(4) };
            buttons.Controls.AddRange([apply, add]);
            page.Controls.Add(_abil); page.Controls.Add(_abilInfo); page.Controls.Add(buttons);
            return page;
        }

        private const string CustomAnim = "(custom code -- not swappable)";
        private static string AnimLabel(int a) => $"{a} - {AbilityAnimations.Known[a].Name} (sprite block +{AbilityAnimations.Known[a].Block})";

        private void FillAbilities()
        {
            var list = RosterTables.ReadAbilities(_rom!);
            Populate(_abil, () =>
            {
                _abil.Rows.Clear();
                var animCol = (DataGridViewComboBoxColumn)_abil.Columns["Animation"]!;
                animCol.Items.Clear();
                animCol.Items.Add(CustomAnim);
                foreach (var k in AbilityAnimations.Known.OrderBy(k => k.Key)) animCol.Items.Add(AnimLabel(k.Key));
                for (int i = 0; i < list.Count; i++)
                {
                    int r = _abil.Rows.Add(i, AbilityName(i), Hex(list[i].ReadyIcon), Hex(list[i].CoolingIcon), Hex(list[i].IsReady), Hex(list[i].OnUse));
                    int? anim = AbilityAnimations.Get(_rom!, list[i]);
                    string label = anim is int a && AbilityAnimations.Known.ContainsKey(a) ? AnimLabel(a) : anim is int b ? $"{b} - (unlisted animation)" : CustomAnim;
                    if (!animCol.Items.Contains(label)) animCol.Items.Add(label);
                    _abil.Rows[r].Cells[6].Value = label;
                }
            });
            _abilInfo.Text = $"{list.Count} of {RosterTables.AbilityCapacity} abilities. An ability = two HUD icons + an is-ready function + an on-use function. New COMBINATIONS of existing " +
                             "functions/icons are just data; a genuinely new attack needs new code. Then give it to a slot on the Default party tab.";
        }

        private bool ApplyAbilities()
        {
            if (_rom == null) return false;
            var parsed = new List<AbilityEntry>();
            foreach (DataGridViewRow r in _abil.Rows)
            {
                if (!TryHex(r.Cells[2].Value, out uint a) || !TryHex(r.Cells[3].Value, out uint b) || !TryHex(r.Cells[4].Value, out uint c) || !TryHex(r.Cells[5].Value, out uint d))
                { Fail($"Ability {r.Cells[0].Value}: pointers must be hex."); return false; }
                parsed.Add(new AbilityEntry { ReadyIcon = a, CoolingIcon = b, IsReady = c, OnUse = d });
            }
            var before = RosterTables.ReadAbilities(_rom);
            for (int i = 0; i < parsed.Count; i++)
            {
                // Animation picker: clone the standard OnUse routine with the chosen animation (see AbilityAnimations).
                string? pick = _abil.Rows[i].Cells[6].Value?.ToString();
                if (pick != null && pick != CustomAnim && int.TryParse(pick.Split(' ')[0], out int want) && AbilityAnimations.Known.ContainsKey(want)
                    && AbilityAnimations.Get(_rom, before[i]) is int have && have != want)
                {
                    try { parsed[i].OnUse = AbilityAnimations.CloneWithAnimation(_rom, before[i], want); }
                    catch (Exception ex) { Fail($"Ability {i}: {ex.Message}"); return false; }
                }
            }
            for (int i = 0; i < parsed.Count; i++) RosterTables.WriteAbility(_rom, i, parsed[i]);
            return true;
        }

        // A brand-new ability that switches the active character to a chosen display row (base -> SSJ God etc.); see DrGero/TransformationAbility.cs.
        private void AddTransformationAbility()
        {
            if (_rom == null) return;
            if (!ApplyAbilities()) return;
            var rows = RosterTables.ReadDisplayRows(_rom);
            var abilities = RosterTables.ReadAbilities(_rom);
            string RowLabel(int i) => $"Row {i}  -  sprite {rows[i].SpriteId}, portrait {rows[i].PortraitIndex}{((rows[i].Flags & 1) != 0 ? ", transformed form" : "")}";

            using var dlg = new Form
            {
                Text = "Add transformation ability", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, ClientSize = new Size(560, 300), AutoScaleMode = AutoScaleMode.Font,
            };
            var target = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 170, Top = 14, Width = 370 };
            var revert = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 170, Top = 50, Width = 370 };
            var icons = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 170, Top = 122, Width = 370 };
            var ep = new NumericUpDown { Left = 170, Top = 86, Width = 90, Minimum = 0, Maximum = 999, Value = 30 };
            var revertible = new CheckBox { Text = "Make the target row revertible with the stock Transformation ability", Left = 170, Top = 156, Width = 380, Checked = true };
            var newForm = new CheckBox { Text = "Create it as a NEW form: copy the \"Becomes\" row with a NEW sprite id (edit its frames in the Sprite editor)", Left = 170, Top = 184, Width = 380, Height = 40, Checked = true };
            newForm.CheckedChanged += (_, _) => revertible.Enabled = !newForm.Checked;
            for (int i = 0; i < rows.Count; i++) { target.Items.Add(RowLabel(i)); revert.Items.Add(RowLabel(i)); }
            for (int i = 0; i < abilities.Count; i++) icons.Items.Add($"{i} - {AbilityName(i)}");
            target.SelectedIndex = Math.Min(20, rows.Count - 1);
            revert.SelectedIndex = Math.Min(19, rows.Count - 1);
            icons.SelectedIndex = Math.Min(11, abilities.Count - 1);
            target.SelectedIndexChanged += (_, _) => { if (target.SelectedIndex >= 0) revert.SelectedIndex = Math.Min(rows[target.SelectedIndex].DetransformedIndex, rows.Count - 1); };
            var ok = new Button { Text = "Add", DialogResult = DialogResult.OK, Left = 370, Top = 255, Width = 80 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 460, Top = 255, Width = 80 };
            dlg.AcceptButton = ok; dlg.CancelButton = cancel;
            dlg.Controls.AddRange([
                new Label { Text = "Becomes (display row) / copy of:", Left = 12, Top = 18, Width = 155, Height = 32 }, target,
                new Label { Text = "Reverts to (display row):", Left = 12, Top = 54, Width = 155 }, revert,
                new Label { Text = "EP cost:", Left = 12, Top = 90, Width = 155 }, ep,
                new Label { Text = "Share HUD icons of:", Left = 12, Top = 126, Width = 155 }, icons,
                revertible, newForm, ok, cancel]);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string form = "";
                TransformationAbility.Result result;
                if (newForm.Checked)
                {
                    var made = TransformationAbility.AddWithNewForm(_rom, target.SelectedIndex, (int)ep.Value, icons.SelectedIndex, revert.SelectedIndex);
                    result = made.Ability;
                    form = $"New form: display row {made.NewRow} with NEW sprite id {made.NewSprite} (a copy of sprite {RosterTables.ReadDisplayRows(_rom)[target.SelectedIndex].SpriteId}; recolour it in the Sprite editor). ";
                }
                else result = TransformationAbility.Add(_rom, target.SelectedIndex, (int)ep.Value, icons.SelectedIndex, revertible.Checked, revert.SelectedIndex);
                FillAbilities(); FillParty(); FillRows();   // the party grid's ability drop-downs gain the new entry; the row list gains the new form
                _abilInfo.Text = form + result.Notes + " Assign it in the Default party tab (it only affects a NEW GAME) or via a script that rewrites the slot's ability list.";
                _session.NotifyChanged(this);
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        private void AddAbility()
        {
            if (_rom == null) return;
            var picked = Picked(_abil);
            if (picked == null) { Fail("Select an ability to copy first."); return; }
            int pickedIndex = picked.Index;
            if (!ApplyAbilities()) return;
            try
            {
                var copy = RosterTables.ReadAbilities(_rom)[pickedIndex];
                RosterTables.AddAbility(_rom, copy);
                FillAbilities(); FillParty(); // the party grid's ability drop-downs gain the new entry
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        // ---- engine patches + map table ------------------------------------------------------------------------
        private TabPage BuildEngineTab()
        {
            var page = new TabPage("Engine patches / maps");
            var root = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
            root.Controls.Add(new Label { Text = "Verified patches (each checks the original bytes first and can be reverted):", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
            root.Controls.Add(_patchPanel);

            root.Controls.Add(new Label { Text = "Map table", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 14, 0, 0) });
            root.Controls.Add(_mapInfo);
            var line = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            var add = new Button { Text = "Add map entry (copy)", AutoSize = true };
            add.Click += (_, _) => AddMap();
            line.Controls.AddRange([new Label { Text = "copy entry #", AutoSize = true, Margin = new Padding(3, 8, 3, 0) }, _mapSource,
                new Label { Text = "zone", AutoSize = true, Margin = new Padding(8, 8, 3, 0) }, _mapZone,
                new Label { Text = "area", AutoSize = true, Margin = new Padding(8, 8, 3, 0) }, _mapArea,
                new Label { Text = "variation", AutoSize = true, Margin = new Padding(8, 8, 3, 0) }, _mapVar, add]);
            root.Controls.Add(line);
            root.Controls.Add(new Label
            {
                AutoSize = true, MaximumSize = new Size(900, 0), Padding = new Padding(0, 8, 0, 0),
                Text = "Adding a map relocates the table (room for 510 entries), repoints its 4 code literals (incl. the new-game map) and raises Map_GetCount. " +
                       "The copy SHARES the source map's triggers, scripts and layers until edited -- reach it with a warp script, or via the debug menu patch above, " +
                       "and open the ROM in Dragon Radar to edit it. Dragon Radar reads the table location from the ROM, so it sees added maps."
            });
            page.Controls.Add(root);
            return page;
        }

        private void FillPatches()
        {
            _patchPanel.SuspendLayout();
            _patchPanel.Controls.Clear();
            foreach (var p in EnginePatches.All)
            {
                var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
                bool applied = p.IsApplied(_rom!), original = p.IsOriginal(_rom!);
                var status = new Label { AutoSize = true, Width = 90, Margin = new Padding(3, 7, 8, 0), ForeColor = applied ? Color.ForestGreen : Color.DimGray, Text = applied ? "APPLIED" : original ? "not applied" : "UNKNOWN" };
                var toggle = new Button { AutoSize = true, Text = applied ? "Revert" : "Apply", Enabled = applied || original };
                var patch = p;
                toggle.Click += (_, _) => { try { if (patch.IsApplied(_rom!)) patch.Revert(_rom!); else patch.Apply(_rom!); FillPatches(); } catch (Exception ex) { Fail(ex.Message); } };
                row.Controls.AddRange([status, toggle, new Label { AutoSize = true, MaximumSize = new Size(760, 0), Margin = new Padding(8, 7, 0, 0), Text = $"{p.Name} -- {p.Description}" }]);
                _patchPanel.Controls.Add(row);
            }
            _patchPanel.ResumeLayout();
        }

        private void FillMap()
        {
            try
            {
                int count = MapTable.Count(_rom!);
                _mapInfo.Text = $"{count} maps, table at file 0x{MapTable.Address(_rom!):X} " + (MapTable.Relocated(_rom!) ? "(relocated)" : "(original)") + $"; limit {MapTable.MaxCount}.";
                _mapSource.Maximum = Math.Max(0, count - 1);
            }
            catch (Exception ex) { _mapInfo.Text = ex.Message; }
        }

        private void AddMap()
        {
            if (_rom == null) return;
            try
            {
                int idx = MapTable.AddEntryFromCopy(_rom, (int)_mapSource.Value, (byte)_mapZone.Value, (byte)_mapArea.Value, (byte)_mapVar.Value);
                FillMap();
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        private void RefreshAll()
        {
            SuspendLayout();
            FillParty(); FillRows(); FillAbilities(); FillPatches(); FillMap();
            ResumeLayout();
        }
    }
}
