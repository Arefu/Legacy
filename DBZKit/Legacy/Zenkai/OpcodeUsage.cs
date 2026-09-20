using DrGero.IO;
using ScintillaNET;

namespace Legacy.Zenkai
{
    /// <summary>
    /// "Show all usage" for an action opcode: walks every script reachable from the ROM's map table (trigger / item / object dialogue,
    /// NPC and enemy conversations -- the same places Legacy's tree lists), decodes each one just far enough to find the action calls
    /// (Step 0x02 + index) and reports where each one is, with its literal arguments and the neighbouring calls. Shared by Legacy and the
    /// micro editor, which both attach it to their Scintilla editor via <see cref="Attach"/> (right-click).
    /// </summary>
    internal static class OpcodeUsage
    {
        internal sealed record Hit(string Where, uint ScriptAddr, uint CallAddr, string Args, string Context);

        const uint RomBase = 0x08000000;
        const uint DialogHandlerA = 0x0800D7E1, ScriptTriggerHandler = 0x0800C2C3;
        const uint NpcSpriteHandler = 0x0800B711, EnemyHandler = 0x0800E77F, EnemyRunDialogFunc = 0x080106B3;

        /// <summary>Every mode-0 (script) dialog entry reachable from the map table, as (label, address of the bytecode).</summary>
        internal static List<(string Label, uint Addr)> EnumerateScripts(byte[] rom)
        {
            var scripts = new List<(string, uint)>();
            bool Ok(uint a) => a >= RomBase && a + 4 <= RomBase + rom.Length;
            uint U32(uint a) => BitConverter.ToUInt32(rom, (int)(a - RomBase));
            byte U8(uint a) => rom[a - RomBase];

            void Walk(uint arr, string label)
            {
                if (!Ok(arr)) return;
                uint first = U32(arr);
                if (first == 0 || !Ok(first) || U8(first + 2) > 5) return;
                // Same extent rule as Legacy.WalkDialogArray: the table runs at least to the highest Index seen, and
                // the first non-pointer slot past that ends it (otherwise it runs on into the next table's entries).
                int extent = 0;
                bool pastEnd = false;
                for (int n = 1; Ok(arr + (uint)(n * 4) - 4); n++)
                {
                    uint p = U32(arr + (uint)((n - 1) * 4));
                    if (p == 0) break;
                    if (!Ok(p)) { if (n - 1 > extent) pastEnd = true; continue; }
                    if (pastEnd) continue;
                    extent = Math.Max(extent, BitConverter.ToUInt16(rom, (int)(p - RomBase)));
                    if (U8(p + 2) == 0) scripts.Add(($"{label} - Dialog #{n}", p + 3));
                }
            }

            uint table = 0x08409368;
            int count = 0x147;
            try
            {
                var view = ROM.FromBytes(rom);
                if (DrGero.Engine.RosterTables.IsSupportedRom(view))
                {
                    table = RomBase | (uint)DrGero.Engine.MapTable.Address(view);
                    count = DrGero.Engine.MapTable.Count(view);
                }
            }
            catch (InvalidOperationException) { }

            for (int i = 0; i < count; i++)
            {
                uint e = table + (uint)(i * 0x38);
                if (!Ok(e + 0x38)) break;
                string L = $"Zone {U8(e)} Area {U8(e + 1)}";
                byte tc = U8(e + 3), sc = U8(e + 4), ic = U8(e + 5), oc = U8(e + 6);
                uint tp = U32(e + 0x10), sp = U32(e + 0x14), ip = U32(e + 0x18), op = U32(e + 0x1C);
                if (tc != 0 && Ok(tp))
                    for (int t = 0; t < tc; t++)
                    {
                        uint x = U32(tp + (uint)(t * 8 + 4));
                        if (!Ok(x + 0x14)) continue;
                        // Same trigger kinds as Legacy's tree: 0x0800D7E1 = dialog table, 0x0800C2C3 = one plain script.
                        if (U32(x) == DialogHandlerA) Walk(U32(x + 16), $"{L} Trigger[{t}]");
                        else if (U32(x) == ScriptTriggerHandler && Ok(U32(x + 16))) scripts.Add(($"{L} Trigger[{t}] - Script", U32(x + 16)));
                    }
                if (ic != 0 && Ok(ip))
                    for (int t = 0; t < ic; t++)
                    {
                        uint x = U32(ip + (uint)(t * 4));
                        if (Ok(x + 0x10)) Walk(U32(x + 0xC), $"{L} Item {t}");
                    }
                if (oc != 0 && Ok(op))
                    for (int t = 0; t < oc; t++)
                    {
                        uint x = U32(op + (uint)(t * 4));
                        if (Ok(x + 0x18)) Walk(U32(x + 0x14), $"{L} Object {t}");
                    }
                if (sc != 0 && Ok(sp))
                    for (int k = 0; k < sc; k++)
                    {
                        uint r = U32(sp + (uint)(k * 4));
                        if (!Ok(r + 0x1C)) continue;
                        uint h = U32(r);
                        if (h == NpcSpriteHandler) Walk(U32(r + 0xC), $"{L} NPC {k}");
                        else if (h == EnemyHandler)
                        {
                            uint ao = U32(r + 0x14);
                            if (Ok(ao + 8) && U32(ao) == EnemyRunDialogFunc) Walk(U32(ao + 4), $"{L} Enemy {k}");
                        }
                    }
            }
            return scripts;
        }

        static string Name(int index) => OpcodeTable.IndexMap.TryGetValue(index, out var o) ? o.Name : $"op{index}";

        /// <summary>Decodes one script's action calls: (call address, opcode index, args as text -- "?" when computed at run time).</summary>
        static List<(uint Addr, int Index, string[] Args)> Calls(byte[] rom, uint addr)
        {
            var result = new List<(uint, int, string[])>();
            var stack = new List<string>();
            int pos = (int)(addr - RomBase);
            int limit = Math.Min(rom.Length, pos + 0x800);

            // Same encoding as ZenkaiDisassembler.ReadVarint + SView_Tools.ZigZagDecode: 7-bit groups, MOST significant
            // group first, and the low bit is the sign (odd = negative).
            long Varint(ref int p)
            {
                long v = 0;
                while (p < limit) { byte b = rom[p++]; v = (v << 7) | (uint)(b & 0x7F); if ((b & 0x80) == 0) break; }
                return (v & 1) != 0 ? -(v >> 1) : (v >> 1);
            }
            void Pop(int n) { for (int i = 0; i < n && stack.Count > 0; i++) stack.RemoveAt(stack.Count - 1); }

            while (pos < limit)
            {
                int start = pos;
                byte op = rom[pos++];
                if (op == 0x00) { stack.Add(rom[pos++].ToString()); }
                else if (op == 0x01) stack.Add(Varint(ref pos).ToString());
                else if (op == 0x02)
                {
                    int idx = rom[pos++];
                    int n = OpcodeTable.IndexMap.TryGetValue(idx, out var info) ? info.Arity : 0;
                    var args = new string[n];
                    for (int i = 0; i < n; i++) { int at = stack.Count - n + i; args[i] = at >= 0 && at < stack.Count ? stack[at] : "?"; }
                    Pop(n);
                    result.Add((RomBase + (uint)start, idx, args));
                }
                else if (op == 0x03) pos++;
                else if (op is >= 0x04 and <= 0x10) { Pop(op == 0x05 || op == 0x06 ? 1 : 2); stack.Add("?"); }
                else if (op == 0x11) break;
                else if (op is 0x12 or 0x1C) pos++;
                else if (op == 0x13) { Pop(1); pos++; }
                else if (op == 0x14 || op == 0x1B) Pop(1);
                else if (op is >= 0x15 and <= 0x1A) pos++;
                else if (op == 0x1D) { int c = rom[pos++]; for (int i = 0; i < c && pos < limit; i++) stack.Add(Varint(ref pos).ToString()); }
                else break; // not script bytecode
            }
            return result;
        }

        internal static List<Hit> Find(byte[] rom, int opcodeIndex, List<(string Label, uint Addr)>? scripts = null)
        {
            var hits = new List<Hit>();
            foreach (var (label, addr) in scripts ?? EnumerateScripts(rom))
            {
                var calls = Calls(rom, addr);
                for (int k = 0; k < calls.Count; k++)
                {
                    if (calls[k].Index != opcodeIndex) continue;
                    var ctx = calls.Skip(Math.Max(0, k - 2)).Take(5).Select(c => Name(c.Index));
                    hits.Add(new Hit(label, addr, calls[k].Addr, $"({string.Join(", ", calls[k].Args)})", string.Join(" > ", ctx)));
                }
            }
            return hits;
        }

        /// <summary>Adds a right-click menu to a Zenkai editor: "Show all usage of &lt;opcode under the cursor&gt;" and "Find opcode usage...".</summary>
        internal static void Attach(Scintilla editor, Func<byte[]?> rom, Action<Hit>? navigate = null)
        {
            var menu = new ContextMenuStrip();
            var usage = new ToolStripMenuItem("Show all usage");
            var pick = new ToolStripMenuItem("Find opcode usage...");
            menu.Items.Add(usage);
            menu.Items.Add(pick);
            editor.ContextMenuStrip = menu;

            string? wordUnderMouse = null;
            editor.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                int p = editor.CharPositionFromPoint(e.X, e.Y);
                string w = editor.GetWordFromPosition(p);
                wordUnderMouse = OpcodeTable.NameMap.ContainsKey(w) ? w : null;
                usage.Text = wordUnderMouse == null ? "Show all usage (right-click on an opcode name)" : $"Show all usage of {wordUnderMouse}";
                usage.Enabled = wordUnderMouse != null;
            };
            usage.Click += (_, _) => Show(editor, rom(), wordUnderMouse, navigate);
            pick.Click += (_, _) => Show(editor, rom(), null, navigate);
        }

        internal static void Show(IWin32Window owner, byte[]? rom, string? opcodeName, Action<Hit>? navigate)
        {
            if (rom == null) { MessageBox.Show(owner, "No ROM is loaded.", "Opcode usage"); return; }
            using var form = new UsageForm(rom, opcodeName, navigate);
            form.ShowDialog(owner);
        }

        sealed class UsageForm : Form
        {
            readonly byte[] _rom;
            readonly List<(string Label, uint Addr)> _scripts;
            readonly ComboBox _op = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
            readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false };
            readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 22, Padding = new Padding(4, 3, 0, 0) };
            List<Hit> _hits = new();
            readonly Action<Hit>? _navigate;
            readonly OpcodeTable.OpcodeInfo[] _ops = OpcodeTable.ByIndex.OrderBy(o => o.Index).ToArray();

            public UsageForm(byte[] rom, string? preselect, Action<Hit>? navigate)
            {
                _rom = rom; _navigate = navigate;
                _scripts = EnumerateScripts(rom);
                Text = "Opcode usage";
                Size = new Size(1000, 560);
                StartPosition = FormStartPosition.CenterParent;

                foreach (var o in _ops) _op.Items.Add($"{o.Index,3}  {o.Name}");
                var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(4) };
                top.Controls.Add(new Label { Text = "Opcode:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
                top.Controls.Add(_op);
                top.Controls.Add(new Label
                {
                    Text = navigate != null ? "double-click a row to open that script" : "double-click a row to copy its address",
                    AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(12, 6, 0, 0)
                });

                _list.Columns.Add("Script", 260);
                _list.Columns.Add("Address", 90);
                _list.Columns.Add("Arguments", 240);
                _list.Columns.Add("Neighbouring calls", 360);
                Controls.Add(_list); Controls.Add(top); Controls.Add(_status);

                _op.SelectedIndexChanged += (_, _) => Search();
                _list.DoubleClick += (_, _) =>
                {
                    if (_list.SelectedIndices.Count == 0) return;
                    var hit = _hits[_list.SelectedIndices[0]];
                    if (_navigate != null) { _navigate(hit); Close(); }
                    else { Clipboard.SetText($"0x{hit.CallAddr:X8}"); _status.Text = $"Copied 0x{hit.CallAddr:X8}"; }
                };

                int sel = preselect == null ? 0 : Array.FindIndex(_ops, o => o.Name == preselect);
                _op.SelectedIndex = Math.Max(0, sel);
            }

            void Search()
            {
                int index = _ops[_op.SelectedIndex].Index;
                _hits = Find(_rom, index, _scripts);
                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (var h in _hits)
                    _list.Items.Add(new ListViewItem(new[] { h.Where, $"0x{h.CallAddr:X8}", h.Args, h.Context }));
                _list.EndUpdate();
                _status.Text = $"{_hits.Count} use(s) in {_hits.Select(h => h.ScriptAddr).Distinct().Count()} of {_scripts.Count} scripts";
            }
        }
    }
}
