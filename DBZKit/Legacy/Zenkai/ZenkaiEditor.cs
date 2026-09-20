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
    // PUBLIC 2026-09-19 (was internal) -- Dragon Radar's trigger/dialog script editor
    // (ScriptEditorForm.cs) references this project to reuse the real Zenkai editor
    // (highlighting, autocomplete, var/if/else) instead of duplicating any of it.
    public static class ZenkaiEditor
    {
        private const int SquiggleIndicator = 8;
        private static readonly string[] JumpFamily = { "Jump", "JumpIfFalse", "LoopOrJump" };
        private static readonly string[] LanguageKeywords = { "var", "if", "else", "return" };

        // `var name = Call(...)` declarations and `label:` lines in the current text -- what
        // a real IntelliSense offers alongside the built-in names. Rescanned per popup
        // (scripts are tiny, so this is cheaper than tracking edits).
        private static readonly System.Text.RegularExpressions.Regex VarDeclRx =
            new(@"\bvar\s+([A-Za-z_]\w*)\s*=\s*([^;\r\n]*)", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex LabelRx =
            new(@"^[ \t]*([A-Za-z_]\w*)[ \t]*:", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.Multiline);
        private static readonly System.Text.RegularExpressions.Regex JumpArgContextRx =
            new(@"\b(?:Jump|JumpIfFalse|LoopOrJump)\s*\(\s*\w*$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static (Dictionary<string, string> Vars, Dictionary<string, int> Labels) ScanSymbols(string text)
        {
            var vars = new Dictionary<string, string>();
            foreach (System.Text.RegularExpressions.Match m in VarDeclRx.Matches(text))
                vars.TryAdd(m.Groups[1].Value, m.Groups[2].Value.Trim());

            var labels = new Dictionary<string, int>();
            foreach (System.Text.RegularExpressions.Match m in LabelRx.Matches(text))
                labels.TryAdd(m.Groups[1].Value, text.Take(m.Index).Count(c => c == '\n') + 1);
            return (vars, labels);
        }

        public static void Configure(Scintilla sc)
        {
            sc.Lexer = Lexer.Cpp;

            string keywords = string.Join(" ",
                OpcodeTable.ByIndex.Select(o => o.Name).Concat(JumpFamily).Distinct());
            sc.SetKeywords(0, keywords);

            // Language-level keywords (var/if/else -- see Zenkai.g4 and
            // Zenkai-Vars-And-If.md) go in Scintilla's SECOND keyword class (styled via
            // Word2 below) so they're visually distinct from opcode names -- these aren't
            // opcodes at all, they're the source-level var/if-else layer that compiles
            // down to real opcode calls underneath. (Same fix as ScriptVisualizer.cs's
            // ConfigureZenkaiEditor -- this is a SEPARATE editor config, they don't share
            // state, so both need it.)
            sc.SetKeywords(1, "var if else return");

            sc.StyleResetDefault();
            sc.Styles[Style.Default].Font = "Consolas";
            sc.Styles[Style.Default].Size = 10;
            sc.StyleClearAll();

            sc.Styles[Style.Cpp.Default].ForeColor = Color.Black;
            sc.Styles[Style.Cpp.Identifier].ForeColor = Color.Black;
            sc.Styles[Style.Cpp.Word].ForeColor = Color.Blue;
            sc.Styles[Style.Cpp.Word].Bold = true;
            sc.Styles[Style.Cpp.Word2].ForeColor = Color.Purple;
            sc.Styles[Style.Cpp.Word2].Bold = true;
            sc.Styles[Style.Cpp.Number].ForeColor = Color.DarkRed;
            sc.Styles[Style.Cpp.CommentLine].ForeColor = Color.Green;
            sc.Styles[Style.Cpp.CommentLine].Italic = true;
            sc.Styles[Style.Cpp.Operator].ForeColor = Color.DimGray;

            sc.Margins[0].Width = 32; // line numbers

            sc.Indicators[SquiggleIndicator].Style = IndicatorStyle.Squiggle;
            sc.Indicators[SquiggleIndicator].ForeColor = Color.Red;

            // VS-light-theme-style call tip: white background, near-black body text, the
            // opcode name highlighted in VS's signature-help blue via CallTipSetHlt (a
            // plain character-offset range, set per-call in FindDocByOpName's callers --
            // NOT via embedded \x01/\x02 control codes, which actually draw clickable
            // up/down-arrow buttons in real Scintilla, not colored text). Back/fore colors
            // for the tip body come from the Style.CallTip style itself, not a separate
            // setter -- this ScintillaNET version doesn't expose CallTipSetBack/Fore/
            // UseStyle at all (checked via reflection), only ForeHlt/SetHlt.
            sc.Styles[Style.CallTip].Font = "Consolas";
            sc.Styles[Style.CallTip].Size = 9;
            sc.Styles[Style.CallTip].BackColor = Color.White;
            sc.Styles[Style.CallTip].ForeColor = Color.FromArgb(30, 30, 30);
            sc.CallTipSetForeHlt(Color.FromArgb(0, 90, 180));

            // Case-insensitive search (typing "pick" finds "PickUpItem"), but Scintilla still
            // inserts the list's own canonical casing on selection, not what was typed.
            sc.AutoCIgnoreCase = true;
            sc.AutoCMaxHeight = 9;
            // Filtering now reorders by relevance (prefix matches before substring
            // matches, see ShowAutoComplete below) rather than staying alphabetical, so
            // Scintilla can no longer assume the list is presorted for its own internal
            // jump-to-typed-text lookup -- PerformSort has it sort (a copy) itself instead.
            sc.AutoCOrder = Order.PerformSort;
            // CONFIRMED (2026-09, real repro): defaults to true, and is exactly what was
            // fighting ShowAutoComplete's own re-filtering -- Scintilla auto-cancels/
            // narrows the ALREADY-VISIBLE list by PREFIX on every keystroke regardless of
            // what we re-show it with (e.g. after "tr" shows SetCharacterTransformation as
            // a substring match, typing "a" makes Scintilla check "does 'SetCharacter...'
            // start with 'tra'?", find no, and collapse to empty before/alongside our own
            // recompute). Turning this off hands 100% of show/hide control to our own
            // FuzzyFilterNames-driven re-show, which is already correct on its own.
            sc.AutoCAutoHide = false;

            // Where the current autocomplete word started - used both to re-anchor the
            // description call tip (AutoCSelection below) and, for the "(" case, to show
            // a call tip at the right spot.
            int autoCAnchor = -1;

            string AllNames() => string.Join(" ",
                OpcodeTable.ByIndex.Where(o => !OpcodeTable.UnusedInGame.Contains(o.Index)).Select(o => o.Name).Concat(JumpFamily).Distinct()
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

            void ShowAutoComplete()
            {
                int currentPos = sc.CurrentPosition;
                int wordStart = sc.WordStartPosition(currentPos, true);
                autoCAnchor = wordStart;
                int lenEntered = currentPos - wordStart;
                string typed = sc.GetTextRange(wordStart, lenEntered);

                // CONFIRMED (2026-09, real repro): Scintilla applies its OWN passive
                // prefix-only narrowing to the list ALREADY on screen every time a
                // character is typed while AutoC is active -- independent of, and BEFORE,
                // this handler re-computes anything. So e.g. after "tr" shows a substring
                // match like SetCharacterTransformation, typing "a" makes Scintilla check
                // "does the CURRENTLY VISIBLE text start with 'tra'?", find nothing (it
                // matched as a substring, not a prefix), and collapse the list to empty --
                // then this handler's own re-show runs on top of that already-broken
                // state. Explicitly cancelling first forces a clean full re-show from our
                // own filtered list every keystroke instead of ever hitting that path.
                string before = sc.GetTextRange(sc.Lines[sc.LineFromPosition(wordStart)].Position,
                    wordStart - sc.Lines[sc.LineFromPosition(wordStart)].Position);

                // Typing the NAME of a new variable -- nothing sensible to suggest.
                if (before.TrimEnd().EndsWith("var") || (typed.Length > 0 && before.EndsWith("var "))) return;

                var (vars, labels) = ScanSymbols(sc.Text);

                // Inside Jump(...)/JumpIfFalse(...)/LoopOrJump(...) only labels make sense.
                IEnumerable<string> pool = JumpArgContextRx.IsMatch(before + typed)
                    ? labels.Keys
                    : AllNames().Split(' ').Concat(LanguageKeywords).Concat(vars.Keys).Concat(labels.Keys).Distinct();

                var filtered = OpcodeTable.FuzzyFilterNames(typed, pool.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray());
                if (sc.AutoCActive) sc.AutoCCancel();
                if (filtered.Count == 0) return;

                sc.AutoCShow(lenEntered, string.Join(" ", filtered));
            }

            // (tip text, highlight length) -- highlight length is how many characters
            // at the START of the tip (the opcode name) to color via CallTipSetHlt.
            // NOTE: \x01/\x02 are NOT generic highlight markers -- in real Scintilla
            // they draw actual clickable up/down-arrow buttons (multi-signature paging).
            // An earlier pass here wrapped text in them expecting colored text and got
            // literal broken-looking arrow buttons instead. The real highlight API is
            // CallTipSetHlt(start, end), a plain character-offset range with no embedded
            // control codes at all -- used below instead.
            (string Tip, int HighlightLength)? FindDocByOpName(string opName)
            {
                // Same fuzzy resolution ZenkaiAssembler uses at build time (exact ->
                // case-insensitive -> substring -> typo-tolerant) -- so hovering/typing
                // "transformation" finds SetCharacterTransformation's docs too, not just
                // an exact, correctly-cased name.
                OpcodeDocs.OpcodeDoc? doc = OpcodeTable.TryResolveFuzzy(opName, out var op, out _) && op != null
                    ? OpcodeDocs.Find(op)
                    : OpcodeDocs.Find(opName);
                if (doc == null) return null;

                string tip = doc.Name + "\n" + doc.Summary;
                if (doc.Params.Count > 0)
                    tip += "\n" + string.Join("\n", doc.Params.Select(p => $"  {p.Name} - {p.Description}"));
                return (tip, doc.Name.Length);
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

                // Hovering a variable / label shows what it is, like a real IDE.
                var (symVars, symLabels) = ScanSymbols(sc.Text);
                if (symVars.TryGetValue(opName, out var varDef))
                {
                    sc.CallTipShow(anchorPos, $"var {opName} = {varDef}\n(every use re-runs this call -- no VM storage)");
                    sc.CallTipSetHlt(0, 3 + 1 + opName.Length);
                    return;
                }
                if (symLabels.TryGetValue(opName, out int labelLine))
                {
                    sc.CallTipShow(anchorPos, $"label {opName} (line {labelLine})");
                    sc.CallTipSetHlt(0, 6 + opName.Length);
                    return;
                }
                var found = FindDocByOpName(opName);
                if (found == null) { sc.CallTipCancel(); return; }

                sc.CallTipShow(anchorPos, found.Value.Tip);
                sc.CallTipSetHlt(0, found.Value.HighlightLength);
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

            // After insertion, swap the description tip for the arg-template one (same
            // content "(" would trigger) so you immediately see what to type next.
            sc.AutoCCompleted += (s, e) =>
            {
                var found = FindDocByOpName(e.Text);
                if (found == null) { sc.CallTipCancel(); return; }
                sc.CallTipShow(sc.CurrentPosition, found.Value.Tip);
                sc.CallTipSetHlt(0, found.Value.HighlightLength);
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
                    // AutoCComplete() commits the selected item but, unlike Enter/
                    // double-click, doesn't reliably raise the AutoCCompleted .NET event
                    // in this ScintillaNET version -- so the tab-complete path was
                    // silently skipping both the "(" and the call tip that follow a
                    // normal completion. Doing both explicitly here instead of relying on
                    // that event firing.
                    sc.AutoCComplete();
                    sc.AddText("(");
                    ShowCallTip();
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
