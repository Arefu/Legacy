using System.ComponentModel;
using DrGero;

namespace Dragon_Radar
{
    /// <summary>
    /// An enemy's EXP, three ways:
    ///   Exact     -- the same amount at every level: EXP base 0 plus "AddEXP(N);" in the enemy's own defeat script
    ///                (EnemyRecords.SetExactExp). Only for enemies already in the ROM.
    ///   Stored    -- the stat entry's EXP base, which the game scales to the player's level (EnemyStats.Reward).
    ///   Receives  -- "a level-L player gets N": works out the EXP base for that (EnemyStats.BaseFor), saying when the game can't.
    /// </summary>
    internal sealed class ExpEditor : TableLayoutPanel
    {
        private readonly RadioButton _exactMode = new() { Text = "Exact (every level)", AutoSize = true };
        private readonly RadioButton _storedMode = new() { Text = "Scaled: stored value", AutoSize = true, Checked = true };
        private readonly RadioButton _rewardMode = new() { Text = "Scaled: player gets", AutoSize = true };
        private readonly NumericUpDown _exact = Box();
        private readonly NumericUpDown _stored = Box();
        private readonly NumericUpDown _wanted = Box();
        private readonly ComboBox _level = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        private readonly Label _result = new() { AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(3, 2, 3, 4) };
        private int[]? _expTable;
        private bool _updating, _exactAllowed = true;

        private static NumericUpDown Box() => new() { Minimum = 0, Maximum = 9999999, Width = 90, ThousandsSeparator = true };

        public ExpEditor(int[]? expTable)
        {
            ColumnCount = 4;
            AutoSize = true;
            for (int l = 1; l <= EnemyStats.MaxLevel; l++) _level.Items.Add($"level {l}");
            _level.SelectedIndex = 0;

            Controls.Add(_exactMode);
            Controls.Add(_exact);
            SetColumnSpan(_exact, 3);
            Controls.Add(_storedMode);
            Controls.Add(_stored);
            SetColumnSpan(_stored, 3);
            Controls.Add(_rewardMode);
            Controls.Add(_wanted);
            Controls.Add(new Label { Text = "EXP at", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 0) });
            Controls.Add(_level);
            Controls.Add(_result);
            SetColumnSpan(_result, 4);

            foreach (var r in new[] { _exactMode, _storedMode, _rewardMode }) r.CheckedChanged += (_, _) => { if (r.Checked) ModeChanged(); };
            _exact.ValueChanged += (_, _) => { if (!_updating) Describe(); ValueChanged?.Invoke(this, EventArgs.Empty); };
            _stored.ValueChanged += (_, _) => { if (!_updating) FromStored(); ValueChanged?.Invoke(this, EventArgs.Empty); };
            _wanted.ValueChanged += (_, _) => { if (!_updating) FromWanted(); };
            _level.SelectedIndexChanged += (_, _) => { if (_updating) return; if (_rewardMode.Checked) FromWanted(); else FromStored(); };
            ExpTable = expTable;
        }

        /// <summary>The shared EXP-per-level table (EnemyStats.ExpTable); null turns the "player gets" mode off.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int[]? ExpTable
        {
            get => _expTable;
            set
            {
                _expTable = value;
                _rewardMode.Enabled = value != null;
                if (value == null && _rewardMode.Checked) _storedMode.Checked = true;
                ModeChanged();
            }
        }

        /// <summary>Whether "Exact" can be picked (it needs the enemy's own record in the ROM).</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ExactAllowed
        {
            get => _exactAllowed;
            set
            {
                _exactAllowed = value;
                _exactMode.Enabled = value;
                if (!value && _exactMode.Checked) _storedMode.Checked = true;
            }
        }

        public event EventHandler? ValueChanged;

        /// <summary>True when "Exact" is picked; then <see cref="ExactAmount"/> is what to give and the EXP base should be 0.</summary>
        public bool IsExact => _exactMode.Checked;

        public int ExactAmount => (int)_exact.Value;

        /// <summary>The EXP base to store (0 in Exact mode -- the script gives the EXP instead).</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => IsExact ? 0 : (int)_stored.Value;
            set => Load(value, null);
        }

        /// <summary>Shows an enemy: its EXP base, and the exact amount its defeat script adds (null if none).</summary>
        public void Load(int storedBase, int? exact)
        {
            _updating = true;
            _stored.Value = Math.Clamp(storedBase, 0, (int)_stored.Maximum);
            _exact.Value = Math.Clamp(exact ?? 0, 0, (int)_exact.Maximum);
            _updating = false;
            if (exact is > 0 && _exactAllowed) _exactMode.Checked = true;
            else if (_exactMode.Checked) _storedMode.Checked = true;
            ModeChanged();
        }

        private int Level => _level.SelectedIndex + 1;

        private void ModeChanged()
        {
            _exact.Enabled = _exactMode.Checked;
            _stored.Enabled = _storedMode.Checked;
            _wanted.Enabled = _level.Enabled = _rewardMode.Checked || _storedMode.Checked;
            _wanted.Enabled = _rewardMode.Checked;
            if (_exactMode.Checked) Describe(); else FromStored();
        }

        private void Describe() =>
            _result.Text = $"Every player gets exactly {_exact.Value:N0} EXP at any level (the game's scaled EXP is turned off for this enemy).";

        private void FromStored()
        {
            if (_exactMode.Checked) { Describe(); return; }
            if (_expTable == null) { _result.Text = ""; return; }
            int gets = EnemyStats.Reward((int)_stored.Value, Level, _expTable);
            _updating = true;
            _wanted.Value = Math.Min(gets, (int)_wanted.Maximum);
            _updating = false;
            _result.Text = $"Scaled by the game: a level {Level} player gets {gets:N0} EXP from this.";
        }

        private void FromWanted()
        {
            if (_expTable == null) return;
            var (stored, gets, note) = EnemyStats.BaseFor((int)_wanted.Value, Level, _expTable);
            _updating = true;
            _stored.Value = stored;
            _updating = false;
            _result.Text = note == null
                ? $"Stores {stored:N0}: a level {Level} player gets exactly {gets:N0} EXP (other levels get a scaled amount)."
                : $"{note} Stores {stored:N0}, which gives {gets:N0}. Use Exact to give any amount at every level.";
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
