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
            Zenkai.ZenkaiEditor.Configure(Legacy_IDE);
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
        const int DialogHeaderSize = 4; // Index(u16) + Mode(u8) + Character(u8) -- Mode 1/2 (text) only
        // CONFIRMED via IDA 2026-09-16 (Dialog_CreateScriptedElement_Impl, 0x800B19C):
        // BytecodeVM_ExecuteScript(&a1, pc: entryPtr + 3, ctx). Mode 0 (script) entries have
        // NO Character byte at all -- the header is just [u16 Index][u8 Mode], and bytecode
        // starts immediately at entry+3, one byte earlier than DialogHeaderSize. Using
        // DialogHeaderSize (4) for scripts was reading one byte into the middle of the first
        // real instruction, desyncing the rest of the script -- the root cause of nearly every
        // "impossible opcode"/arity-mismatch case seen when disassembling real ROM scripts.
        const int ScriptHeaderSize = 3; // Index(u16) + Mode(u8), script bytecode immediately follows
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

        // Disassembles to editable Zenkai source (Zenkai.g4 call syntax) rather than a raw
        // offset/opcode dump, so the same text shown here is what Legacy_IDE's autocomplete/
        // squiggle checking and the Compile button (ZenkaiAssembler) understand.
        // scriptByteLength is how many ROM bytes the script actually occupies (through its
        // terminating END opcode) - the caller needs this to know how much space is safe to
        // overwrite in place when re-saving an edited version of this exact script.
        private string ReadScriptText(uint addr, out int scriptByteLength)
        {
            uint offset = addr - RomBase;
            int length = Math.Min(MaxCompressedSliceLength, _rom.Length - (int)offset);
            byte[] slice = new byte[length];
            Array.Copy(_rom, offset, slice, 0, length);

            try
            {
                return Zenkai.ZenkaiDisassembler.Disassemble(slice, out scriptByteLength);
            }
            catch (Exception ex)
            {
                scriptByteLength = 0;
                return $"// disassemble error: {ex.Message}";
            }
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

            // Mode 0 (script) nodes are also Editable now (for the script-editor round-trip
            // below), but they don't use the character preview at all -- guard explicitly by
            // mode rather than by Editable alone so switching to the Character/Text tab while
            // a script node is selected can't stamp a bogus Character byte onto it.
            if (_currentNode?.Tag is DialogNodeInfo info && info.Editable && info.Mode != 0x0)
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
            public string Text = "";     // current text (editable for Mode 0/1/2)
            public bool Editable;        // true for Mode 0 (script) and Mode 1/2 (text)
            public bool Dirty;           // true once user has changed the text

            // Mode 0 (script) only: where the raw bytecode starts in the ROM and how many
            // bytes it occupies there (through its terminating END opcode). Recompiled bytes
            // that fit within OriginalScriptLength are written in place (zero-padded);
            // anything larger is appended to the ROM extension like an edited text entry.
            public uint ScriptAddr;
            public int OriginalScriptLength;
        }

        // MapItem struct (16 bytes): flags/condition(4), itemIndex(1), itemCount(1),
        // unk2(2), handler(4), dialogSeq(4). CONFIRMED via IDA 2026-09 against real
        // Zone1/Area1 ROM data (the three rocks). mapItems itself is a pointer array
        // (double indirection), same as mapTriggers/mapScripts.
        const int MapItemPtrArrayStride = 4;
        const int MapItemDialogSeqOffset = 0xC;
        const int MapItemCountOffset = 0x05;
        const int MapItemsPtrOffset = 0x18;

        // One zone/area's worth of dialog entries, built once per ROM load. Kept separate
        // from the TreeView itself so the tree can be re-rendered filtered by active tab
        // (RenderTreeForActiveTab) without re-walking the ROM every time.
        private class ZoneModel
        {
            public string Label = "";
            public List<(string Label, DialogNodeInfo Info)> Children = new();
        }
        private List<ZoneModel> _dialogModel = new();

        // Walks a 0-terminated DCD pointer table of dialog entries (same shape whether
        // it came from a MapTriggerDialog's dialogArray or a MapItem's dialogSeq) and
        // adds one entry per dialog into target.
        //
        // CONFIRMED (2026-09, via real ROM data): dialogSeq is polymorphic -- some
        // handlers (e.g. the rocks, Z1A1_RockInteraction_Handle) treat it as a real
        // pointer-to-pointer-array like this; at least one other handler seen this
        // session (sub_800F9A2) treats the SAME field as raw executable bytecode
        // instead, which would NOT look like a valid table here. Guard against that:
        // if the very first pointer doesn't decode to a plausible entry (mode in the
        // known 0-5 range), skip the whole table rather than add garbage nodes.
        private int WalkDialogArray(List<(string Label, DialogNodeInfo Info)> target, uint dialogArrayPtr, string labelPrefix)
        {
            if (!IsValidPtr(dialogArrayPtr)) return 0;

            uint firstPtr = ReadU32(dialogArrayPtr);
            if (firstPtr == 0 || !IsValidPtr(firstPtr) || ReadU8(firstPtr + 2) > 5)
                return 0; // doesn't look like a real dialog table -- likely inline bytecode instead

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
                uint scriptAddr = 0;
                int scriptLen = 0;

                if (mode == 0x0) // DIALOG_SCRIPT
                {
                    scriptAddr = dialogPtr + ScriptHeaderSize;
                    text = ReadScriptText(scriptAddr, out scriptLen);
                    editable = true;
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
                else if (mode == 0x5) // DIALOG_JUMP
                {
                    // CONFIRMED via IDA 2026-09 (Dialog_CreateJump, 0x800B494): target is
                    // a 4-byte value at entry+4, not the 2 bytes previously assumed
                    // elsewhere in this codebase. Not editable yet -- just annotated.
                    uint jumpTarget = ReadU32(dialogPtr + 4);
                    text = $"[JUMP -> 0x{jumpTarget:X8}]";
                }
                else
                {
                    text = $"[unhandled DialogMode {mode}]";
                }

                var info = new DialogNodeInfo
                {
                    TableSlotAddr = tableCursor,
                    Mode = mode,
                    Index = index,
                    Character = character,
                    Text = text,
                    Editable = editable,
                    Dirty = false,
                    ScriptAddr = scriptAddr,
                    OriginalScriptLength = scriptLen
                };

                target.Add(($"{labelPrefix} - Dialog #{seq}", info));

                tableCursor += DialogSequenceTableEntrySize;
            }

            return seq;
        }

        private void PopulateDialogTree()
        {
            _dialogModel.Clear();
            _extension.Clear();
            _originalRomLength = _rom!.Length;

            for (int i = 0; i < MapCount; i++)
            {
                uint entryAddr = MapEntriesAddr + (uint)(i * MapEntrySize);
                byte zone = ReadU8(entryAddr + 0x00);
                byte area = ReadU8(entryAddr + 0x01);
                byte triggerCount = ReadU8(entryAddr + TriggerCountOffset);
                uint triggersPtr = ReadU32(entryAddr + MapTriggersPtrOffset);
                byte itemCount = ReadU8(entryAddr + MapItemCountOffset);
                uint mapItemsPtr = ReadU32(entryAddr + MapItemsPtrOffset);

                bool hasTriggers = triggerCount != 0 && IsValidPtr(triggersPtr);
                bool hasItems = itemCount != 0 && IsValidPtr(mapItemsPtr);
                if (!hasTriggers && !hasItems) continue;

                ZoneModel zoneModel = null;
                ZoneModel GetZoneModel()
                {
                    if (zoneModel == null)
                    {
                        zoneModel = new ZoneModel { Label = $"Z{zone}A{area}" };
                        _dialogModel.Add(zoneModel);
                    }
                    return zoneModel;
                }

                if (hasTriggers)
                {
                    for (int t = 0; t < triggerCount; t++)
                    {
                        uint entryPtr = triggersPtr + (uint)(t * 8);
                        uint triggerPtr = ReadU32(entryPtr + 4);

                        if (!IsValidPtr(triggerPtr)) continue; // filler/number, not a pointer

                        uint h1 = ReadU32(triggerPtr);
                        uint h2 = ReadU32(triggerPtr + 4);
                        if (h1 != DialogHandlerA || h2 != DialogHandlerB) continue;

                        uint dialogArrayPtr = ReadU32(triggerPtr + 16);
                        WalkDialogArray(GetZoneModel().Children, dialogArrayPtr, $"Z{zone}A{area} Trigger");
                    }
                }

                // Static-object dialogue (rocks, signs, pickups) -- reachable via
                // MapItem.dialogSeq rather than a MapTriggerDialog. See WalkDialogArray's
                // comment for why some MapItems are silently skipped (bytecode, not text).
                if (hasItems)
                {
                    for (int it = 0; it < itemCount; it++)
                    {
                        uint slotAddr = mapItemsPtr + (uint)(it * MapItemPtrArrayStride);
                        uint itemPtr = ReadU32(slotAddr);
                        if (!IsValidPtr(itemPtr)) continue;

                        uint dialogSeqPtr = ReadU32(itemPtr + MapItemDialogSeqOffset);
                        WalkDialogArray(GetZoneModel().Children, dialogSeqPtr, $"Z{zone}A{area} Item[{it}]");
                    }
                }
            }

            RenderTreeForActiveTab();
        }

        // Re-renders Legacy_ScriptFunctions from _dialogModel, filtered to only the entries
        // the active tab can actually edit -- Script Editor shows Mode 0 (script) entries,
        // Character/Text shows Mode 1/2 (text) entries. Other modes (e.g. Mode 5 jump) have
        // no dedicated editor tab, so they're shown on both rather than hidden entirely.
        // Cheap enough (hundreds of entries) to just rebuild rather than toggle visibility --
        // TreeNode has no Visible property to toggle anyway.
        private void RenderTreeForActiveTab()
        {
            bool scriptTab = Legacy_MainTabs.SelectedTab == Legacy_TabScriptEditor;

            Legacy_ScriptFunctions.BeginUpdate();
            Legacy_ScriptFunctions.Nodes.Clear();

            foreach (var zone in _dialogModel)
            {
                var matching = zone.Children.Where(c => MatchesActiveTab(c.Info, scriptTab)).ToList();
                if (matching.Count == 0) continue;

                var zoneNode = new TreeNode(zone.Label);
                foreach (var (label, info) in matching)
                    zoneNode.Nodes.Add(new TreeNode(label) { Tag = info });
                Legacy_ScriptFunctions.Nodes.Add(zoneNode);
            }

            Legacy_ScriptFunctions.EndUpdate();
        }

        private static bool MatchesActiveTab(DialogNodeInfo info, bool scriptTab)
        {
            if (info.Mode == 0x0) return scriptTab;
            if (info.Mode == 0x1 || info.Mode == 0x2) return !scriptTab;
            return true;
        }

        private void Legacy_MainTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            CommitPendingEdit();

            // The previously selected node's DialogNodeInfo may not be visible in the
            // new filtered tree (e.g. it's a script node and we just switched to
            // Character/Text) - clear the editors/selection rather than show stale state.
            _currentNode = null;
            Legacy_IDE.Text = "";
            Legacy_TextBox.Text = "";
            ClearCharacterPreview();

            if (_rom != null)
                RenderTreeForActiveTab();
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
                string current = info.Mode == 0x0 ? Legacy_IDE.Text : Legacy_TextBox.Text;
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
            toolStripButton2.ToolTipText = "Save ROM";
            toolStripButton2.Click += (s, e2) => Legacy_SaveROM_Click(s, e2);
        }

        // Non-blocking status strip message, replacing MessageBox popups for routine
        // save feedback (errors and confirmations alike) so they don't interrupt the flow
        // of editing. isError just tints the text - both cases stay visible until replaced.
        private void ShowStatus(string message, bool isError = false)
        {
            Legacy_StatusLabel.Text = message;
            Legacy_StatusLabel.ForeColor = isError ? Color.Firebrick : SystemColors.ControlText;
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
                ShowStatus("No ROM loaded.", isError: true);
                return;
            }

            CommitPendingEdit();

            // Collect every dirty node across the whole model - NOT just the visible tree,
            // which is now filtered per active tab (RenderTreeForActiveTab) and so can be
            // missing dirty nodes edited on the other tab.
            var dirtyInfos = _dialogModel
                .SelectMany(z => z.Children)
                .Where(c => c.Info.Editable && c.Info.Dirty)
                .Select(c => c.Info)
                .ToList();

            if (dirtyInfos.Count == 0)
            {
                ShowStatus("No changes to save.");
                return;
            }

            // Compile every dirty script up front (before touching any ROM bytes) so a bad
            // edit aborts the whole save instead of leaving the ROM half-patched.
            var compiledScripts = new Dictionary<DialogNodeInfo, byte[]>();
            foreach (var info in dirtyInfos)
            {
                if (info.Mode != 0x0) continue;

                try
                {
                    compiledScripts[info] = Zenkai.ZenkaiAssembler.Assemble(info.Text);
                }
                catch (Zenkai.ZenkaiAssemblerException ex)
                {
                    ShowStatus($"Save aborted - script for {info.TableSlotAddr:X8} failed to compile: {ex.Message}", isError: true);
                    return;
                }
                catch (Exception ex)
                {
                    ShowStatus($"Save aborted - script for {info.TableSlotAddr:X8} failed to compile: {ex.Message}", isError: true);
                    return;
                }
            }

            // Build the new ROM: original bytes (with any script edits that fit in place
            // applied and zero-padded) + appended entries for edited text and any script
            // that outgrew its original allocation.
            byte[] newRom = new byte[_rom.Length];
            Array.Copy(_rom, newRom, _rom.Length);
            var extension = new List<byte>();

            using (var openSaveDialog = new SaveFileDialog() { Filter = "GBA ROMs|*.gba", Title = "Save ROM As" })
            {
                if (openSaveDialog.ShowDialog() != DialogResult.OK)
                    return;

                foreach (var info in dirtyInfos)
                {
                    if (info.Mode == 0x0)
                    {
                        byte[] compiled = compiledScripts[info];

                        if (compiled.Length <= info.OriginalScriptLength)
                        {
                            // Fits in the space the original script occupied - overwrite in
                            // place and zero-pad the rest. Safe: compiled bytes always end
                            // with the END (0x11) opcode, so execution never reaches the
                            // padding, and the table slot pointer doesn't need to change.
                            uint scriptOffset = info.ScriptAddr - RomBase;
                            Array.Copy(compiled, 0, newRom, scriptOffset, compiled.Length);
                            for (int i = compiled.Length; i < info.OriginalScriptLength; i++)
                                newRom[scriptOffset + i] = 0x00;
                        }
                        else
                        {
                            // Grew past (or is a brand-new script with) no room to fit in
                            // place - write a fresh header+script entry at the end of the
                            // ROM and repoint the table slot at it, same as an edited text
                            // entry below. The original in-ROM bytes are left as dead data.
                            uint newEntryAddr = RomBase + (uint)(newRom.Length + extension.Count);

                            // Header is [u16 Index][u8 Mode] ONLY for scripts (ScriptHeaderSize,
                            // 3 bytes) - no Character byte, confirmed against
                            // Dialog_CreateScriptedElement_Impl's `pc: entryPtr + 3`.
                            extension.Add((byte)(info.Index & 0xFF));
                            extension.Add((byte)((info.Index >> 8) & 0xFF));
                            extension.Add(0x0); // DIALOG_SCRIPT
                            extension.AddRange(compiled);

                            uint slotOffset = info.TableSlotAddr - RomBase;
                            byte[] ptrBytes = BitConverter.GetBytes(newEntryAddr);
                            Array.Copy(ptrBytes, 0, newRom, slotOffset, 4);
                        }

                        continue;
                    }

                    uint textEntryAddr = RomBase + (uint)(newRom.Length + extension.Count);

                    // Header: Index(u16) + Mode=0x1(u8, force plain text) + Character(u8)
                    extension.Add((byte)(info.Index & 0xFF));
                    extension.Add((byte)((info.Index >> 8) & 0xFF));
                    extension.Add(0x1); // force DIALOG_TEXT, no re-compression
                    extension.Add(info.Character);

                    // Message bytes + null terminator
                    extension.AddRange(Encoding.UTF8.GetBytes(info.Text));
                    extension.Add(0x00);

                    // Patch the table slot (in the ORIGINAL rom region) to point at the new entry
                    uint textSlotOffset = info.TableSlotAddr - RomBase;
                    byte[] textPtrBytes = BitConverter.GetBytes(textEntryAddr);
                    Array.Copy(textPtrBytes, 0, newRom, textSlotOffset, 4);
                }

                byte[] finalRom = new byte[newRom.Length + extension.Count];
                Array.Copy(newRom, finalRom, newRom.Length);
                Array.Copy(extension.ToArray(), 0, finalRom, newRom.Length, extension.Count);

                File.WriteAllBytes(openSaveDialog.FileName, finalRom);

                foreach (var info in dirtyInfos)
                    info.Dirty = false;

                ShowStatus($"Saved {dirtyInfos.Count} edited entries to {Path.GetFileName(openSaveDialog.FileName)}.");
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

        // Compiles the Script Editor tab's current text purely to validate it (and show
        // the resulting byte count) - it does not write the recompiled bytes back into
        // the ROM. Mode-0 (DIALOG_SCRIPT) entries aren't part of the Save ROM extension
        // path the way Mode 1/2 text entries are; see CommitPendingEdit/Legacy_SaveROM_Click.
        private void Legacy_IDE_BTN_Compile_Click(object sender, EventArgs e)
        {
            try
            {
                byte[] bytes = Zenkai.ZenkaiAssembler.Assemble(Legacy_IDE.Text);
                Legacy_IDE_LBL_CompileStatus.Text = $"Compiled OK - {bytes.Length} bytes.";
                Legacy_IDE_LBL_CompileStatus.ForeColor = Color.DarkGreen;
            }
            catch (Zenkai.ZenkaiAssemblerException ex)
            {
                Legacy_IDE_LBL_CompileStatus.Text = ex.Message;
                Legacy_IDE_LBL_CompileStatus.ForeColor = Color.Firebrick;
            }
            catch (Exception ex)
            {
                Legacy_IDE_LBL_CompileStatus.Text = "Unexpected error: " + ex.Message;
                Legacy_IDE_LBL_CompileStatus.ForeColor = Color.Firebrick;
            }
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