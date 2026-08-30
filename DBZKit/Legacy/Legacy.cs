using System.Text;

namespace Legacy
{
    public partial class Legacy : Form
    {
        byte[]? _rom;
        List<byte> _extension = new(); // bytes appended past original ROM on save
        bool _dirtyPending = false;

        public Legacy()
        {
            InitializeComponent();
        }

        const uint RomBase = 0x08000000;
        const uint DialogHandlerA = 0x0800D7E1; // MapTrigger_OnEnter+1
        const uint DialogHandlerB = 0x0800D765; // sub_800D764+1

        const int MapEntrySize = 0x38;
        const int TriggerCountOffset = 0x03;
        const int MapTriggersPtrOffset = 0x10;

        const uint MapEntriesAddr = 0x08409368;
        const int MapCount = 0x147;

        const int DialogSequenceTableEntrySize = 4; // DCD pointer table, 0-terminated
        const int DialogHeaderSize = 4; // Index(u16) + Mode(u8) + Character(u8)
        const int MaxCompressedSliceLength = 4096; // generous max, decoders are self-terminating

        const uint PortraitTable = 0x083EC9D4;
        const int PortraitTotalEntries = 115;
        const uint PalettePtr = 0x081DA6C8;

        int _originalRomLength;

        uint ReadU32(uint addr) => BitConverter.ToUInt32(_rom, (int)(addr - RomBase));
        ushort ReadU16(uint addr) => BitConverter.ToUInt16(_rom, (int)(addr - RomBase));
        short ReadS16(uint addr) => BitConverter.ToInt16(_rom, (int)(addr - RomBase));
        uint RomEnd => RomBase + (uint)_rom.Length;
        bool IsValidPtr(uint addr) => addr >= RomBase && addr < RomEnd;
        byte ReadU8(uint addr)
        {
            uint off = addr - RomBase;
            if (off >= _rom.Length)
                throw new Exception($"ReadU8 OOB: addr=0x{addr:X8} off=0x{off:X8} romLen=0x{_rom.Length:X8}");
            return _rom[off];
        }

        private string ReadNullTerminatedString(uint addr)
        {
            var bytes = new List<byte>();
            uint offset = addr - RomBase;
            while (offset < _rom.Length && _rom[offset] != 0)
            {
                bytes.Add(_rom[offset]);
                offset++;
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private string ReadJcalg1String(uint addr)
        {
            uint offset = addr - RomBase;
            int length = Math.Min(MaxCompressedSliceLength, _rom.Length - (int)offset);
            byte[] slice = new byte[length];
            Array.Copy(_rom, offset, slice, 0, length);

            try
            {
                var decompressed = Jcalg1Decompress.Decompress(slice).Data;

                for (int i = 0; i < decompressed.Length; i++)
                {
                    if (decompressed[i] == 0x0B) decompressed[i] = (byte)'\n';
                }

                int nullIndex = Array.IndexOf(decompressed, (byte)0);
                if (nullIndex >= 0)
                    decompressed = decompressed[..nullIndex];

                return Encoding.ASCII.GetString(decompressed).Replace("\n", "\r\n");
            }
            catch (Exception)
            {
                return "[JCALG1 decompression failed]";
            }
        }

        private string ReadScriptText(uint addr)
        {
            uint offset = addr - RomBase;
            int length = Math.Min(MaxCompressedSliceLength, _rom.Length - (int)offset);
            byte[] slice = new byte[length];
            Array.Copy(_rom, offset, slice, 0, length);

            var instructions = SView_Decoder.Decode(slice);
            var sb = new StringBuilder();

            foreach (var ins in instructions)
            {
                sb.Append(ins.Offset.ToString("X4")).Append("  ").Append(ins.Name);
                if (ins.Args != null && ins.Args.Count > 0)
                    sb.Append("  [").Append(string.Join(", ", ins.Args)).Append(']');
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // Renders a single portrait by index directly from the ROM's portrait table
        private Bitmap? RenderPortrait(byte index)
        {
            if (_rom == null || index >= PortraitTotalEntries)
                return null;

            uint pointer = GBA.ReadUInt32(_rom, PortraitTable + (uint)(index * 4));
            if (pointer == 0)
                return null;

            try
            {
                int deflatedSize = GBA.ReadInt32(_rom, pointer + 4);
                int dataOffset = GBA.ToOffset(pointer) + 8;

                var palette = GBA.ReadPalette(_rom, PalettePtr);
                var result = Jcalg1Decompress.Decompress(_rom, dataOffset, deflatedSize);

                return GBA.Render8bpp(result.Data, 64, 64, palette);
            }
            catch
            {
                return null;
            }
        }
        private bool _suppressCharacterChanged = false;

        private void UpdateCharacterPreview(byte characterId)
        {
            _suppressCharacterChanged = true;
            Legacy_CharacterUpDown.Minimum = 0;
            Legacy_CharacterUpDown.Maximum = PortraitTotalEntries - 1;
            Legacy_CharacterUpDown.Value = characterId;
            _suppressCharacterChanged = false;

            Legacy_CharacterPreview.Image = RenderPortrait(characterId);
            Legacy_CharacterLabel.Text = $"Character ID: {characterId}";
        }

        private void Legacy_CharacterUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (_suppressCharacterChanged) return;

            if (_currentNode?.Tag is DialogNodeInfo info && info.Editable)
            {
                byte newId = (byte)Legacy_CharacterUpDown.Value;
                info.Character = newId;
                info.Dirty = true;

                Legacy_CharacterPreview.Image = RenderPortrait(newId);
                Legacy_CharacterLabel.Text = $"Character ID: {newId}";
            }
        }
        private void ClearCharacterPreview()
        {
            Legacy_CharacterPreview.Image = null;
            Legacy_CharacterLabel.Text = "";
        }

        // Holds enough info to rewrite an edited dialog entry back into the ROM extension area
        private class DialogNodeInfo
        {
            public uint TableSlotAddr;   // address of the 4-byte pointer in the DCD table that points at this entry
            public byte Mode;            // original mode (0=Script,1=Text,2=Jcalg1Text,...)
            public ushort Index;         // original Index field
            public byte Character;       // original DialogSprite/Character byte
            public string Text = "";     // current text (editable for Mode 1/2)
            public bool Editable;        // true only for Mode 1/2
            public bool Dirty;           // true once user has changed the text
        }

        private void PopulateDialogTree()
        {
            Legacy_ScriptFunctions.Nodes.Clear();
            _extension.Clear();
            _originalRomLength = _rom!.Length;

            for (int i = 0; i < MapCount; i++)
            {
                uint entryAddr = MapEntriesAddr + (uint)(i * MapEntrySize);
                byte zone = ReadU8(entryAddr + 0x00);
                byte area = ReadU8(entryAddr + 0x01);
                byte triggerCount = ReadU8(entryAddr + TriggerCountOffset);
                uint triggersPtr = ReadU32(entryAddr + MapTriggersPtrOffset);

                if (triggerCount == 0 || !IsValidPtr(triggersPtr)) continue;

                TreeNode zoneAreaNode = null;

                for (int t = 0; t < triggerCount; t++)
                {
                    uint entryPtr = triggersPtr + (uint)(t * 8);
                    uint triggerPtr = ReadU32(entryPtr + 4);

                    if (!IsValidPtr(triggerPtr)) continue; // filler/number, not a pointer

                    uint h1 = ReadU32(triggerPtr);
                    uint h2 = ReadU32(triggerPtr + 4);
                    if (h1 != DialogHandlerA || h2 != DialogHandlerB) continue;

                    uint dialogArrayPtr = ReadU32(triggerPtr + 16);
                    if (!IsValidPtr(dialogArrayPtr)) continue;

                    if (zoneAreaNode == null)
                    {
                        zoneAreaNode = new TreeNode($"Z{zone}A{area}");
                        Legacy_ScriptFunctions.Nodes.Add(zoneAreaNode);
                    }

                    // Walk the 0-terminated DCD pointer table
                    int seq = 0;
                    uint tableCursor = dialogArrayPtr;
                    while (true)
                    {
                        uint dialogPtr = ReadU32(tableCursor);
                        if (dialogPtr == 0) break;

                        seq++;

                        if (!IsValidPtr(dialogPtr))
                        {
                            tableCursor += DialogSequenceTableEntrySize;
                            continue;
                        }

                        byte mode = ReadU8(dialogPtr + 2); // DialogMode offset within entry
                        ushort index = ReadU16(dialogPtr + 0);
                        byte character = ReadU8(dialogPtr + 3);

                        string text = "";
                        bool editable = false;

                        if (mode == 0x0) // DIALOG_SCRIPT
                        {
                            text = ReadScriptText(dialogPtr + DialogHeaderSize);
                        }
                        else if (mode == 0x1) // DIALOG_TEXT
                        {
                            text = ReadNullTerminatedString(dialogPtr + DialogHeaderSize);
                            editable = true;
                        }
                        else if (mode == 0x2) // DIALOG_TEXT_JCALG1
                        {
                            text = ReadJcalg1String(dialogPtr + DialogHeaderSize);
                            editable = true;
                        }

                        var info = new DialogNodeInfo
                        {
                            TableSlotAddr = tableCursor,
                            Mode = mode,
                            Index = index,
                            Character = character,
                            Text = text,
                            Editable = editable,
                            Dirty = false
                        };

                        var dialogNode = new TreeNode($"Z{zone}A{area} - Dialog #{seq}") { Tag = info };
                        zoneAreaNode.Nodes.Add(dialogNode);

                        tableCursor += DialogSequenceTableEntrySize;
                    }
                }
            }
        }

        private TreeNode? _currentNode;

        private void Legacy_ScriptFunctions_AfterSelect(object sender, TreeViewEventArgs e)
        {
            CommitPendingEdit();
            _currentNode = e.Node;

            if (e.Node.Tag is DialogNodeInfo info)
            {
                if (info.Mode == 0x0) // DIALOG_SCRIPT
                {
                    Legacy_IDE.Text = info.Text;
                    Legacy_TextBox.Text = ""; // clear the unused control
                    ClearCharacterPreview();
                }
                else // DIALOG_TEXT / DIALOG_TEXT_JCALG1
                {
                    Legacy_TextBox.Text = info.Text;
                    Legacy_IDE.Text = ""; // clear the unused control
                    UpdateCharacterPreview(info.Character);
                }
            }
            else
            {
                // Zone header or any non-dialog node — clear everything
                Legacy_IDE.Text = "";
                Legacy_TextBox.Text = "";
                ClearCharacterPreview();
            }
        }

        private void CommitPendingEdit()
        {
            if (_currentNode?.Tag is DialogNodeInfo info && info.Editable)
            {
                string current = Legacy_TextBox.Text;
                if (info.Text != current)
                {
                    info.Text = current;
                    info.Dirty = true;
                }
            }
        }

        private void Legacy_OpenROM_Click(object sender, EventArgs e)
        {
            //TODO: Hmm, how to work out which game it is?
            using (var openRomDialog = new OpenFileDialog() { Filter = "GBA ROMs|*.gba", Title = "Select a GBA ROM" })
            {
                if (openRomDialog.ShowDialog() != DialogResult.OK)
                    return;
                _rom = File.ReadAllBytes(openRomDialog.FileName);
            }

            PopulateDialogTree();
        }

        private void Legacy_Load(object sender, EventArgs e)
        {
            toolStripButton1.Image = RenderGlyphIcon('\uE768', Color.Green, 32); // play
            toolStripButton2.Image = RenderGlyphIcon('\uE74E', Color.SteelBlue, 32); // save
        }
        private Bitmap RenderGlyphIcon(char glyph, Color color, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Segoe MDL2 Assets", size * 1f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(glyph.ToString(), font, brush, new RectangleF(0, 0, size, size), sf);
            }
            return bmp;
        }

        private void Legacy_SaveROM_Click(object sender, EventArgs e)
        {
            if (_rom == null)
            {
                MessageBox.Show("No ROM loaded.");
                return;
            }

            CommitPendingEdit();

            // Collect every dirty node across the whole tree
            var dirtyInfos = new List<DialogNodeInfo>();
            foreach (TreeNode zoneNode in Legacy_ScriptFunctions.Nodes)
            {
                foreach (TreeNode dialogNode in zoneNode.Nodes)
                {
                    if (dialogNode.Tag is DialogNodeInfo info && info.Editable && info.Dirty)
                        dirtyInfos.Add(info);
                }
            }

            if (dirtyInfos.Count == 0)
            {
                MessageBox.Show("No changes to save.");
                return;
            }

            // Build the new ROM: original bytes + appended edited entries
            byte[] newRom = new byte[_rom.Length];
            Array.Copy(_rom, newRom, _rom.Length);
            var extension = new List<byte>();

            using (var openSaveDialog = new SaveFileDialog() { Filter = "GBA ROMs|*.gba", Title = "Save ROM As" })
            {
                if (openSaveDialog.ShowDialog() != DialogResult.OK)
                    return;

                foreach (var info in dirtyInfos)
                {
                    uint newEntryAddr = RomBase + (uint)(newRom.Length + extension.Count);

                    // Header: Index(u16) + Mode=0x1(u8, force plain text) + Character(u8)
                    extension.Add((byte)(info.Index & 0xFF));
                    extension.Add((byte)((info.Index >> 8) & 0xFF));
                    extension.Add(0x1); // force DIALOG_TEXT, no re-compression
                    extension.Add(info.Character);

                    // Message bytes + null terminator
                    extension.AddRange(Encoding.UTF8.GetBytes(info.Text));
                    extension.Add(0x00);

                    // Patch the table slot (in the ORIGINAL rom region) to point at the new entry
                    uint slotOffset = info.TableSlotAddr - RomBase;
                    byte[] ptrBytes = BitConverter.GetBytes(newEntryAddr);
                    Array.Copy(ptrBytes, 0, newRom, slotOffset, 4);
                }

                byte[] finalRom = new byte[newRom.Length + extension.Count];
                Array.Copy(newRom, finalRom, newRom.Length);
                Array.Copy(extension.ToArray(), 0, finalRom, newRom.Length, extension.Count);

                File.WriteAllBytes(openSaveDialog.FileName, finalRom);

                foreach (var info in dirtyInfos)
                    info.Dirty = false;

                MessageBox.Show($"Saved {dirtyInfos.Count} edited entries to {Path.GetFileName(openSaveDialog.FileName)}.");
            }
        }

        private void Legacy_QuitEditor_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void ToolStripMenuItem_ScriptVisualizer_Click(object sender, EventArgs e)
        {
            new ScriptVisualizer().ShowDialog();
        }

        private void ToolStripMenuItem_StringDecompressor_Click(object sender, EventArgs e)
        {
            new StringDecompressor().ShowDialog();
        }

        private void statViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new StatView(_rom).ShowDialog();
        }
    }

    public static class GBA
    {
        private const uint RomBase = 0x08000000;

        public static uint ReadUInt32(byte[] rom, uint addr)
        {
            int off = ToOffset(addr);
            if (off < 0 || off + 4 > rom.Length) throw new ArgumentOutOfRangeException(nameof(addr));
            return BitConverter.ToUInt32(rom, off);
        }

        public static int ReadInt32(byte[] rom, uint addr)
        {
            int off = ToOffset(addr);
            if (off < 0 || off + 4 > rom.Length) throw new ArgumentOutOfRangeException(nameof(addr));
            return BitConverter.ToInt32(rom, off);
        }

        public static int ToOffset(uint addr)
        {
            return (int)(addr - RomBase);
        }

        // Read a palette stored as 15-bit BGR (GBA format). Default count 256.
        public static Color[] ReadPalette(byte[] rom, uint palettePtr, int count = 256)
        {
            int off = ToOffset(palettePtr);
            int bytesNeeded = count * 2;
            if (off < 0 || off + bytesNeeded > rom.Length) throw new ArgumentOutOfRangeException(nameof(palettePtr));

            var palette = new Color[count];
            for (int i = 0; i < count; i++)
            {
                ushort v = BitConverter.ToUInt16(rom, off + i * 2);
                int r = (v & 0x1F);
                int g = (v >> 5) & 0x1F;
                int b = (v >> 10) & 0x1F;
                // expand 5-bit to 8-bit
                r = (r << 3) | (r >> 2);
                g = (g << 3) | (g >> 2);
                b = (b << 3) | (b >> 2);
                palette[i] = Color.FromArgb(255, r, g, b);
            }
            return palette;
        }

        // Render 8bpp pixel data into a 32bpp Bitmap using the given palette
        public static Bitmap Render8bpp(byte[] data, int width, int height, Color[] palette)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            if (data.Length < width * height) throw new ArgumentException("Insufficient pixel data");

            var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = data[y * width + x];
                    Color c = (idx < palette.Length) ? palette[idx] : Color.Magenta;
                    bmp.SetPixel(x, y, c);
                }
            }
            return bmp;
        }
    }
}