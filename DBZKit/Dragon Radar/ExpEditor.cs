using DrGero;

namespace Dragon_Radar
{
    /// <summary>
    /// Edits an enemy stat entry's EXP base (EnemyStats +0x00) either as the stored number, or as "a level-N player receives X EXP"
    /// -- the game scales the stored number to the player's level (EnemyStats.Reward), so the second mode works out the number to
    /// store (EnemyStats.BaseFor) and says when the game can't give exactly that. <see cref="Value"/> is always the number stored.
    /// </summary>
    internal sealed class ExpEditor : TableLayoutPanel
    {
        private readonly RadioButton _storedMode = new() { Text = "Stored value", AutoSize = true, Checked = true };
        private readonly RadioButton _rewardMode = new() { Text = "Player receives", AutoSize = true };
        private readonly NumericUpDown _stored = new() { Minimum = 0, Maximum = 9999999, Width = 90, ThousandsSeparator = true };
        private readonly NumericUpDown _wanted = new() { Minimum = 0, Maximum = 9999999, Width = 90, ThousandsSeparator = true, Enabled = false };
        private readonly ComboBox _level = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Enabled = false };
        private readonly Label _result = new() { AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(3, 2, 3, 4) };
        private int[]? _expTable;
        private bool _updating;

        public ExpEditor(int[]? expTable)
        {
            _expTable = expTable;
            ColumnCount = 4;
            AutoSize = true;
            for (int l = 1; l <= EnemyStats.MaxLevel; l++) _level.Items.Add($"level {l}");
            _level.SelectedIndex = 0;

            Controls.Add(_storedMode);
            Controls.Add(_stored);
            SetColumnSpan(_stored, 3);
            Controls.Add(_rewardMode);
            Controls.Add(_wanted);
            Controls.Add(new Label { Text = "EXP at", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 0) });
            Controls.Add(_level);
            Controls.Add(_result);
            SetColumnSpan(_result, 4);

            _rewardMode.Enabled = expTable != null; // without the EXP table the scaling can't be worked out
            _storedMode.CheckedChanged += (_, _) => ModeChanged();
            _stored.ValueChanged += (_, _) => { if (!_updating) Refresh(fromWanted: false); ValueChanged?.Invoke(this, EventArgs.Empty); };
            _wanted.ValueChanged += (_, _) => { if (!_updating) Refresh(fromWanted: true); };
            _level.SelectedIndexChanged += (_, _) => { if (!_updating) Refresh(fromWanted: _rewardMode.Checked); };
            Refresh(fromWanted: false);
        }

        /// <summary>The shared EXP-per-level table (EnemyStats.ExpTable); null turns the "Player receives" mode off.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int[]? ExpTable
        {
            get => _expTable;
            set
            {
                _expTable = value;
                _rewardMode.Enabled = value != null;
                if (value == null) _storedMode.Checked = true;
                Refresh(fromWanted: false);
            }
        }

        /// <summary>Raised when the stored value changes (typed, or worked out from "Player receives").</summary>
        public event EventHandler? ValueChanged;

        /// <summary>The EXP base to store.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => (int)_stored.Value;
            set
            {
                _updating = true;
                _stored.Value = Math.Clamp(value, 0, (int)_stored.Maximum);
                _updating = false;
                Refresh(fromWanted: false);
            }
        }

        private int Level => _level.SelectedIndex + 1;

        private void ModeChanged()
        {
            bool reward = _rewardMode.Checked;
            _stored.Enabled = !reward;
            _wanted.Enabled = _level.Enabled = reward;
            Refresh(fromWanted: false); // start the reward box from what the current value gives
        }

        private new void Refresh(bool fromWanted)
        {
            if (_expTable == null) { _result.Text = ""; return; }
            _updating = true;
            if (fromWanted)
            {
                var (stored, gets, note) = EnemyStats.BaseFor((int)_wanted.Value, Level, _expTable);
                _stored.Value = stored;
                _result.Text = note == null
                    ? $"Stores {stored:N0}: a level {Level} player gets exactly {gets:N0} EXP."
                    : $"{note} Stores {stored:N0}, which gives {gets:N0}.";
            }
            else
            {
                int gets = EnemyStats.Reward((int)_stored.Value, Level, _expTable);
                _wanted.Value = Math.Min(gets, (int)_wanted.Maximum);
                _result.Text = $"A level {Level} player gets {gets:N0} EXP from this.";
            }
            _updating = false;
        }
    }
}
