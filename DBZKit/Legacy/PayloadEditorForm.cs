using DrGero.Quests;
using Legacy.Zenkai;
using ScintillaNET;

namespace Legacy
{
    /// <summary>
    /// The "micro Legacy": a small in-process editor for one trigger's or object's payload --
    /// either a conversation/pickup sequence (a list of message and script steps you can add,
    /// remove and reorder) or, for triggers whose payload is plain bytecode, a single script.
    /// Uses the real Zenkai editor (highlighting, autocomplete incl. variables/labels, squiggles).
    ///
    /// It only edits; the caller writes the result back (DialogWriter.WriteSequence for a
    /// sequence, raw bytes for a plain script) and repoints the payload field.
    /// </summary>
    internal sealed class PayloadEditorForm : Form
    {
        internal sealed class Step
        {
            public bool IsScript;
            public byte Character;   // messages only: who's speaking (CharacterId)
            public string Text = ""; // message text (without its position/centre prefix), or Zenkai source for a script
            public BoxPosition Position; // messages only: '!' / '@' / '#'
            public bool Center;          // messages only: '^' 
        }

        private readonly List<Step> _steps;
        private readonly bool _rawScript;

        private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly Scintilla _script = new() { Dock = DockStyle.Fill };
        private readonly TextBox _message = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, Font = new Font("Consolas", 10f) };
        private readonly NumericUpDown _character = new() { Minimum = 0, Maximum = 255, Width = 60 };
        private readonly Panel _messagePanel = new() { Dock = DockStyle.Fill, Visible = false };
        private readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

        // Live preview of the selected message: portrait, box position and width (see DialogPreview).
        private readonly ComboBox _position = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        private readonly CheckBox _center = new() { Text = "Centre text (^)", AutoSize = true };
        private readonly Label _speakerInfo = new() { Dock = DockStyle.Top, Height = 38, Padding = new Padding(4, 2, 4, 0), ForeColor = Color.DimGray };
        private readonly DialogPreviewControl _preview = new();
        private readonly Panel _previewHolder = new() { Dock = DockStyle.Bottom, Height = 380, Visible = false };
        private readonly byte[]? _romForPreview;
        private readonly Dictionary<byte, (Bitmap? Image, string What)> _portraits = [];

        private Step? _shown;
        private bool _loading;

        // Called with (sequence lines, null) or (null, compiled plain script) each time Apply/OK
        // compiles cleanly; writes the result into the ROM and returns an error message, or null.
        private readonly Func<List<DialogLine>?, byte[]?, string?> _commit;

        public PayloadEditorForm(string title, List<Step> steps, bool rawScript, string? warning,
            Func<List<DialogLine>?, byte[]?, string?> commit, byte[]? romForPreview = null)
        {
            _romForPreview = romForPreview;
            _steps = steps;
            _rawScript = rawScript;
            _commit = commit;

            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, romForPreview != null ? 900 : 580);
            MinimumSize = new Size(700, romForPreview != null ? 700 : 400);
            ShowIcon = false;

            // ---- left: step list + add/remove/reorder ----
            var addMessage = new Button { Text = "+ Message", AutoSize = true };
            var addScript = new Button { Text = "+ Script", AutoSize = true };
            var remove = new Button { Text = "Remove", AutoSize = true };
            var up = new Button { Text = "Up", AutoSize = true };
            var down = new Button { Text = "Down", AutoSize = true };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true };
            if (!rawScript)
                buttons.Controls.AddRange(new Control[] { addMessage, addScript, remove, up, down });

            var left = new Panel { Dock = DockStyle.Left, Width = 250 };
            left.Controls.Add(_list);
            left.Controls.Add(buttons);
            if (rawScript) left.Visible = false; // a single plain script -- nothing to list

            // ---- right: the editor for the selected step ----
            var charLabel = new Label { Text = "Character ID:", AutoSize = true, Location = new Point(6, 9) };
            _character.Location = new Point(90, 5);
            var top = new Panel { Dock = DockStyle.Top, Height = 32 };
            top.Controls.Add(charLabel);
            top.Controls.Add(_character);

            // Box position + centring (the leading '!' / '@' / '#' and '^' of the message, kept out of the text you edit).
            _position.Items.AddRange(new object[] { "Auto (engine decides)", "Top (Y 40)", "Middle (Y 80)", "Bottom (Y 120)" });
            _position.SelectedIndex = 0;
            var formatBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(4, 4, 0, 0) };
            formatBar.Controls.AddRange(new Control[] { new Label { Text = "Box position:", AutoSize = true, Margin = new Padding(0, 5, 4, 0) }, _position, _center });
            _center.Margin = new Padding(12, 4, 0, 0);

            _preview.Anchor = AnchorStyles.Top;
            _preview.Location = new Point(8, 4);
            var previewInner = new Panel { Dock = DockStyle.Fill };
            previewInner.Controls.Add(_preview);
            _previewHolder.Controls.Add(previewInner);
            _previewHolder.Controls.Add(_speakerInfo);
            previewInner.Resize += (_, _) => _preview.Left = Math.Max(4, (previewInner.Width - _preview.Width) / 2);

            _messagePanel.Controls.Add(_message);
            _messagePanel.Controls.Add(_previewHolder);
            _messagePanel.Controls.Add(formatBar);
            _messagePanel.Controls.Add(top);
            _previewHolder.Visible = romForPreview != null;

            var right = new Panel { Dock = DockStyle.Fill };
            right.Controls.Add(_script);
            right.Controls.Add(_messagePanel);

            // ---- bottom bar ----
            var check = new Button { Text = "Compile check", Width = 110, Dock = DockStyle.Left };
            var apply = new Button { Text = "Apply", Width = 90, Dock = DockStyle.Right };
            var ok = new Button { Text = "OK", Width = 80, Dock = DockStyle.Right };
            // Not DialogResult.Cancel: this form is run with Application.Run (not ShowDialog), where a
            // button's DialogResult alone doesn't close it -- that's why Cancel used to do nothing.
            var cancel = new Button { Text = "Cancel", Width = 90, Dock = DockStyle.Right };
            cancel.Click += (_, _) => Close();
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
            bar.Controls.Add(_status);
            bar.Controls.Add(check);
            bar.Controls.Add(cancel);
            bar.Controls.Add(apply);
            bar.Controls.Add(ok);

            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(bar);
            CancelButton = cancel;

            if (!string.IsNullOrEmpty(warning))
                SetStatus(warning, isError: true);

            _list.SelectedIndexChanged += (_, _) => ShowSelected();
            addMessage.Click += (_, _) => AddStep(isScript: false);
            addScript.Click += (_, _) => AddStep(isScript: true);
            remove.Click += (_, _) => RemoveSelected();
            up.Click += (_, _) => MoveSelected(-1);
            down.Click += (_, _) => MoveSelected(1);
            _message.TextChanged += (_, _) => { if (!_loading) { RefreshListLabels(); UpdatePreview(); } };
            _character.ValueChanged += (_, _) => { if (!_loading && _shown is { IsScript: false }) { _shown.Character = (byte)_character.Value; UpdatePreview(); } };
            _position.SelectedIndexChanged += (_, _) => { if (!_loading && _shown is { IsScript: false }) { _shown.Position = (BoxPosition)_position.SelectedIndex; UpdatePreview(); } };
            _center.CheckedChanged += (_, _) => { if (!_loading && _shown is { IsScript: false }) { _shown.Center = _center.Checked; UpdatePreview(); } };
            check.Click += (_, _) => TryCompileAll(showSuccess: true);
            apply.Click += (_, _) => Apply();
            ok.Click += (_, _) => { if (Apply()) { DialogResult = DialogResult.OK; Close(); } };

            // Configure after the handle exists -- native Scintilla calls silently no-op before
            // there's a real HWND (same reason Legacy_Load does this).
            Load += (_, _) =>
            {
                ZenkaiEditor.Configure(_script);
                OpcodeUsage.Attach(_script, () => _romForPreview);
                RebuildList(selectIndex: 0);
            };
        }

        // ---- list / selection ----

        private static string FirstLine(string text)
        {
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0 && !line.StartsWith("//")) return line.Length > 34 ? line[..34] + "…" : line;
            }
            return "(empty)";
        }

        private string LabelFor(int i)
        {
            var s = _steps[i];
            return s.IsScript ? $"{i + 1}. Script: {FirstLine(s.Text)}" : $"{i + 1}. Message: {FirstLine(s.Text)}";
        }

        private void RebuildList(int selectIndex)
        {
            _loading = true;
            _list.BeginUpdate();
            _list.Items.Clear();
            for (int i = 0; i < _steps.Count; i++) _list.Items.Add(LabelFor(i));
            _list.EndUpdate();
            _loading = false;

            if (_steps.Count == 0) { _shown = null; _script.Visible = false; _messagePanel.Visible = false; return; }
            _list.SelectedIndex = Math.Clamp(selectIndex, 0, _steps.Count - 1);
            if (_rawScript) ShowSelected(); // list is hidden but still drives selection
        }

        private void RefreshListLabels()
        {
            CommitShown();
            int sel = _list.SelectedIndex;
            _loading = true;
            for (int i = 0; i < _steps.Count && i < _list.Items.Count; i++)
                _list.Items[i] = LabelFor(i);
            if (sel >= 0 && sel < _list.Items.Count) _list.SelectedIndex = sel;
            _loading = false;
        }

        private void CommitShown()
        {
            if (_shown == null) return;
            _shown.Text = _shown.IsScript ? _script.Text : _message.Text;
            if (!_shown.IsScript)
            {
                _shown.Character = (byte)_character.Value;
                _shown.Position = (BoxPosition)Math.Max(0, _position.SelectedIndex);
                _shown.Center = _center.Checked;
            }
        }

        private void ShowSelected()
        {
            if (_loading) return;
            CommitShown();

            int i = _list.SelectedIndex;
            if (i < 0 || i >= _steps.Count) { _shown = null; return; }

            _loading = true;
            _shown = _steps[i];
            _script.Visible = _shown.IsScript;
            _messagePanel.Visible = !_shown.IsScript;
            if (_shown.IsScript)
                _script.Text = _shown.Text;
            else
            {
                _message.Text = _shown.Text;
                _character.Value = _shown.Character;
                _position.SelectedIndex = (int)_shown.Position;
                _center.Checked = _shown.Center;
            }
            _loading = false;
            RefreshListLabelsQuiet();
            UpdatePreview();
        }

        // Redraws the mock screen for the selected message. Portraits are decoded once per speaker id.
        private void UpdatePreview()
        {
            if (_romForPreview == null || _shown is not { IsScript: false }) return;

            byte speaker = (byte)_character.Value;
            if (!_portraits.TryGetValue(speaker, out var cached))
            {
                var img = DialogPreview.PortraitFor(_romForPreview, speaker, out string what);
                cached = (img, what);
                _portraits[speaker] = cached;
            }

            int style = DialogPreview.BoxStyle(speaker);
            _speakerInfo.Text = $"Speaker {speaker}: {cached.What}  |  box {DialogPreview.BoxWidth(speaker)} px wide, style {style}." +
                                (speaker != 0 && cached.Image == null ? "  (no portrait bitmap for this id)" : "");
            _preview.SetContent(speaker, (BoxPosition)Math.Max(0, _position.SelectedIndex), _center.Checked, _message.Text, cached.Image);
        }

        private void RefreshListLabelsQuiet()
        {
            _loading = true;
            int sel = _list.SelectedIndex;
            for (int i = 0; i < _steps.Count && i < _list.Items.Count; i++)
                _list.Items[i] = LabelFor(i);
            if (sel >= 0 && sel < _list.Items.Count) _list.SelectedIndex = sel;
            _loading = false;
        }

        private void AddStep(bool isScript)
        {
            CommitShown();
            int at = _list.SelectedIndex + 1;
            _steps.Insert(at, new Step { IsScript = isScript });
            RebuildList(at);
        }

        private void RemoveSelected()
        {
            int i = _list.SelectedIndex;
            if (i < 0) return;
            _shown = null; // don't commit into a step that's going away
            _steps.RemoveAt(i);
            RebuildList(Math.Min(i, _steps.Count - 1));
        }

        private void MoveSelected(int delta)
        {
            CommitShown();
            int i = _list.SelectedIndex, j = i + delta;
            if (i < 0 || j < 0 || j >= _steps.Count) return;
            (_steps[i], _steps[j]) = (_steps[j], _steps[i]);
            RebuildList(j);
        }

        // ---- compile / apply ----

        private void SetStatus(string message, bool isError)
        {
            _status.Text = message;
            _status.ForeColor = isError ? Color.Firebrick : Color.DarkGreen;
        }

        private List<byte[]?>? TryCompileAll(bool showSuccess)
        {
            CommitShown();
            var compiled = new List<byte[]?>();
            for (int i = 0; i < _steps.Count; i++)
            {
                if (!_steps[i].IsScript) { compiled.Add(null); continue; }
                try
                {
                    compiled.Add(ZenkaiAssembler.Assemble(_steps[i].Text));
                }
                catch (Exception ex)
                {
                    _list.SelectedIndex = i; // jump to the broken step
                    SetStatus($"Step {i + 1}: {ex.Message}", isError: true);
                    return null;
                }
            }
            if (showSuccess) SetStatus("Everything compiles.", isError: false);
            return compiled;
        }

        // Ctrl+S = Apply (commit and keep the window open), whichever control has focus.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                Apply();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Compiles everything and hands the result to the commit callback (which writes it into
        // the ROM). Returns true if it was committed. Apply keeps the window open so you can keep
        // iterating -- Dragon Radar reloads on every commit -- while OK also closes it.
        private bool Apply()
        {
            var compiled = TryCompileAll(showSuccess: false);
            if (compiled == null) return false;

            string? error;
            if (_rawScript)
            {
                error = _commit(null, compiled.FirstOrDefault(c => c != null) ?? new byte[] { 0x11 });
            }
            else
            {
                var lines = new List<DialogLine>();
                for (int i = 0; i < _steps.Count; i++)
                {
                    var s = _steps[i];
                    if (s.IsScript) lines.Add(DialogLine.Script(compiled[i]!));
                    else lines.Add(DialogLine.TextLine(s.Character, DialogPreview.ApplyFormat(s.Text.Replace("\r\n", "\n"), s.Position, s.Center)));
                }

                if (lines.Count == 0)
                {
                    SetStatus("Add at least one message or script step.", isError: true);
                    return false;
                }

                error = _commit(lines, null);
            }

            if (error != null)
            {
                SetStatus(error, isError: true);
                return false;
            }

            SetStatus($"Applied at {DateTime.Now:T} -- saved to the ROM Dragon Radar is watching.", isError: false);
            return true;
        }
    }
}
