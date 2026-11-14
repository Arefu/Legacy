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

            // Whole-table editing (2026-11-15): the list is every slot of the dialog table, in order.
            public bool IsEnd;             // an end marker: the conversation that runs into it stops here
            public Step? NextTalk;         // end markers: the next talk with this character starts at this step (null = the top)
            public uint EndRaw;            // end markers: the value as read, kept when it doesn't name a step
            public byte[]? RawEntry;       // a mode-5 jump to another table: shown, movable, not editable
            public int JumpTarget;
            public byte[]? ArgScript;      // messages only: the script that fills in %s / %d (kept as it is)
            public Step? Next;             // continue at this step instead of the following one (null = the following one)
            public int OriginalEntryOffset; // where this entry already is in the ROM (0 = new)
            public string OriginalKey = ""; // Key() when it was read / last applied
            // Scripts: the bytes as they are in the ROM. While the text is untouched (Text == BaselineText) these bytes
            // are what gets written, so a script nobody edited is never put through disassemble + assemble. Only its
            // "return N" step numbers are patched, in place, when steps are added, removed or moved.
            public byte[]? OriginalBytes;
            public string BaselineText = "";
            // The text shown is not a faithful copy of the script (it uses something Zenkai has no syntax for yet), so
            // typing in it would lose part of the script. Shown read-only; still movable and removable.
            public bool ScriptReadOnly;
            public bool TextUntouched => IsScript && OriginalBytes != null && Text == BaselineText;

            public bool Highlighted;     // part of the conversations asked for (quest or idle); the others are drawn grey

            public bool IsLocked => IsEnd || RawEntry != null;
            public string Key() => IsLocked ? "" : IsScript ? Text : $"{Character}|{Position}|{Center}|{Text.Replace("\r\n", "\n")}";
            public bool Unchanged => OriginalEntryOffset > 0 && (IsLocked || Key() == OriginalKey);
        }

        private readonly List<Step> _steps;
        private readonly bool _rawScript;

        private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed };
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
        private int _lastSelected = -1;
        private bool _loading;

        // Called with (table slots, null) or (null, compiled plain script) each time Apply/OK
        // compiles cleanly; writes the result into the ROM and returns an error message (or null)
        // and, for a table, where each slot's entry now is (so the next Apply only rewrites what changed).
        private readonly Func<List<DialogSlot>?, byte[]?, (string? Error, int[]? EntryOffsets)> _commit;

        private readonly Label _info = new() { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(16), Font = new Font("Segoe UI", 10f) };
        private readonly CheckBox _jumpOn = new() { Text = "Then go to step:", AutoSize = true, Location = new Point(170, 7) };
        private readonly NumericUpDown _jumpTo = new() { Minimum = 0, Maximum = 511, Width = 60, Location = new Point(300, 5), Enabled = false };

        public PayloadEditorForm(string title, List<Step> steps, bool rawScript, string? warning,
            Func<List<DialogSlot>?, byte[]?, (string? Error, int[]? EntryOffsets)> commit, byte[]? romForPreview = null)
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
            var addEnd = new Button { Text = "+ End", AutoSize = true };
            var remove = new Button { Text = "Remove", AutoSize = true };
            var up = new Button { Text = "Up", AutoSize = true };
            var down = new Button { Text = "Down", AutoSize = true };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true };
            if (!rawScript)
                buttons.Controls.AddRange(new Control[] { addMessage, addScript, addEnd, remove, up, down });
            addEnd.Click += (_, _) => AddStep(isScript: false, isEnd: true);

            // The list is the whole table. Steps run downwards until an End; a script that finishes with
            // `return N;` carries on at step N, which is how one table holds a conversation per story state.
            var hint = new Label
            {
                Dock = DockStyle.Top, Height = 98, Padding = new Padding(4), ForeColor = Color.DimGray,
                Text = "Every conversation in this table. Steps run downwards until an End.\r\nIn a script, \"return 3;\" carries on at step 3: that is how it picks a conversation. Those numbers follow when you add, remove or move steps.",
            };

            var left = new Panel { Dock = DockStyle.Left, Width = 330 };
            left.Controls.Add(_list);
            left.Controls.Add(hint);
            left.Controls.Add(buttons);
            if (rawScript) left.Visible = false; // a single plain script -- nothing to list

            // ---- right: the editor for the selected step ----
            var charLabel = new Label { Text = "Character ID:", AutoSize = true, Location = new Point(6, 9) };
            _character.Location = new Point(90, 5);
            var top = new Panel { Dock = DockStyle.Top, Height = 32 };
            top.Controls.Add(charLabel);
            top.Controls.Add(_character);
            if (!rawScript)
            {
                top.Controls.Add(_jumpOn);
                top.Controls.Add(_jumpTo);
            }
            _jumpOn.CheckedChanged += (_, _) => { _jumpTo.Enabled = _jumpOn.Checked; StoreJump(); };
            _jumpTo.ValueChanged += (_, _) => StoreJump();

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
            right.Controls.Add(_info);

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

            // End markers can't be selected. A click on one is ignored (the selection stays where it was); arrow keys
            // step over it so the list can still be walked with the keyboard.
            _list.SelectedIndexChanged += (_, _) =>
            {
                int i = _list.SelectedIndex;
                if (!_loading && i >= 0 && i < _steps.Count && _steps[i].IsEnd)
                {
                    if (Control.MouseButtons != MouseButtons.None && _lastSelected >= 0 && _lastSelected < _steps.Count)
                    {
                        _list.SelectedIndex = _lastSelected; // the click doesn't count
                        return;
                    }
                    int dir = i >= _lastSelected ? 1 : -1;
                    int j = i;
                    while (j >= 0 && j < _steps.Count && _steps[j].IsEnd) j += dir;
                    if (j < 0 || j >= _steps.Count) { j = i; while (j >= 0 && j < _steps.Count && _steps[j].IsEnd) j -= dir; }
                    if (j >= 0 && j < _steps.Count) { _list.SelectedIndex = j; return; } // raises this event again for j
                }
                _lastSelected = i;
                ShowSelected();
            };
            // Opened for one kind of conversation (quest or idle): the other kind's steps are drawn grey.
            _list.DrawItem += (_, e) =>
            {
                if (e.Index < 0 || e.Index >= _list.Items.Count) return;
                e.DrawBackground();
                bool focused = _steps.Any(s => s.Highlighted);
                bool dim = focused && e.Index < _steps.Count && !_steps[e.Index].Highlighted;
                var color = (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : dim ? Color.Silver : SystemColors.WindowText;
                TextRenderer.DrawText(e.Graphics, _list.Items[e.Index].ToString(), e.Font, e.Bounds, color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                e.DrawFocusRectangle();
            };
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
                // Focused on quest or idle: open on that kind's first line of text, so the portrait preview shows straight away.
                int first = _steps.FindIndex(s => s.Highlighted && !s.IsScript && !s.IsLocked);
                if (first < 0) first = _steps.FindIndex(s => s.Highlighted);
                RebuildList(selectIndex: Math.Max(0, first));
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

        // Numbered from 0, the same numbers a script's `return N;` uses.
        private string LabelFor(int i)
        {
            var s = _steps[i];
            if (s.IsEnd)
                return s.NextTalk != null && _steps.Contains(s.NextTalk)
                    ? $"{i}   ---- end (next talk starts at step {_steps.IndexOf(s.NextTalk)}) ----"
                    : $"{i}   ---- end ----";
            if (s.RawEntry != null) return $"{i}   Jump to the table at 0x{0x08000000 | s.JumpTarget:X8}";
            string then = s.Next != null && _steps.Contains(s.Next) ? $"   (then step {_steps.IndexOf(s.Next)})" : "";
            return (s.IsScript ? $"{i}   Script: {FirstLine(s.Text)}" : $"{i}   {s.Character}: {FirstLine(s.Text)}") + then;
        }

        private void StoreJump()
        {
            if (_loading || _shown is not { IsScript: false, IsLocked: false }) return;
            int target = (int)_jumpTo.Value;
            _shown.Next = _jumpOn.Checked && target < _steps.Count ? _steps[target] : null;
            RefreshListLabelsQuiet();
        }

        // After steps were added, removed or moved: every script's `return N;` keeps pointing at the step it pointed
        // at before (`before` is the list as it was). A return to a step that is gone is left as it is.
        private void RenumberReturns(List<Step> before)
        {
            int NewIndex(int old) => old < before.Count ? _steps.IndexOf(before[old]) : -1;
            string Renumber(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\breturn\s+(\d+)\s*;", m =>
            {
                int now = NewIndex(int.Parse(m.Groups[1].Value));
                return now >= 0 ? $"return {now};" : m.Value;
            });

            foreach (var s in _steps.Where(s => s.IsScript && !s.IsLocked))
            {
                s.Text = Renumber(s.Text);
                s.BaselineText = Renumber(s.BaselineText);
                if (s.OriginalBytes == null) continue;

                // The same change in the kept bytes: a "go to step N" is PushByte N right before an END.
                var code = InstructionDecoder.Decode(s.OriginalBytes);
                for (int i = 0; i + 1 < code.Count; i++)
                {
                    if (code[i].Opcode != 0x00 || code[i + 1].Opcode != 0x11) continue;
                    int now = NewIndex((int)code[i].Args[0]);
                    if (now is >= 0 and <= 255) s.OriginalBytes[code[i].Offset + 1] = (byte)now;
                }
            }
            foreach (var s in _steps)
            {
                if (s.Next != null && !_steps.Contains(s.Next)) s.Next = null;
                if (s.NextTalk != null && !_steps.Contains(s.NextTalk)) { s.NextTalk = null; s.EndRaw = 0; } // its step is gone: back to the top
            }
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
            if (_shown == null || _shown.IsLocked) return;
            if (_shown.IsScript)
            {
                // Only a real edit counts: the editor control may hand the same text back with different line endings,
                // and "untouched" is what keeps a script's own bytes (see Step.TextUntouched).
                static string Lines(string s) => s.Replace("\r\n", "\n");
                if (!_shown.ScriptReadOnly && Lines(_script.Text) != Lines(_shown.Text)) _shown.Text = _script.Text;
                return;
            }
            _shown.Text = _message.Text;
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
            _script.Visible = _shown.IsScript && !_shown.IsLocked;
            _messagePanel.Visible = !_shown.IsScript && !_shown.IsLocked;
            _info.Visible = _shown.IsLocked;
            if (_shown.IsEnd)
                _info.Text = "End of a conversation.\r\n\r\nThe steps above run down to here and stop. Steps below belong to another conversation, " +
                             "started by a script's \"return N;\" (N = the number of its first step).\r\n\r\nThe table always finishes with one of these.";
            else if (_shown.RawEntry != null)
                _info.Text = $"Jump: the conversation carries on in another dialog table, at 0x{0x08000000 | _shown.JumpTarget:X8}.\r\n\r\nIt can be moved or removed here, but not edited.";
            else if (_shown.IsScript)
            {
                _script.ReadOnly = false;
                _script.Text = _shown.Text;
                _script.ReadOnly = _shown.ScriptReadOnly;
                if (_shown.ScriptReadOnly)
                    SetStatus("This script uses something the script editor can't write back yet, so it is read-only here and kept exactly as it is. It can still be moved or removed.", isError: true);
            }
            else
            {
                _message.Text = _shown.Text;
                _character.Value = _shown.Character;
                _position.SelectedIndex = (int)_shown.Position;
                _center.Checked = _shown.Center;
                int next = _shown.Next != null ? _steps.IndexOf(_shown.Next) : -1;
                _jumpOn.Checked = next >= 0;
                _jumpTo.Enabled = next >= 0;
                if (next >= 0) _jumpTo.Value = next;
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

        private void AddStep(bool isScript, bool isEnd = false)
        {
            CommitShown();
            var before = _steps.ToList();
            int at = _list.SelectedIndex + 1;
            // A new message starts with the speaker of the line above it.
            byte speaker = _steps.Take(at).LastOrDefault(s => !s.IsScript && !s.IsLocked)?.Character ?? 0;
            _steps.Insert(at, new Step { IsScript = isScript, IsEnd = isEnd, Character = speaker, Highlighted = at > 0 && _steps[at - 1].Highlighted });
            RenumberReturns(before);
            _shown = null; // the texts were just rewritten; don't write the editor's copy back over them
            RebuildList(at);
        }

        private void RemoveSelected()
        {
            int i = _list.SelectedIndex;
            if (i < 0) return;
            CommitShown();
            var before = _steps.ToList();
            _steps.RemoveAt(i);
            RenumberReturns(before);
            _shown = null;
            RebuildList(Math.Min(i, _steps.Count - 1));
        }

        private void MoveSelected(int delta)
        {
            CommitShown();
            int i = _list.SelectedIndex, j = i + delta;
            if (i < 0 || j < 0 || j >= _steps.Count) return;
            var before = _steps.ToList();
            (_steps[i], _steps[j]) = (_steps[j], _steps[i]);
            RenumberReturns(before);
            _shown = null;
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
                if (!_steps[i].IsScript || _steps[i].IsLocked) { compiled.Add(null); continue; }
                if (_steps[i].TextUntouched) { compiled.Add(_steps[i].OriginalBytes); continue; } // not edited: its own bytes, as they are
                try
                {
                    compiled.Add(ZenkaiAssembler.Assemble(_steps[i].Text));
                }
                catch (Exception ex)
                {
                    _list.SelectedIndex = i; // jump to the broken step
                    SetStatus($"Step {i}: {ex.Message}", isError: true);
                    return null;
                }
                if (_rawScript) continue;
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(_steps[i].Text, @"\breturn\s+(\d+)\s*;"))
                {
                    if (int.Parse(m.Groups[1].Value) < _steps.Count) continue;
                    _list.SelectedIndex = i;
                    SetStatus($"Step {i}: \"{m.Value}\" points past the last step ({_steps.Count - 1}).", isError: true);
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
                error = _commit(null, compiled.FirstOrDefault(c => c != null) ?? new byte[] { 0x11 }).Error;
            }
            else
            {
                if (_steps.Count == 0 || _steps[0].IsEnd)
                {
                    SetStatus("The first step has to be a message or a script.", isError: true);
                    return false;
                }
                // The table always closes with an End; add it rather than complain.
                if (!_steps[^1].IsEnd)
                {
                    _steps.Add(new Step { IsEnd = true });
                    compiled.Add(null);
                    _shown = null;
                    RebuildList(_list.SelectedIndex);
                }

                var slots = new List<DialogSlot>();
                for (int i = 0; i < _steps.Count; i++)
                {
                    var s = _steps[i];
                    if (s.IsEnd)
                    {
                        uint endValue = s.NextTalk != null ? (uint)_steps.IndexOf(s.NextTalk) : s.EndRaw;
                        slots.Add(new DialogSlot { IsEnd = true, EndValue = endValue });
                        continue;
                    }

                    int reuse = s.Unchanged ? s.OriginalEntryOffset : 0;
                    int next = s.Next != null ? _steps.IndexOf(s.Next) : -1;
                    if (s.RawEntry != null) { slots.Add(new DialogSlot { RawEntry = s.RawEntry, ReuseEntryOffset = reuse, NextSlot = next }); continue; }

                    string text = DialogPreview.ApplyFormat(s.Text.Replace("\r\n", "\n"), s.Position, s.Center);
                    var line = s.IsScript ? DialogLine.Script(compiled[i]!)
                        : s.ArgScript != null ? DialogLine.Interpolated(s.Character, text, s.ArgScript)
                        : DialogLine.TextLine(s.Character, text);
                    slots.Add(new DialogSlot { Line = line, ReuseEntryOffset = reuse, NextSlot = next });
                }

                var (commitError, offsets) = _commit(slots, null);
                error = commitError;
                if (error == null && offsets != null)
                {
                    // What is in the ROM now is the new baseline: the next Apply only writes what changes after this.
                    for (int i = 0; i < _steps.Count && i < offsets.Length; i++)
                    {
                        _steps[i].OriginalEntryOffset = offsets[i];
                        _steps[i].OriginalKey = _steps[i].Key();
                        if (_steps[i].IsScript && compiled[i] != null)
                        {
                            _steps[i].OriginalBytes = (byte[])compiled[i]!.Clone();
                            _steps[i].BaselineText = _steps[i].Text;
                        }
                    }
                }
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
