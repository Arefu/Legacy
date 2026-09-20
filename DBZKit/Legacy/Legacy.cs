using System.Text;

namespace Legacy
{
    public partial class Legacy : Form
    {
        byte[]? _rom;
        List<byte> _extension = new(); // bytes appended past original ROM on save
        bool _dirtyPending = false;

        public Legacy() : this(Array.Empty<string>()) { }

        // Startup args, launched from Dragon Radar's "Edit Script..."/"Edit Dialogue..."
        // buttons (see Dragon Radar's editScriptButton_Click/editDialogueButton_Click,
        // which shell out to this exe via Process.Start instead of using Dragon Radar's
        // own simpler embedded editor forms) so the two tools share one real script/
        // dialogue editor instead of maintaining two:
        //   --rom=<path>            load this ROM on startup
        //   --zone=<n> --area=<n>   expand/select this Zone/Area once the tree is built
        //   --focus=0x<addr>        (optional) also select the specific trigger's
        //                           conversation within that Area -- matched against
        //                           DialogNodeInfo.SequenceBaseAddr (the trigger's own
        //                           dataPtr+0x10 field, which is exactly what Dragon
        //                           Radar already resolves as ResolveTriggerScriptPayload's
        //                           ScriptAddress)
        private readonly string[] _startupArgs;

        public Legacy(string[] args)
        {
            InitializeComponent();
            BuildTextTabLayout();
            _startupArgs = args;
        }

        const uint RomBase = 0x08000000;
        // A trigger's first vtable word says what kind it is. Surveyed over every trigger in the ROM: 0x0800D7E1 triggers
        // (any second word) carry a dialog sequence[] at dataPtr+0x10, and 0x0800C2C3 triggers carry a plain script there.
        // The others (0x0800C233, 0x08010AC7, ...) have no ROM pointer at +0x10, so there is nothing to show for them.
        const uint DialogHandlerA = 0x0800D7E1; // MapTrigger_OnEnter+1
        const uint ScriptTriggerHandler = 0x0800C2C3;

        const int MapEntrySize = 0x38;
        const int TriggerCountOffset = 0x03;
        const int MapTriggersPtrOffset = 0x10;

        // Object script support, added 2026-09-19 -- Legacy is now the shared editor for
        // an object's OnPickup payload (the Math Book/Golden Capsule/etc "set a flag and/or
        // show a message on pickup" step) too, not just trigger payloads. Offsets confirmed
        // via DrGero.Rendering.EntityReader.ReadObjectArray/FindPickupTemplates -- MapEntry's
        // own field layout was already cross-checked against this file's OTHER hand-rolled
        // offsets above (TriggerCountOffset/MapTriggersPtrOffset/MapItemCountOffset/
        // MapItemsPtrOffset all landed exactly where DrGero.Types.MapEntry's field order
        // says they should), so these two follow the same confirmed layout: ObjectCount is
        // the very next byte after ItemCount, MapObjects the very next pointer after MapItems.
        const int MapObjectCountOffset = 0x06;
        const int MapObjectsPtrOffset = 0x1C;
        // CORRECTED 2026-09-19 (IDA): +0x10 onPickup is a NATIVE function pointer, not a script.
        // The dialogue it runs (where SetStoryFlag lives) is +0x14 collectionMsg.
        const int ObjectCollectionMsgOffset = 0x14;

        const uint MapEntriesAddr = 0x08409368;
        const int MapCount = 0x147;

        // Dialogue reached through mapScripts[] (added 2026-09-20; Legacy used to index only triggers, items and
        // objects, so it missed 242 town-NPC conversations and 27 enemy dialogues). Confirmed via IDA + ROM data:
        //  - handler 0x0800B711 (MapScript_CreateNpcSprite): record+0xC is the NPC's dialog sequence[], opened by
        //    MapEntity_HandleCollisionInteract when you talk to it;
        //  - handler 0x0800E77F (MapScript_CreateEnemy): record+0x14 is an action object {func, payload}; with func
        //    0x080106B3 (EnemyAction_RunDialog) the payload is a dialog sequence[] (e.g. Cell's defeat dialogue).
        const int ScriptCountOffset = 0x04;
        const int MapScriptsPtrOffset = 0x14;
        const uint NpcSpriteHandler = 0x0800B711;
        const uint EnemyHandler = 0x0800E77F;
        const uint EnemyRunDialogFunc = 0x080106B3;

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

            Legacy_CharacterPreview.Image = _rom == null ? null : DialogPreview.PortraitFor(_rom, characterId, out _);
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

                Legacy_CharacterPreview.Image = _rom == null ? null : DialogPreview.PortraitFor(_rom, newId, out _);
                Legacy_CharacterLabel.Text = $"Character ID: {newId}";
                UpdateDialogPreview();
            }
        }
        // ---- text tab: same layout as the micro editor (speaker row, box-position bar, big message box, mock screen) ----

        private readonly DialogPreviewControl _dialogPreview = new();
        private readonly Label _speakerInfo = new() { Dock = DockStyle.Top, Height = 38, Padding = new Padding(4, 2, 4, 0), ForeColor = Color.DimGray };
        private readonly Label _flowInfo = new() { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 2, 8, 2), AutoSize = false };
        private readonly Dictionary<byte, (Bitmap? Image, string What)> _portraitCache = [];
        private byte[]? _portraitCacheRom;

        private void BuildTextTabLayout()
        {
            var tab = Legacy_TabCharacterText;
            tab.SuspendLayout();
            tab.Controls.Clear();

            // Row 1: speaker id (the old absolute-positioned 128x128 portrait is replaced by the mock screen below).
            Legacy_CharacterLabel.AutoSize = true;
            Legacy_CharacterLabel.Location = new Point(6, 9);
            Legacy_CharacterUpDown.Location = new Point(120, 5);
            Legacy_CharacterUpDown.Size = new Size(70, 23);
            var top = new Panel { Dock = DockStyle.Top, Height = 32 };
            top.Controls.Add(Legacy_CharacterLabel);
            top.Controls.Add(Legacy_CharacterUpDown);

            // Row 2: box position + centring.
            Legacy_TextPositionCombo.Width = 170;
            var fmt = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(4, 4, 0, 0) };
            fmt.Controls.Add(new Label { Text = "Box position:", AutoSize = true, Margin = new Padding(0, 5, 4, 0) });
            fmt.Controls.Add(Legacy_TextPositionCombo);
            Legacy_ChkCenterText.Margin = new Padding(12, 4, 0, 0);
            fmt.Controls.Add(Legacy_ChkCenterText);

            // The message itself fills the middle.
            Legacy_TextBox.Dock = DockStyle.Fill;
            Legacy_TextBox.Location = new Point(0, 0);
            Legacy_TextBox.Font = new Font("Consolas", 10f);
            Legacy_TextBox.TextChanged += (_, _) => UpdateDialogPreview();

            // Bottom: what it will look like.
            var holder = new Panel { Dock = DockStyle.Bottom, Height = 380 };
            var inner = new Panel { Dock = DockStyle.Fill };
            _dialogPreview.Location = new Point(8, 4);
            inner.Controls.Add(_dialogPreview);
            inner.Resize += (_, _) => _dialogPreview.Left = Math.Max(4, (inner.Width - _dialogPreview.Width) / 2);
            holder.Controls.Add(inner);
            holder.Controls.Add(_speakerInfo);

            tab.Controls.Add(Legacy_TextBox);
            tab.Controls.Add(holder);
            tab.Controls.Add(fmt);
            tab.Controls.Add(top);
            Legacy_CharacterPreview.Visible = false; // superseded by the mock screen

            // "End dialog" is now spelled out, with a flow line under it that says what happens next.
            Legacy_ChkEndDialog.Text = "End the conversation after this entry";
            var panel = Legacy_AppContainer.Panel2;
            panel.Controls.Remove(Legacy_ChkEndDialog);
            panel.Controls.Add(_flowInfo);
            panel.Controls.Add(Legacy_ChkEndDialog);

            tab.ResumeLayout();
        }

        // Redraws the mock screen for the selected text entry. Portraits are decoded once per speaker id per ROM.
        private void UpdateDialogPreview()
        {
            if (_rom == null || _currentNode?.Tag is not DialogNodeInfo info || (info.Mode != 0x1 && info.Mode != 0x2))
            {
                _dialogPreview.SetContent(0, BoxPosition.Auto, false, "", null);
                _speakerInfo.Text = "Select a text entry to see how it will look in-game.";
                return;
            }

            if (!ReferenceEquals(_portraitCacheRom, _rom)) { _portraitCache.Clear(); _portraitCacheRom = _rom; }

            byte speaker = (byte)Legacy_CharacterUpDown.Value;
            if (!_portraitCache.TryGetValue(speaker, out var cached))
            {
                var img = DialogPreview.PortraitFor(_rom, speaker, out string what);
                cached = (img, what);
                _portraitCache[speaker] = cached;
            }

            _speakerInfo.Text = $"Speaker {speaker}: {cached.What}  |  box {DialogPreview.BoxWidth(speaker)} px wide, style {DialogPreview.BoxStyle(speaker)}." +
                                (speaker != 0 && cached.Image == null ? "  (no portrait bitmap for this id)" : "");
            _dialogPreview.SetContent(speaker, (BoxPosition)Math.Max(0, Legacy_TextPositionCombo.SelectedIndex), Legacy_ChkCenterText.Checked, Legacy_TextBox.Text, cached.Image);
        }

        // Says where the conversation goes after the selected entry, and shouts when an "end" flag would cut off later steps
        // (that is exactly how a change-character script followed by a message silently never showed the message).
        private void UpdateFlowInfo()
        {
            if (_currentNode?.Tag is not DialogNodeInfo info || !info.Editable || info.SequenceBaseAddr == 0 || info.RawScript)
            {
                _flowInfo.Text = "";
                return;
            }

            int slot = (int)((info.TableSlotAddr - info.SequenceBaseAddr) / DialogSequenceTableEntrySize);
            int terminator = info.TerminatorSlotIndex;
            int next = info.EndsDialogHere ? terminator : info.Index;
            int stepsAfter = terminator - slot - 1;

            if (next == terminator)
            {
                if (stepsAfter <= 0)
                {
                    _flowInfo.ForeColor = Color.DimGray;
                    _flowInfo.Text = $"Step {slot + 1}: this is the last step -- the conversation ends after it.";
                }
                else
                {
                    _flowInfo.ForeColor = Color.Firebrick;
                    _flowInfo.Text = $"Step {slot + 1}: the conversation ENDS after this step, so the {stepsAfter} step(s) after it will never run. " +
                                     $"Untick the box above to continue to step {slot + 2}.";
                }
            }
            else
            {
                _flowInfo.ForeColor = Color.DarkGreen;
                _flowInfo.Text = next == slot + 1
                    ? $"Step {slot + 1}: continues to step {next + 1}."
                    : $"Step {slot + 1}: jumps to step {next + 1} (not the next one in the list).";
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

            // True for a trigger's plain-script payload: the script is referenced directly (TableSlotAddr is the
            // payload field, not a dialog table slot) and has no Index/Mode header or end-dialog step.
            public bool RawScript;

            // True once a save has appended this script at the end of the ROM (and repointed to it); from then
            // on it is this node's own copy, so later edits that fit may be written over it in place.
            public bool Relocated;

            // CONFIRMED via IDA 2026-09-16 (Dialog_ProcessNext, 0x800B520): dialog sequencing
            // is index-chained, not positional -- after processing sequence[currentIndex], the
            // engine sets currentIndex = thisEntry.Index (the same u16 header field above) for
            // the NEXT call. A slot whose stored value's top 16 bits are zero (not a real
            // pointer) ends the sequence -- that's the table's own trailing terminator slot.
            // So "end the dialog after this entry" means: point Index at that terminator slot
            // instead of wherever it naturally continues. EntryAddr is the entry struct's own
            // address (Index lives at EntryAddr+0, 2 bytes) so this can be patched in place
            // independently of the text/script body edit above.
            public uint EntryAddr;
            public int TerminatorSlotIndex; // shared by every entry from the same table
            public bool EndsDialogHere;     // current desired state (starts = whether Index already equals the terminator)
            public bool EndsDialogDirty;    // true once the user has toggled EndsDialogHere away from its loaded state

            // The dialog table's own base address (the same "dialogArrayPtr" every entry
            // in one WalkDialogArray call was walked from) -- shared by every entry from
            // that same table, same as TerminatorSlotIndex above. Used by FocusOnAddress
            // to jump straight to a specific trigger's conversation when launched from
            // Dragon Radar's "Edit Dialogue..."/"Edit Script..." buttons, which know this
            // exact address (it's the trigger's own dataPtr+0x10 field).
            public uint SequenceBaseAddr;

            // Mode 1/2 (text) only: leading format characters confirmed via IDA
            // (Dialog_CreateTextBox, 0x800B1E2) and, for Center, via in-game testing.
            // These are stripped off Text on load and re-prepended on save rather than
            // being left inline for the user to hand-edit/typo.
            public TextPosition Position = TextPosition.Unspecified; // '!'/'@'/'#' -> box Y position
            public bool CenterText;                                  // '^' -> centers the message text
        }

        // Leading message character controlling the text box's vertical position
        // (Dialog_CreateTextBox, 0x800B1E2): '!'=40 (top), '@'=80 (middle), '#'=120 (bottom).
        internal enum TextPosition { Unspecified, Top, Middle, Bottom }

        // Splits a message's leading format characters (position, then '^') off into their
        // own fields so the text box shows only the actual dialog text. Order matters: the
        // engine consumes any position prefix first (Dialog_CreateTextBox), then checks for
        // '^' as the next character (the per-frame box-open state machine, sub_800BA84).
        private static (string Text, TextPosition Position, bool Center) ExtractTextFormat(string raw)
        {
            TextPosition position = TextPosition.Unspecified;
            if (raw.Length > 0)
            {
                switch (raw[0])
                {
                    case '!': position = TextPosition.Top; raw = raw[1..]; break;
                    case '@': position = TextPosition.Middle; raw = raw[1..]; break;
                    case '#': position = TextPosition.Bottom; raw = raw[1..]; break;
                }
            }

            bool center = raw.Length > 0 && raw[0] == '^';
            if (center) raw = raw[1..];

            return (raw, position, center);
        }

        private static string ApplyTextFormat(string text, TextPosition position, bool center)
        {
            string prefix = position switch
            {
                TextPosition.Top => "!",
                TextPosition.Middle => "@",
                TextPosition.Bottom => "#",
                _ => "",
            };
            if (center) prefix += "^";
            return prefix + text;
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
        // from the TreeView itself so the tree can be re-rendered
        // (RenderTree) without re-walking the ROM every time.
        //
        // Two-level Zone -> Area hierarchy (matches Dragon Radar's own MapTreeBuilder.Build
        // labeling, "Zone {n}" / "Area {n}") -- previously this was a flat list of one
        // "Z{zone}A{area}" node per map entry, which meant a zone with several areas (the
        // normal case) showed as several unrelated top-level nodes instead of one zone you
        // could expand. REFACTORED 2026-09-19 per explicit request.
        private class AreaModel
        {
            public byte Zone;
            public byte Area;
            public string Label = "";
            public List<(string Label, DialogNodeInfo Info)> Children = new();
        }
        private class ZoneGroup
        {
            public byte Zone;
            public string Label = "";
            public List<AreaModel> Areas = new();
        }
        private List<ZoneGroup> _dialogModel = new();

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
            var createdInfos = new List<DialogNodeInfo>();

            // Where the real entries stop. Each entry's Index is the slot the conversation moves to next, and a
            // non-pointer slot is an end marker, so the table runs at least up to the highest Index seen. Past that,
            // the first non-pointer slot is the end: anything after it is other data (Zone 4 Area 41's sprite 82
            // table used to run on into sprite 83's table and list its lines a second time). The loop still runs
            // on to the literal 0 slot so TerminatorSlotIndex below is unchanged.
            int extent = 0;
            bool pastEnd = false;
            while (true)
            {
                uint dialogPtr = ReadU32(tableCursor);
                if (dialogPtr == 0) break;

                seq++;

                if (!IsValidPtr(dialogPtr))
                {
                    if (seq - 1 > extent) pastEnd = true;
                    tableCursor += DialogSequenceTableEntrySize;
                    continue;
                }

                byte mode = ReadU8(dialogPtr + 2); // DialogMode offset within entry
                ushort index = ReadU16(dialogPtr + 0);
                byte character = ReadU8(dialogPtr + 3);

                if (pastEnd)
                {
                    tableCursor += DialogSequenceTableEntrySize;
                    continue;
                }
                extent = Math.Max(extent, (int)index);

                // The pointer table has no reliable terminator: after the real entries it can run on
                // into unrelated data (e.g. the entries' own bytes), and a stray word that happens to
                // look like a ROM pointer used to become a fake node such as "[unhandled DialogMode 104]"
                // (Zone 4 Area 41, sprite 83: 0x08000313 -> the ROM header). A real entry's Index equals
                // its slot number + 1 (= seq) in every table checked, and its mode is 0-5.
                if (mode > 5 && index != seq)
                {
                    tableCursor += DialogSequenceTableEntrySize;
                    continue;
                }

                string text = "";
                bool editable = false;
                uint scriptAddr = 0;
                int scriptLen = 0;

                TextPosition position = TextPosition.Unspecified;
                bool centerText = false;
                bool interpolated = false;

                if (mode == 0x0) // DIALOG_SCRIPT
                {
                    scriptAddr = dialogPtr + ScriptHeaderSize;
                    text = ReadScriptText(scriptAddr, out scriptLen);
                    editable = true;
                }
                else if (mode == 0x1) // DIALOG_TEXT
                {
                    (text, position, centerText) = ExtractTextFormat(ReadNullTerminatedString(dialogPtr + DialogHeaderSize));
                    editable = true;
                }
                else if (mode == 0x2) // DIALOG_TEXT_JCALG1
                {
                    (text, position, centerText) = ExtractTextFormat(ReadJcalg1String(dialogPtr + DialogHeaderSize));
                    editable = true;
                }
                else if (mode == 0x3 || mode == 0x4) // DIALOG_TEXT_INTERPOLATE / _COMPRESSED (read-only for now)
                {
                    // CONFIRMED via IDA 2026-09 (Dialog_CreateInterpolatedTextBox_Impl / ...FromCompressed_Impl):
                    // a format string (%s etc.) plus a small script whose pushed values fill the placeholders.
                    //   mode 3: [+4] ptr to the format string, script inline at +8 (ends with END 0x11)
                    //   mode 4: [+4] ptr to the script, JCALG1-compressed format string inline at +8
                    // Only the message is shown (placeholders left as %s); the node label says it is interpolated.
                    uint fieldPtr = ReadU32(dialogPtr + 4);
                    if (mode == 0x3)
                        text = IsValidPtr(fieldPtr) ? ReadNullTerminatedString(fieldPtr) : "[bad format string pointer]";
                    else
                        text = ReadJcalg1String(dialogPtr + 8);
                    interpolated = true;
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
                    OriginalScriptLength = scriptLen,
                    EntryAddr = dialogPtr,
                    Position = position,
                    CenterText = centerText,
                    SequenceBaseAddr = dialogArrayPtr
                };

                target.Add(($"{labelPrefix} - Dialog #{seq}{(interpolated ? " (interpolated)" : "")}", info));
                createdInfos.Add(info);

                tableCursor += DialogSequenceTableEntrySize;
            }

            // tableCursor now sits on the table's own terminator slot (dialogPtr read there
            // was 0, or a >=0x1E small value would also have IsValidPtr()==false and been
            // skipped as filler above rather than ending the loop -- only a literal 0 breaks
            // it here, matching WalkDialogArray's own termination check). Its slot index is
            // what every entry's Index field must equal to end the dialog right after it.
            int terminatorSlotIndex = (int)((tableCursor - dialogArrayPtr) / DialogSequenceTableEntrySize);
            foreach (var info in createdInfos)
            {
                info.TerminatorSlotIndex = terminatorSlotIndex;
                info.EndsDialogHere = info.Index == terminatorSlotIndex;
            }

            return seq;
        }

        // A trigger whose payload (dataPtr+0x10) is one plain script rather than a dialog table. It is edited like a
        // dialog script entry, except there is no table slot or entry header: the pointer to patch is the payload
        // field itself and a grown script is written back without a header (see RawScript in Save).
        private void AddRawScriptNode(List<(string Label, DialogNodeInfo Info)> target, uint payloadFieldAddr, uint scriptAddr, string label)
        {
            if (!IsValidPtr(scriptAddr) || scriptAddr + 2 > RomEnd) return;

            string text = ReadScriptText(scriptAddr, out int scriptLen);
            if (scriptLen <= 0) return; // did not disassemble to a script -- not something to offer for editing

            target.Add((label, new DialogNodeInfo
            {
                TableSlotAddr = payloadFieldAddr,
                Mode = 0x0,
                Text = text,
                Editable = true,
                ScriptAddr = scriptAddr,
                OriginalScriptLength = scriptLen,
                EntryAddr = scriptAddr,
                SequenceBaseAddr = scriptAddr, // what Dragon Radar's Edit Script passes for this trigger
                RawScript = true
            }));
        }

        private void PopulateDialogTree()
        {
            _dialogModel.Clear();
            _extension.Clear();
            _originalRomLength = _rom!.Length;

            // The ROM's own code says where the map table is and how many maps there are, so ROMs whose table was
            // relocated/extended (DBZKit > Engine tools) are read correctly.
            uint mapTableAddr = MapEntriesAddr;
            int mapCount = MapCount;
            try
            {
                var romView = DrGero.IO.ROM.FromBytes(_rom!);
                if (DrGero.Engine.RosterTables.IsSupportedRom(romView))
                {
                    mapTableAddr = RomBase | (uint)DrGero.Engine.MapTable.Address(romView);
                    mapCount = DrGero.Engine.MapTable.Count(romView);
                }
            }
            catch (InvalidOperationException) { /* unexpected layout -- keep the built-in values */ }

            for (int i = 0; i < mapCount; i++)
            {
                uint entryAddr = mapTableAddr + (uint)(i * MapEntrySize);
                byte zone = ReadU8(entryAddr + 0x00);
                byte area = ReadU8(entryAddr + 0x01);
                byte triggerCount = ReadU8(entryAddr + TriggerCountOffset);
                uint triggersPtr = ReadU32(entryAddr + MapTriggersPtrOffset);
                byte itemCount = ReadU8(entryAddr + MapItemCountOffset);
                uint mapItemsPtr = ReadU32(entryAddr + MapItemsPtrOffset);

                byte objectCount = ReadU8(entryAddr + MapObjectCountOffset);
                uint mapObjectsPtr = ReadU32(entryAddr + MapObjectsPtrOffset);

                bool hasTriggers = triggerCount != 0 && IsValidPtr(triggersPtr);
                bool hasItems = itemCount != 0 && IsValidPtr(mapItemsPtr);
                bool hasObjects = objectCount != 0 && IsValidPtr(mapObjectsPtr);

                byte scriptCount = ReadU8(entryAddr + ScriptCountOffset);
                uint mapScriptsPtr = ReadU32(entryAddr + MapScriptsPtrOffset);
                bool hasScripts = scriptCount != 0 && IsValidPtr(mapScriptsPtr);
                if (!hasTriggers && !hasItems && !hasObjects && !hasScripts) continue;

                AreaModel areaModel = null;
                AreaModel GetZoneModel()
                {
                    if (areaModel == null)
                    {
                        var zoneGroup = _dialogModel.FirstOrDefault(z => z.Zone == zone);
                        if (zoneGroup == null)
                        {
                            zoneGroup = new ZoneGroup { Zone = zone, Label = $"Zone {zone}" };
                            _dialogModel.Add(zoneGroup);
                        }
                        areaModel = new AreaModel { Zone = zone, Area = area, Label = $"Area {area}" };
                        zoneGroup.Areas.Add(areaModel);
                    }
                    return areaModel;
                }

                if (hasTriggers)
                {
                    for (int t = 0; t < triggerCount; t++)
                    {
                        uint entryPtr = triggersPtr + (uint)(t * 8);
                        uint triggerPtr = ReadU32(entryPtr + 4);

                        if (!IsValidPtr(triggerPtr)) continue; // filler/number, not a pointer

                        uint h1 = ReadU32(triggerPtr);
                        uint payloadPtr = ReadU32(triggerPtr + 16);
                        if (h1 == DialogHandlerA)
                            WalkDialogArray(GetZoneModel().Children, payloadPtr, $"Z{zone}A{area} Trigger[{t}]");
                        else if (h1 == ScriptTriggerHandler)
                            AddRawScriptNode(GetZoneModel().Children, triggerPtr + 16, payloadPtr, $"Z{zone}A{area} Trigger[{t}] - Script");
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

                // Map_Object pickups (Mathbook, Golden Capsules, custom test items...): the
                // native onPickup handler runs the object's collectionMsg (+0x14) as a dialog
                // sequence, and that's where a pickup's script/message lives (confirmed via IDA).
                if (hasObjects)
                {
                    for (int o = 0; o < objectCount; o++)
                    {
                        uint objectPtr = ReadU32(mapObjectsPtr + (uint)(o * 4));
                        if (!IsValidPtr(objectPtr) || objectPtr + ObjectCollectionMsgOffset + 4 > RomEnd) continue;

                        uint collectionMsgPtr = ReadU32(objectPtr + ObjectCollectionMsgOffset);
                        WalkDialogArray(GetZoneModel().Children, collectionMsgPtr, $"Z{zone}A{area} Object[{o}] (item {ReadU32(objectPtr + 4)})");
                    }
                }

                // NPC conversations and enemy dialogues (see the constants above). WalkDialogArray already skips a
                // pointer that doesn't decode to a real dialog table, so records without a conversation add nothing.
                if (hasScripts)
                {
                    for (int k = 0; k < scriptCount; k++)
                    {
                        uint recordPtr = ReadU32(mapScriptsPtr + (uint)(k * 4));
                        if (!IsValidPtr(recordPtr) || recordPtr + 0x1C > RomEnd) continue;

                        uint handler = ReadU32(recordPtr);
                        if (handler == NpcSpriteHandler)
                        {
                            WalkDialogArray(GetZoneModel().Children, ReadU32(recordPtr + 0x0C),
                                $"Z{zone}A{area} NPC[{k}] (sprite {ReadU32(recordPtr + 0x10)})");
                        }
                        else if (handler == EnemyHandler)
                        {
                            uint actionObj = ReadU32(recordPtr + 0x14);
                            if (!IsValidPtr(actionObj) || actionObj + 8 > RomEnd || ReadU32(actionObj) != EnemyRunDialogFunc) continue;
                            WalkDialogArray(GetZoneModel().Children, ReadU32(actionObj + 4),
                                $"Z{zone}A{area} Enemy[{k}] (stat {ReadU32(recordPtr + 8)}, sprite {ReadU32(recordPtr + 0x10)})");
                        }
                    }
                }
            }

            // Matches Dragon Radar's own MapTreeBuilder.Build ordering (OrderBy Zone, then
            // Area) so navigating the same game in either tool feels the same.
            _dialogModel = _dialogModel.OrderBy(z => z.Zone).ToList();
            foreach (var zoneGroup in _dialogModel)
                zoneGroup.Areas = zoneGroup.Areas.OrderBy(a => a.Area).ToList();

            RenderTree();
        }

        // Re-renders Legacy_ScriptFunctions from _dialogModel. Every entry is shown (scripts, text,
        // jumps and anything else); selecting one flips to the tab that edits it (see
        // Legacy_ScriptFunctions_AfterSelect), so the tree is never filtered by tab.
        private void RenderTree()
        {
            Legacy_ScriptFunctions.BeginUpdate();
            Legacy_ScriptFunctions.Nodes.Clear();

            foreach (var zoneGroup in _dialogModel)
            {
                var zoneNode = new TreeNode(zoneGroup.Label);
                foreach (var area in zoneGroup.Areas)
                {
                    var matching = area.Children.ToList();
                    if (matching.Count == 0) continue;

                    var areaNode = new TreeNode(area.Label);
                    foreach (var (label, info) in matching)
                        areaNode.Nodes.Add(new TreeNode(label) { Tag = info });
                    zoneNode.Nodes.Add(areaNode);
                }
                if (zoneNode.Nodes.Count == 0) continue;
                Legacy_ScriptFunctions.Nodes.Add(zoneNode);
            }

            Legacy_ScriptFunctions.EndUpdate();

            if (_pendingFocus != null)
            {
                var focus = _pendingFocus.Value;
                _pendingFocus = null;
                FocusZoneArea(focus.Zone, focus.Area, focus.Addr);
            }
        }

        // Requested via command-line args (see Program.cs/ParseStartupArgs) or a future
        // launch-from-Dragon-Radar call -- applied once, right after the tree this needs
        // to search has actually been rebuilt (RenderTree runs synchronously
        // at the end of PopulateDialogTree, so by the time that returns the ROM has
        // already been read and the tree populated).
        private (byte Zone, byte Area, uint? Addr)? _pendingFocus;

        /// <summary>
        /// Expands and selects the tree node for the given Zone/Area -- and, if addr is
        /// given and matches an entry's SequenceBaseAddr (the same dataPtr+0x10 value
        /// Dragon Radar already resolves for a trigger's payload), selects that specific
        /// conversation/script entry instead of just the Area node.
        ///
        /// NOTE: only dialog-table entries (WalkDialogArray) become tree nodes here -- a
        /// trigger whose payload is a plain script (not a dialog table) has no node to
        /// select, so addr won't match anything for those and this falls back to
        /// selecting the Area node, which is still a genuine improvement over nothing.
        /// </summary>
        private void FocusZoneArea(byte zone, byte area, uint? addr = null)
        {
            foreach (TreeNode zoneNode in Legacy_ScriptFunctions.Nodes)
            {
                if (zoneNode.Text != $"Zone {zone}") continue;
                foreach (TreeNode areaNode in zoneNode.Nodes)
                {
                    if (areaNode.Text != $"Area {area}") continue;

                    zoneNode.Expand();
                    areaNode.Expand();

                    TreeNode? target = areaNode;
                    if (addr != null)
                    {
                        foreach (TreeNode child in areaNode.Nodes)
                        {
                            if (child.Tag is DialogNodeInfo info && info.SequenceBaseAddr == addr.Value)
                            {
                                target = child;
                                break;
                            }
                        }
                    }

                    Legacy_ScriptFunctions.SelectedNode = target;
                    target.EnsureVisible();
                    return;
                }
            }
        }

        // Opcode usage search result -> select that script's node in the tree (selecting it opens the Script Editor tab).
        private void NavigateToScript(Zenkai.OpcodeUsage.Hit hit)
        {
            Legacy_MainTabs.SelectedIndex = 0;
            TreeNode? Find(TreeNodeCollection nodes)
            {
                foreach (TreeNode n in nodes)
                {
                    if (n.Tag is DialogNodeInfo info && info.ScriptAddr == hit.ScriptAddr) return n;
                    var inner = Find(n.Nodes);
                    if (inner != null) return inner;
                }
                return null;
            }
            var node = Find(Legacy_ScriptFunctions.Nodes);
            if (node == null) { MessageBox.Show($"That script (0x{hit.ScriptAddr:X8}) is not in the tree.", "Opcode usage"); return; }
            Legacy_ScriptFunctions.SelectedNode = node;
            node.EnsureVisible();
            Legacy_ScriptFunctions.Focus();
        }

        // Set while the tree selection flips the tab itself, so that isn't treated as the user switching tabs.
        private bool _switchingTab;

        private void Legacy_MainTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_switchingTab) return;
            CommitPendingEdit();
        }

        // Script entries open on the Script Editor tab, text entries on Character / Text. Other modes
        // have no editor of their own and leave the current tab alone.
        private void ShowTabFor(DialogNodeInfo info)
        {
            int wanted = info.Mode == 0x0 ? 0 : (info.Mode >= 0x1 && info.Mode <= 0x4) ? 1 : -1;
            if (wanted < 0 || Legacy_MainTabs.SelectedIndex == wanted) return;
            _switchingTab = true;
            try { Legacy_MainTabs.SelectedIndex = wanted; }
            finally { _switchingTab = false; }
        }

        private TreeNode? _currentNode;

        private void Legacy_ScriptFunctions_AfterSelect(object sender, TreeViewEventArgs e)
        {
            bool treeHadFocus = Legacy_ScriptFunctions.Focused;
            CommitPendingEdit();
            _currentNode = e.Node;

            if (e.Node.Tag is DialogNodeInfo info)
            {
                ShowTabFor(info);
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

                _suppressEndDialogChanged = true;
                Legacy_ChkEndDialog.Enabled = info.Editable && !info.RawScript;
                Legacy_ChkEndDialog.Checked = info.EndsDialogHere;
                _suppressEndDialogChanged = false;

                bool isText = info.Mode == 0x1 || info.Mode == 0x2;
                _suppressTextFormatChanged = true;
                Legacy_TextPositionCombo.Enabled = isText;
                Legacy_TextPositionCombo.SelectedIndex = isText ? (int)info.Position : -1;
                Legacy_ChkCenterText.Enabled = isText;
                Legacy_ChkCenterText.Checked = isText && info.CenterText;
                _suppressTextFormatChanged = false;
                UpdateDialogPreview();
                UpdateFlowInfo();
            }
            else
            {
                // Zone header or any non-dialog node — clear everything
                Legacy_IDE.Text = "";
                Legacy_TextBox.Text = "";
                ClearCharacterPreview();

                _suppressEndDialogChanged = true;
                Legacy_ChkEndDialog.Enabled = false;
                Legacy_ChkEndDialog.Checked = false;
                _suppressEndDialogChanged = false;
                _suppressTextFormatChanged = true;
                Legacy_TextPositionCombo.Enabled = false;
                Legacy_TextPositionCombo.SelectedIndex = -1;
                Legacy_ChkCenterText.Enabled = false;
                Legacy_ChkCenterText.Checked = false;
                _suppressTextFormatChanged = false;
                UpdateDialogPreview();
                UpdateFlowInfo();
            }

            // Selecting a node flips the tab and loads the editors, and either can pull focus off the tree.
            // Put it back afterwards (BeginInvoke: after the tab change settles) so Up/Down keeps working.
            if (treeHadFocus)
                BeginInvoke(new Action(() => { if (!Legacy_ScriptFunctions.Focused) Legacy_ScriptFunctions.Focus(); }));
        }

        private bool _suppressEndDialogChanged = false;
        private bool _suppressTextFormatChanged = false;

        private void Legacy_ChkEndDialog_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressEndDialogChanged) return;
            if (_currentNode?.Tag is not DialogNodeInfo info || !info.Editable) return;

            bool desired = Legacy_ChkEndDialog.Checked;
            bool loadedState = info.Index == info.TerminatorSlotIndex;
            info.EndsDialogHere = desired;
            info.EndsDialogDirty = desired != loadedState;
            UpdateFlowInfo();
        }

        private void Legacy_TextPositionCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressTextFormatChanged) return;
            if (_currentNode?.Tag is not DialogNodeInfo info || !info.Editable) return;
            if (Legacy_TextPositionCombo.SelectedIndex < 0) return;

            info.Position = (TextPosition)Legacy_TextPositionCombo.SelectedIndex;
            info.Dirty = true;
            UpdateDialogPreview();
        }

        private void Legacy_ChkCenterText_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressTextFormatChanged) return;
            if (_currentNode?.Tag is not DialogNodeInfo info || !info.Editable) return;

            info.CenterText = Legacy_ChkCenterText.Checked;
            info.Dirty = true;
            UpdateDialogPreview();
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
            // The game is identified by the 4-character game code in the GBA header (offset 0xAC): ALFE = Legacy of Goku II (US), ALGP = Legacy of Goku I,
            // BDBE = Buu's Fury. It is shown in the title bar; the decoders themselves are still the Legacy of Goku II ones.
            using (var openRomDialog = new OpenFileDialog() { Filter = "GBA ROMs|*.gba", Title = "Select a GBA ROM" })
            {
                if (openRomDialog.ShowDialog() != DialogResult.OK)
                    return;
                _rom = File.ReadAllBytes(openRomDialog.FileName);
            }
            string gameCode = _rom.Length > 0xB0 ? System.Text.Encoding.ASCII.GetString(_rom, 0xAC, 4) : "?";
            Text = $"Legacy - {gameCode}" + (gameCode == "ALFE" ? " (Legacy of Goku II, US)" : " (not the Legacy of Goku II US ROM the decoders were built for)");

            _saveTargetPath = null; // freshly opened ROM - next Save prompts for a destination again
            PopulateDialogTree();
        }

        private void Legacy_Load(object sender, EventArgs e)
        {
            // Configuring here (not the constructor) so Legacy_IDE's native window handle
            // already exists - MouseDwellTime and other native Scintilla calls silently
            // no-op if sent before the control has a real HWND, which broke hover call tips.
            Zenkai.ZenkaiEditor.Configure(Legacy_IDE);
            Zenkai.OpcodeUsage.Attach(Legacy_IDE, () => _rom, NavigateToScript);

            toolStripButton1.Image = RenderGlyphIcon('\uE768', Color.Green, 32); // play
            toolStripButton2.Image = RenderGlyphIcon('\uE74E', Color.SteelBlue, 32); // save
            toolStripButton1.ToolTipText = "Test (compile-check the current script)";
            toolStripButton2.ToolTipText = "Save ROM";
            toolStripButton1.Click += (s, e2) => Legacy_IDE_BTN_Compile_Click(s, e2);
            toolStripButton2.Click += (s, e2) => Legacy_SaveROM_Click(s, e2);

            ApplyStartupArgs();
        }

        private void ApplyStartupArgs()
        {
            string? romPath = null;
            byte? zone = null, area = null;
            uint? focus = null;

            foreach (var arg in _startupArgs)
            {
                if (arg.StartsWith("--rom=")) romPath = arg.Substring("--rom=".Length).Trim('"');
                else if (arg.StartsWith("--zone=") && byte.TryParse(arg.Substring("--zone=".Length), out var z)) zone = z;
                else if (arg.StartsWith("--area=") && byte.TryParse(arg.Substring("--area=".Length), out var a)) area = a;
                else if (arg.StartsWith("--focus=") && TryParseHexAddr(arg.Substring("--focus=".Length), out var f)) focus = f;
            }

            if (romPath == null || !File.Exists(romPath))
            {
                if (romPath != null)
                    ShowStatus($"Startup ROM not found: {romPath}", isError: true);
                return;
            }

            _rom = File.ReadAllBytes(romPath);
            _saveTargetPath = romPath;

            if (zone != null && area != null)
                _pendingFocus = (zone.Value, area.Value, focus); // consumed by RenderTree, called at the end of PopulateDialogTree below

            PopulateDialogTree();
        }

        private static bool TryParseHexAddr(string s, out uint value)
        {
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            return uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out value);
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

        private string? _saveTargetPath;

        private void Legacy_SaveROM_Click(object sender, EventArgs e) => PerformSave(forceSaveAs: false);

        private void Legacy_SaveROMAs_Click(object sender, EventArgs e) => PerformSave(forceSaveAs: true);

        private void PerformSave(bool forceSaveAs)
        {
            if (_rom == null)
            {
                ShowStatus("No ROM loaded.", isError: true);
                return;
            }

            CommitPendingEdit();

            // Collect every dirty node across the whole model - NOT just the visible tree,
            // which is rebuilt on demand and can lose nodes.
            var dirtyInfos = _dialogModel
                .SelectMany(z => z.Areas)
                .SelectMany(a => a.Children)
                .Where(c => c.Info.Editable && (c.Info.Dirty || c.Info.EndsDialogDirty))
                .Select(c => c.Info)
                .ToList();

            if (dirtyInfos.Count == 0)
            {
                ShowStatus("No changes to save.");
                return;
            }

            // Compile every dirty script up front (before touching any ROM bytes) so a bad
            // edit aborts the whole save instead of leaving the ROM half-patched. Entries only
            // toggling EndsDialogDirty (no body edit) skip this - see the Index-only patch path below.
            var compiledScripts = new Dictionary<DialogNodeInfo, byte[]>();
            foreach (var info in dirtyInfos)
            {
                if (info.Mode != 0x0 || !info.Dirty) continue;

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

            // First save (or an explicit "Save As") prompts for a destination -- that
            // prompt is the backup: the original loaded ROM is never touched. Every save
            // after that silently overwrites the same chosen file, same as any normal
            // editor's Ctrl+S, until the user explicitly picks a new target via Save As.
            string? targetPath = _saveTargetPath;
            if (forceSaveAs || targetPath == null)
            {
                using var openSaveDialog = new SaveFileDialog() { Filter = "GBA ROMs|*.gba", Title = "Save ROM As" };
                if (openSaveDialog.ShowDialog() != DialogResult.OK)
                    return;
                targetPath = openSaveDialog.FileName;
            }

            // Build the new ROM: original bytes (with any script edits that fit in place
            // applied and zero-padded) + appended entries for edited text and any script
            // that outgrew its original allocation.
            byte[] newRom = new byte[_rom.Length];
            Array.Copy(_rom, newRom, _rom.Length);
            var extension = new List<byte>();

            foreach (var info in dirtyInfos)
            {
                    // If EndsDialogHere was toggled, this entry's Index header field (2 bytes,
                    // always in place - it never changes layout/size) needs to end up pointing
                    // at TerminatorSlotIndex instead of its original next-entry slot.
                    ushort desiredIndex = info.EndsDialogHere ? (ushort)info.TerminatorSlotIndex : info.Index;

                    if (!info.Dirty)
                    {
                        // Only EndsDialogHere changed - no body edit, so just patch the header
                        // in place rather than appending a duplicate entry to the ROM.
                        uint entryOffset = info.EntryAddr - RomBase;
                        newRom[entryOffset] = (byte)(desiredIndex & 0xFF);
                        newRom[entryOffset + 1] = (byte)((desiredIndex >> 8) & 0xFF);
                        info.Index = desiredIndex;
                        continue;
                    }

                    if (info.Mode == 0x0)
                    {
                        byte[] compiled = compiledScripts[info];

                        // An original script is never overwritten: it can be shared (e.g. one trigger script used
                        // by two areas), so an edit is always written at the end of the ROM and only this node's
                        // pointer is repointed. A script this tool already appended (Relocated) is private to
                        // this node, so it can be rewritten in place when it still fits, rather than growing the
                        // ROM on every save.
                        if (info.Relocated && compiled.Length <= info.OriginalScriptLength)
                        {
                            // Fits in the space the appended copy occupies - overwrite in place and
                            // zero-pad the rest. Safe: compiled bytes always end with the END (0x11)
                            // opcode, so execution never reaches the padding, and the pointer doesn't change.
                            uint scriptOffset = info.ScriptAddr - RomBase;
                            Array.Copy(compiled, 0, newRom, scriptOffset, compiled.Length);
                            for (int i = compiled.Length; i < info.OriginalScriptLength; i++)
                                newRom[scriptOffset + i] = 0x00;

                            if (info.EndsDialogDirty)
                            {
                                uint entryOffset = info.EntryAddr - RomBase;
                                newRom[entryOffset] = (byte)(desiredIndex & 0xFF);
                                newRom[entryOffset + 1] = (byte)((desiredIndex >> 8) & 0xFF);
                            }
                            info.Index = desiredIndex;
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
                            // A trigger's plain-script payload has no header at all: the payload
                            // field points straight at the bytecode.
                            if (!info.RawScript)
                            {
                                extension.Add((byte)(desiredIndex & 0xFF));
                                extension.Add((byte)((desiredIndex >> 8) & 0xFF));
                                extension.Add(0x0); // DIALOG_SCRIPT
                            }
                            extension.AddRange(compiled);

                            uint slotOffset = info.TableSlotAddr - RomBase;
                            byte[] ptrBytes = BitConverter.GetBytes(newEntryAddr);
                            Array.Copy(ptrBytes, 0, newRom, slotOffset, 4);

                            // Entry physically moved to the appended region - keep this node's
                            // addresses in sync so further edits (and the next Save) don't
                            // write to/measure against the old, now-dead location.
                            info.Relocated = true;
                            info.EntryAddr = newEntryAddr;
                            info.ScriptAddr = newEntryAddr + (info.RawScript ? 0u : ScriptHeaderSize);
                            info.OriginalScriptLength = compiled.Length;
                            info.Index = desiredIndex;
                        }

                        continue;
                    }

                    uint textEntryAddr = RomBase + (uint)(newRom.Length + extension.Count);

                    // Header: Index(u16) + Mode=0x1(u8, force plain text) + Character(u8)
                    extension.Add((byte)(desiredIndex & 0xFF));
                    extension.Add((byte)((desiredIndex >> 8) & 0xFF));
                    extension.Add(0x1); // force DIALOG_TEXT, no re-compression
                    extension.Add(info.Character);

                    // Message bytes (with the Position/Center format prefix re-applied - see
                    // ExtractTextFormat/ApplyTextFormat) + null terminator
                    extension.AddRange(Encoding.UTF8.GetBytes(ApplyTextFormat(info.Text, info.Position, info.CenterText)));
                    extension.Add(0x00);

                    // Patch the table slot (in the ORIGINAL rom region) to point at the new entry
                    uint textSlotOffset = info.TableSlotAddr - RomBase;
                    byte[] textPtrBytes = BitConverter.GetBytes(textEntryAddr);
                    Array.Copy(textPtrBytes, 0, newRom, textSlotOffset, 4);

                    // Text entries always append fresh - keep the node in sync with where it
                    // actually lives now (see the matching comment in the mode-0 branch above).
                    info.EntryAddr = textEntryAddr;
                    info.Index = desiredIndex;
            }

            byte[] finalRom = new byte[newRom.Length + extension.Count];
            Array.Copy(newRom, finalRom, newRom.Length);
            Array.Copy(extension.ToArray(), 0, finalRom, newRom.Length, extension.Count);

            File.WriteAllBytes(targetPath, finalRom);
            _saveTargetPath = targetPath;

            // The tool now keeps editing the ROM it just wrote, not the one it originally
            // opened - otherwise a second Save would rebuild from the ORIGINAL _rom bytes
            // again, silently discarding the first save's changes. Every dirty DialogNodeInfo
            // had its addresses patched in place above (see the "info.EntryAddr = ..." lines),
            // so - unlike an earlier version of this method - there's no need to re-walk the
            // whole ROM and rebuild the tree: that wiped the user's scroll position, expanded
            // nodes, and current selection on every single save.
            _rom = finalRom;
            _originalRomLength = _rom.Length;
            _extension.Clear();

            foreach (var info in dirtyInfos)
            {
                info.Dirty = false;
                info.EndsDialogDirty = false;
            }

            ShowStatus($"Saved {dirtyInfos.Count} edited entries to {Path.GetFileName(targetPath)}.");
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