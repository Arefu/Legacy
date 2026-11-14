namespace DBZKit
{
    /// <summary>
    /// The left-hand navigation that replaces the tab bar: the main TabControl keeps every page (so nothing else changes how it finds or
    /// selects them), its tab strip is hidden, and this list shows the pages in three sections -- Assets, Editors and Boot screens --
    /// with each asset section's count on the right. It follows the TabControl: pages added, renamed or selected elsewhere show here.
    /// </summary>
    internal sealed class NavigationBar : Control
    {
        private readonly TabControl _tabs;
        private readonly List<Entry> _entries = [];
        private int _hover = -1;

        private const int RowHeight = 30, HeaderHeight = 30, Indent = 16;
        private static readonly Color Back = Color.FromArgb(246, 247, 249);
        private static readonly Color Selected = Color.FromArgb(222, 232, 252);
        private static readonly Color Marker = Color.FromArgb(37, 99, 235);
        private static readonly Color HeaderText = Color.FromArgb(120, 126, 138);

        // Which section a page belongs to, and the name shown for it. Pages not listed here are assets.
        private static readonly Dictionary<string, (string Section, string Label)> Known = new()
        {
            ["Sprite editor"] = ("Editors", "Sprites"),
            ["Sound"] = ("Editors", "Sound & music"),
            ["Engine tools"] = ("Editors", "Characters & abilities"),
            ["Intro"] = ("Boot screens", "Intro"),
            ["Attract Mode"] = ("Boot screens", "Attract mode"),
            ["Title Screen"] = ("Boot screens", "Title screen"),
            ["Credits"] = ("Boot screens", "Credits"),
        };
        private static readonly string[] Sections = ["Assets", "Editors", "Boot screens"];

        private sealed record Entry(string Text, string? Count, TabPage? Page)
        {
            public bool IsHeader => Page == null;
        }

        public NavigationBar(TabControl tabs)
        {
            _tabs = tabs;
            Dock = DockStyle.Left;
            Width = 210;
            DoubleBuffered = true;
            BackColor = Back;
            Font = new Font("Segoe UI", 9.5f);
            tabs.SelectedIndexChanged += (_, _) => Invalidate();
            tabs.ControlAdded += (_, e) => { Watch(e.Control); Rebuild(); };
            tabs.ControlRemoved += (_, e) => BeginInvokeRebuild();
            foreach (TabPage page in tabs.TabPages) Watch(page);
            Rebuild();
        }

        /// <summary>
        /// Puts the TabControl in a panel that cuts off its tab strip: every page still lives in the TabControl (so nothing that finds or
        /// selects pages changes), only the strip is out of sight. The TabControl's own settings are left alone -- shrinking its strip
        /// (ItemSize, FlatButtons) or giving it a Region makes the SplitContainers in the pages throw in GDI+ while it is created.
        /// </summary>
        public static Panel HostWithoutStrip(TabControl tabs)
        {
            var host = new Panel { Dock = DockStyle.Fill };
            tabs.Parent?.Controls.Remove(tabs);
            tabs.Dock = DockStyle.None;
            host.Controls.Add(tabs);
            void Fit()
            {
                // The strip's height comes from the first tab; the frame around the page area is a few pixels on each side.
                int strip = tabs.TabCount > 0 ? tabs.GetTabRect(0).Bottom + 2 : tabs.ItemSize.Height + 4;
                tabs.Bounds = new Rectangle(-4, -strip, host.ClientSize.Width + 8, host.ClientSize.Height + strip + 4);
            }
            host.Resize += (_, _) => Fit();
            tabs.HandleCreated += (_, _) => Fit();
            tabs.ControlAdded += (_, _) => Fit();
            Fit();
            return host;
        }

        private void Watch(Control page) => page.TextChanged += (_, _) => Rebuild();

        private void BeginInvokeRebuild()
        {
            if (IsHandleCreated) BeginInvoke(Rebuild);
            else Rebuild();
        }

        private void Rebuild()
        {
            _entries.Clear();
            var pages = _tabs.TabPages.Cast<TabPage>().ToList();
            foreach (var section in Sections)
            {
                // "Resources" without a count means nothing in the ROM is left unidentified (or it is still being worked out): no entry.
                var inSection = pages.Where(p => p.Text != DrGero.Catalog.AssetCatalog.Unclassified
                                              && (Known.TryGetValue(p.Text, out var k) ? k.Section : "Assets") == section).ToList();
                if (inSection.Count == 0) continue;
                _entries.Add(new Entry(section.ToUpperInvariant(), null, null));
                foreach (var page in inSection)
                {
                    // Asset pages are titled "Characters (150)": the name on the left, the count on the right.
                    string text = page.Text, count = "";
                    int open = text.LastIndexOf(" (", StringComparison.Ordinal);
                    if (open > 0 && text.EndsWith(')')) { count = text[(open + 2)..^1]; text = text[..open]; }
                    if (Known.TryGetValue(page.Text, out var known)) text = known.Label;
                    _entries.Add(new Entry(text, count, page));
                }
            }
            Invalidate();
        }

        private int RowTop(int index)
        {
            int y = 8;
            for (int i = 0; i < index; i++) y += _entries[i].IsHeader ? HeaderHeight + (i > 0 ? 6 : 0) : RowHeight;
            if (_entries[index].IsHeader && index > 0) y += 6;
            return y;
        }

        private int RowAt(int y)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                int top = RowTop(i), h = _entries[i].IsHeader ? HeaderHeight : RowHeight;
                if (y >= top && y < top + h) return _entries[i].IsHeader ? -1 : i;
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Back);
            using var headerFont = new Font(Font.FontFamily, 8f, FontStyle.Bold);
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                int top = RowTop(i);
                if (entry.IsHeader)
                {
                    TextRenderer.DrawText(g, entry.Text, headerFont, new Rectangle(Indent, top, Width - Indent, HeaderHeight), HeaderText,
                        TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                    continue;
                }
                var row = new Rectangle(6, top, Width - 12, RowHeight);
                bool on = entry.Page == _tabs.SelectedTab;
                if (on || i == _hover)
                {
                    using var fill = new SolidBrush(on ? Selected : Color.FromArgb(234, 236, 240));
                    g.FillRectangle(fill, row);
                    if (on) using (var bar = new SolidBrush(Marker)) g.FillRectangle(bar, row.X, row.Y + 6, 3, row.Height - 12);
                }
                var text = new Rectangle(row.X + Indent - 4, row.Y, row.Width - Indent - 40, row.Height);
                TextRenderer.DrawText(g, entry.Text, Font, text, on ? Marker : ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (!string.IsNullOrEmpty(entry.Count))
                    TextRenderer.DrawText(g, entry.Count, Font, new Rectangle(row.Right - 48, row.Y, 40, row.Height), HeaderText,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
            }
            using var line = new Pen(Color.FromArgb(225, 228, 233));
            g.DrawLine(line, Width - 1, 0, Width - 1, Height);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int row = RowAt(e.Y);
            if (row != _hover) { _hover = row; Cursor = row >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int row = RowAt(e.Y);
            if (row >= 0 && _entries[row].Page is { } page) _tabs.SelectedTab = page;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }
    }
}
