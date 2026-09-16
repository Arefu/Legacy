using ScintillaNET;

namespace Legacy.Zenkai
{
    /// <summary>
    /// Turns a plain ScintillaNET editor into a small Zenkai IDE: syntax highlighting
    /// (Scintilla's built-in Cpp lexer, a reasonable fit for Zenkai's C-like call syntax),
    /// Ctrl+Space opcode autocomplete (with a Visual-Studio-style description call tip that
    /// follows the highlighted entry) sourced from OpcodeDocs.xml, call tips on "(" and on
    /// hover, and red squiggle underlines on syntax errors (debounced, via
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

            // Case-insensitive search (typing "pick" finds "PickUpItem"), but Scintilla still
            // inserts the list's own canonical casing on selection, not what was typed.
            sc.AutoCIgnoreCase = true;
            sc.AutoCMaxHeight = 9;

            // Where the current autocomplete word started - used both to re-anchor the
            // description call tip (AutoCSelection below) and, for the "(" case, to show
            // a call tip at the right spot.
            int autoCAnchor = -1;

            string AllNames() => string.Join(" ",
                OpcodeTable.ByIndex.Select(o => o.Name).Concat(JumpFamily).Distinct()
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

            void ShowAutoComplete()
            {
                int currentPos = sc.CurrentPosition;
                int wordStart = sc.WordStartPosition(currentPos, true);
                autoCAnchor = wordStart;
                int lenEntered = currentPos - wordStart;
                sc.AutoCShow(lenEntered, AllNames());
            }

            string? FindDocByOpName(string opName)
            {
                OpcodeDocs.OpcodeDoc? doc = OpcodeTable.NameMap.TryGetValue(opName, out var op)
                    ? OpcodeDocs.Find(op)
                    : OpcodeDocs.Find(opName);
                if (doc == null) return null;

                string tip = doc.Summary;
                if (doc.Params.Count > 0)
                    tip += "\n" + string.Join("\n", doc.Params.Select(p => $"  {p.Name} - {p.Description}"));
                return tip;
            }

            // wordPos: any position inside (or at the end of) the opcode name's word.
            // anchorPos: where Scintilla anchors the tip popup (caret for the type-"(" case,
            // the mouse position for hover).
            void ShowCallTipForWordAt(int wordPos, int anchorPos)
            {
                int nameStart = sc.WordStartPosition(wordPos, true);
                int nameEnd = sc.WordEndPosition(wordPos, true);
                if (nameStart >= nameEnd) { sc.CallTipCancel(); return; }

                string opName = sc.GetTextRange(nameStart, nameEnd - nameStart);
                string? tip = FindDocByOpName(opName);
                if (tip == null) { sc.CallTipCancel(); return; }

                sc.CallTipShow(anchorPos, tip);
            }

            void ShowCallTip()
            {
                int pos = sc.CurrentPosition;
                int nameEnd = pos - 1; // position of the "(" just typed - end of the word before it
                ShowCallTipForWordAt(nameEnd, pos);
            }

            // Hover call tips, driven off our own mouse-move + timer rather than Scintilla's
            // native SCN_DWELLSTART notification - the native dwell event turned out to not
            // reliably fire for this control/host combination, so polling
            // CharPositionFromPointClose ourselves is the version that actually works.
            int hoverPos = -1;
            var hoverTimer = new System.Windows.Forms.Timer { Interval = 500 };
            hoverTimer.Tick += (s, e) =>
            {
                hoverTimer.Stop();
                if (hoverPos >= 0)
                    ShowCallTipForWordAt(hoverPos, hoverPos);
            };
            sc.MouseMove += (s, e) =>
            {
                int pos = sc.CharPositionFromPointClose(e.X, e.Y);
                if (pos != hoverPos)
                {
                    hoverTimer.Stop();
                    sc.CallTipCancel();
                }
                hoverPos = pos;
                if (pos >= 0)
                    hoverTimer.Start();
            };
            sc.MouseLeave += (s, e) =>
            {
                hoverTimer.Stop();
                hoverPos = -1;
                sc.CallTipCancel();
            };

            // Visual-Studio-style description alongside the Ctrl+Space list: as the
            // highlighted entry changes (arrow keys or further typing narrowing the list),
            // show its doc summary as a call tip anchored at the word being completed.
            sc.AutoCSelection += (s, e) =>
            {
                string? tip = FindDocByOpName(e.Text);
                if (tip == null) { sc.CallTipCancel(); return; }
                sc.CallTipShow(autoCAnchor >= 0 ? autoCAnchor : e.Position, tip);
            };
            // After insertion, swap the description tip for the arg-template one (same
            // content "(" would trigger) so you immediately see what to type next.
            sc.AutoCCompleted += (s, e) =>
            {
                string? tip = FindDocByOpName(e.Text);
                if (tip == null) { sc.CallTipCancel(); return; }
                sc.CallTipShow(sc.CurrentPosition, tip);
            };
            sc.AutoCCancelled += (s, e) => sc.CallTipCancel();

            var validateTimer = new System.Windows.Forms.Timer { Interval = 400 };
            validateTimer.Tick += (s, e) =>
            {
                validateTimer.Stop();
                Revalidate(sc);
            };

            // Tab is a WinForms dialog-navigation key by default, so it never reaches
            // KeyDown unless we claim it first - without this, Tab while the autocomplete
            // list is open just moved focus to the next control instead of inserting.
            sc.PreviewKeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Tab && sc.AutoCActive)
                    e.IsInputKey = true;
            };

            sc.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Space)
                {
                    ShowAutoComplete();
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Tab && sc.AutoCActive)
                {
                    sc.AutoCComplete();
                    e.SuppressKeyPress = true;
                }
            };

            sc.CharAdded += (s, e) =>
            {
                if (char.IsLetter((char)e.Char) || e.Char == '_')
                    ShowAutoComplete();
                else if (e.Char == '(')
                    ShowCallTip();
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
