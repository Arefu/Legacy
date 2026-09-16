using ScintillaNET;

namespace Legacy.Zenkai
{
    /// <summary>
    /// Turns a plain ScintillaNET editor into a small Zenkai IDE: syntax highlighting
    /// (Scintilla's built-in Cpp lexer, a reasonable fit for Zenkai's C-like call syntax),
    /// Ctrl+Space opcode autocomplete with hints sourced from OpcodeDocs.xml, call tips on
    /// "(", and red squiggle underlines on syntax errors (debounced, via
    /// ZenkaiAssembler.Validate). Shared by every editor that edits Zenkai source so they
    /// can't drift out of sync with each other.
    /// </summary>
    internal static class ZenkaiEditor
    {
        private const int SquiggleIndicator = 8;
        private static readonly string[] JumpFamily = { "Jump", "JumpIfFalse", "LoopOrJump" };

        internal static void Configure(Scintilla sc)
        {
            sc.Lexer = Lexer.Cpp;

            string keywords = string.Join(" ",
                OpcodeTable.ByIndex.Select(o => o.Name).Concat(JumpFamily).Distinct());
            sc.SetKeywords(0, keywords);

            sc.StyleResetDefault();
            sc.Styles[Style.Default].Font = "Consolas";
            sc.Styles[Style.Default].Size = 10;
            sc.StyleClearAll();

            sc.Styles[Style.Cpp.Default].ForeColor = Color.Black;
            sc.Styles[Style.Cpp.Identifier].ForeColor = Color.Black;
            sc.Styles[Style.Cpp.Word].ForeColor = Color.Blue;
            sc.Styles[Style.Cpp.Word].Bold = true;
            sc.Styles[Style.Cpp.Number].ForeColor = Color.DarkRed;
            sc.Styles[Style.Cpp.CommentLine].ForeColor = Color.Green;
            sc.Styles[Style.Cpp.CommentLine].Italic = true;
            sc.Styles[Style.Cpp.Operator].ForeColor = Color.DimGray;

            sc.Margins[0].Width = 32; // line numbers

            sc.Indicators[SquiggleIndicator].Style = IndicatorStyle.Squiggle;
            sc.Indicators[SquiggleIndicator].ForeColor = Color.Red;

            sc.AutoCIgnoreCase = false;
            sc.AutoCMaxHeight = 9;

            var validateTimer = new System.Windows.Forms.Timer { Interval = 400 };
            validateTimer.Tick += (s, e) =>
            {
                validateTimer.Stop();
                Revalidate(sc);
            };

            sc.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Space)
                {
                    ShowAutoComplete(sc);
                    e.SuppressKeyPress = true;
                }
            };

            sc.CharAdded += (s, e) =>
            {
                if (char.IsLetter((char)e.Char) || e.Char == '_')
                    ShowAutoComplete(sc);
                else if (e.Char == '(')
                    ShowCallTip(sc);
                else if (e.Char == ')')
                    sc.CallTipCancel();
            };

            sc.TextChanged += (s, e) =>
            {
                validateTimer.Stop();
                validateTimer.Start();
            };

            Revalidate(sc);
        }

        private static void ShowAutoComplete(Scintilla sc)
        {
            int currentPos = sc.CurrentPosition;
            int wordStart = sc.WordStartPosition(currentPos, true);
            int lenEntered = currentPos - wordStart;

            string names = string.Join(" ",
                OpcodeTable.ByIndex.Select(o => o.Name).Concat(JumpFamily).Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            sc.AutoCShow(lenEntered, names);
        }

        private static void ShowCallTip(Scintilla sc)
        {
            int pos = sc.CurrentPosition;
            int nameEnd = pos - 1; // position of the "(" just typed
            int nameStart = sc.WordStartPosition(nameEnd, true);
            if (nameStart >= nameEnd) return;

            string opName = sc.GetTextRange(nameStart, nameEnd - nameStart);
            OpcodeTable.OpcodeInfo? op = null;
            if (OpcodeTable.NameMap.TryGetValue(opName, out var found)) op = found;

            OpcodeDocs.OpcodeDoc? doc = op != null ? OpcodeDocs.Find(op) : OpcodeDocs.Find(opName);
            if (doc == null) return;

            string tip = doc.Summary;
            if (doc.Params.Count > 0)
                tip += "\n" + string.Join("\n", doc.Params.Select(p => $"  {p.Name} - {p.Description}"));

            sc.CallTipShow(pos, tip);
        }

        private static void Revalidate(Scintilla sc)
        {
            sc.IndicatorCurrent = SquiggleIndicator;
            sc.IndicatorClearRange(0, sc.TextLength);

            List<ZenkaiDiagnostic> diagnostics;
            try
            {
                diagnostics = ZenkaiAssembler.Validate(sc.Text);
            }
            catch
            {
                return; // don't let a validator bug break editing
            }

            foreach (var d in diagnostics)
            {
                int lineIndex = d.Line - 1;
                if (lineIndex < 0 || lineIndex >= sc.Lines.Count) continue;

                int lineStart = sc.Lines[lineIndex].Position;
                int start = lineStart + Math.Max(0, d.Column);
                int length = Math.Min(d.Length, sc.TextLength - start);
                if (length <= 0) continue;

                sc.IndicatorFillRange(start, length);
            }
        }
    }
}
