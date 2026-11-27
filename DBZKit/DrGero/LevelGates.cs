using DrGero.IO;
using DrGero.Types;
using static DrGero.Types.MapEntry;

namespace DrGero.Rendering
{
    /// <summary>
    /// A level gate is not one record. Traced in IDA (Map_LoadInternal, LevelGate_Create, MapItem_Spawn,
    /// Map_InitTilemap) and checked against all 22 gates in the US ROM (2026-11-27):
    ///
    ///   1. Gate record, MapEntry.npcArray (+0x20, count +0x07), 8 bytes:
    ///      x s16, y s16, graphicId u8, style u8, level u8, character u8.
    ///      Only draws the number (or '?') over the rock. Style follows the character:
    ///      Gohan 6, Piccolo 4, Vegeta 3, Trunks 7, Goku 39, Mr. Satan 5 (2 always shows '?').
    ///   2. The rock: the layout's graphicObjects[graphicId] decoration. Map_InitTilemap spawns every
    ///      graphicObjects entry as a "type 6" entity with its array index; the gate finds it by that index.
    ///   3. Barrier MapItem (MapEntry.mapItems), 16 bytes: condition script, itemIndex = graphicId, 1, u16 3,
    ///      handler 0x0800F9A3 (runs the script at +0xC when the rock is hit), hit script.
    ///      Condition: <c>PushActiveCharIndex; PushByte c; CmpEq; PushByte c; StackGetCharLevel; PushByte L; CmpGe; And</c>,
    ///      so the item (the breakable version) only spawns for the right character at the right level.
    ///      Hit script: <c>PlayAudio(47); SetCollisionRect(r); RemoveRectFromMask2(r); SetStoryFlag(F)</c>.
    ///   4. The map's entry script (MapEntry +0x30) carries, per gate:
    ///      <c>if (TestStoryFlag(F)) { op30(graphicId); SetCollisionRect(r); RemoveRectFromMask2(r); }</c>
    ///      op30 broadcasts message 4 to the rock, which removes it; that is how a broken rock stays gone.
    ///   5. Collision (+0x34) is blocked under r until it's broken. Ops 71 and 117 SET the rect's bits = passable
    ///      (CollisionList_InsertRemove with remove=1) in the walk grid and the shot mask (+0x38). The shot mask
    ///      under shipped rocks is already open except on Z2A2, so it isn't edited here.
    ///
    /// Four story gates (Z8A48, Z10A1, Z12A15, Z14A1) test a story flag in the condition instead and their
    /// hit script ends in ReloadMap; Z4A5 has no barrier item (its check is inline in nearby triggers).
    /// Script integers are big-endian LEB128 with zigzag (PushVarint 0x01, PushMultiVarint 0x1D).
    /// </summary>
    public static class LevelGates
    {
        public const int RecordSize = 8;
        public const int ItemSize = 16;
        public const uint BarrierItemHandler = 0x0800F9A3;
        public const int FirstNewStoryFlag = 500; // scripts in the shipped game use flags up to 480

        private const int GraphicObjectsCountOffset = 0x24, GraphicObjectsPtrOffset = 0x28;
        private const int CollisionFieldOffset = 0x34, ShotMaskFieldOffset = 0x38;
        private const int EntryScriptOffset = 0x30;

        /// <summary>Number style for each character index (0 Gohan .. 5 Mr. Satan), as every shipped gate uses.</summary>
        public static readonly byte[] StyleForCharacter = { 6, 4, 3, 7, 39, 5 };
        public static readonly string[] CharacterNames = { "Gohan", "Piccolo", "Vegeta", "Trunks", "Goku", "Mr. Satan" };

        /// <summary>Everything that makes up one gate, as file offsets (-1 = not present).</summary>
        public sealed class Bundle
        {
            public int RecordAddress;
            public int X, Y, GraphicId, Style, Level, Character;
            public List<int> Items = new();            // barrier MapItems (+4 == GraphicId)
            public int? Flag;                          // story flag the hit script sets
            public int[]? Rect;                        // x1,y1,x2,y2 from the hit script (else the rock's box)
            public int EntryBlock = -1;                // offset of the flag push that opens the entry-script block
            public string Note = "";                   // anything the editor should tell the user
        }

        // ---------------------------------------------------------------- script helpers

        /// <summary>Reads a big-endian LEB128 zigzag integer (the VM's PushVarint encoding).</summary>
        public static int ReadVarint(byte[] b, ref int p)
        {
            int v = 0;
            while (true)
            {
                byte c = b[p++];
                v = (v << 7) | (c & 0x7F);
                if ((c & 0x80) == 0) break;
            }
            return (int)((uint)v >> 1) ^ -(v & 1);
        }

        /// <summary>Encodes a varint, padded with leading zero groups to at least <paramref name="width"/> bytes.</summary>
        public static byte[] EncodeVarint(int value, int width = 1)
        {
            uint z = (uint)((value << 1) ^ (value >> 31));
            var groups = new List<byte> { (byte)(z & 0x7F) };
            z >>= 7;
            while (z != 0) { groups.Insert(0, (byte)(z & 0x7F)); z >>= 7; }
            while (groups.Count < width) groups.Insert(0, 0);
            for (int i = 0; i < groups.Count - 1; i++) groups[i] |= 0x80;
            return groups.ToArray();
        }

        private static byte[] Push(int value) => value is >= 0 and <= 255 ? new byte[] { 0x00, (byte)value } : new byte[] { 0x01 }.Concat(EncodeVarint(value)).ToArray();

        private static byte[] PushRect(int[] r)
        {
            var bytes = new List<byte> { 0x1D, 4 };
            foreach (int v in r) bytes.AddRange(EncodeVarint(v, 2)); // 2 bytes covers every map coordinate, so later moves fit in place
            return bytes.ToArray();
        }

        /// <summary>One VM instruction: where it starts, its opcode and total length.</summary>
        private readonly record struct Token(int At, byte Op, int Length);

        /// <summary>
        /// Walks a script instruction by instruction to its real end: an END (0x11) that no earlier forward
        /// jump goes past. Jumps are relative to the byte after their operand. Null for an opcode outside
        /// 0x00-0x1D or a script that runs off the ROM.
        /// </summary>
        private static List<Token>? Tokenize(byte[] b, int start, int max = 0x2000)
        {
            var tokens = new List<Token>();
            int p = start, furthest = start;
            while (p < b.Length && p - start < max)
            {
                byte op = b[p];
                int q = p + 1;
                switch (op)
                {
                    case 0x00: case 0x02: case 0x03: q += 1; break;
                    case 0x01: while (q < b.Length && (b[q] & 0x80) != 0) q++; q++; break;
                    case >= 0x04 and <= 0x10: case 0x14: case 0x1B: break;
                    case 0x11:
                        tokens.Add(new Token(p, op, 1));
                        if (p >= furthest) return tokens;
                        p = q; continue;
                    case 0x12: case 0x13: case 0x1C:
                        {
                            int target = q + 1 + (sbyte)b[q];
                            if (target > furthest) furthest = target;
                            q += 1; break;
                        }
                    case >= 0x15 and <= 0x1A: q += 2; break;
                    case 0x1D:
                        {
                            int n = b[q++];
                            for (int i = 0; i < n && q < b.Length; i++) { while (q < b.Length && (b[q] & 0x80) != 0) q++; q++; }
                            break;
                        }
                    default: return null;
                }
                tokens.Add(new Token(p, op, q - p));
                p = q;
            }
            return null;
        }

        /// <summary>Total length of the script at <paramref name="start"/>, or -1 when it can't be walked.</summary>
        public static int ScriptLength(byte[] b, int start)
        {
            var t = Tokenize(b, start);
            return t == null || t.Count == 0 ? -1 : t[^1].At + t[^1].Length - start;
        }

        // The value pushed by the instruction that ends right before 'at', or null.
        private static int? PushedBefore(byte[] b, List<Token> tokens, int index)
        {
            if (index <= 0) return null;
            var t = tokens[index - 1];
            if (t.Op == 0x00) return b[t.At + 1];
            if (t.Op == 0x01) { int p = t.At + 1; return ReadVarint(b, ref p); }
            return null;
        }

        private static int[] ReadRect(byte[] b, int at) // at = the 0x1D opcode
        {
            int p = at + 2;
            var r = new int[4];
            for (int i = 0; i < 4; i++) r[i] = ReadVarint(b, ref p);
            return r;
        }

        private static int ToFileOffset(uint ptr, int length)
        {
            if (ptr < 0x08000000 || ptr >= 0x0A000000) return -1;
            int o = (int)(ptr & 0x01FFFFFF);
            return o < length ? o : -1;
        }

        private static int CountPointersTo(byte[] b, int fileOffset)
        {
            uint target = 0x08000000u | (uint)fileOffset;
            int n = 0;
            for (int i = 0; i + 4 <= b.Length; i += 4)
                if (BitConverter.ToUInt32(b, i) == target) n++;
            return n;
        }

        // ---------------------------------------------------------------- reading a gate

        public static Bundle? Read(ROM rom, MapEntry entry, int recordAddress)
        {
            byte[] b = rom.RawBytes;
            if (recordAddress <= 0 || recordAddress + RecordSize > b.Length) return null;

            var g = new Bundle
            {
                RecordAddress = recordAddress,
                X = BitConverter.ToInt16(b, recordAddress),
                Y = BitConverter.ToInt16(b, recordAddress + 2),
                GraphicId = b[recordAddress + 4],
                Style = b[recordAddress + 5],
                Level = b[recordAddress + 6],
                Character = b[recordAddress + 7],
            };

            for (int i = 0; entry.MapItems != 0 && i < entry.ItemCount; i++)
            {
                int item = ToFileOffset(BitConverter.ToUInt32(b, entry.MapItems + i * 4), b.Length);
                if (item < 0 || item + ItemSize > b.Length || b[item + 4] != g.GraphicId) continue;
                if (BitConverter.ToUInt32(b, item + 8) != BarrierItemHandler) continue;
                g.Items.Add(item);

                int hit = ToFileOffset(BitConverter.ToUInt32(b, item + 12), b.Length);
                var tokens = hit < 0 ? null : Tokenize(b, hit);
                if (tokens == null) continue;
                for (int t = 0; t < tokens.Count; t++)
                {
                    if (tokens[t].Op == 0x1D && b[tokens[t].At + 1] == 4 && g.Rect == null) g.Rect = ReadRect(b, tokens[t].At);
                    if (tokens[t].Op == 0x02 && b[tokens[t].At + 1] == 27 && g.Flag == null) g.Flag = PushedBefore(b, tokens, t);
                }
            }

            if (g.Flag is int flag && entry.EntryScript > 0)
            {
                var tokens = Tokenize(b, entry.EntryScript);
                for (int t = 1; tokens != null && t + 1 < tokens.Count; t++)
                    if (tokens[t].Op == 0x02 && b[tokens[t].At + 1] == 19 && tokens[t + 1].Op == 0x13 && PushedBefore(b, tokens, t) == flag)
                    { g.EntryBlock = tokens[t - 1].At; break; }
            }

            if (g.Items.Count == 0)
                g.Note = "This gate has no barrier item: its check lives in the triggers around it, so only the number and the rock move.";
            else if (g.Flag == null)
                g.Note = "A story gate: the story removes it (its condition tests a story flag), not breaking it.";
            return g;
        }

        /// <summary>The rect the game clears when the rock breaks: the hit script's, else the rock's box.</summary>
        public static int[]? BarrierRect(ROM rom, Bundle g, int layoutOffset)
        {
            if (g.Rect != null) return g.Rect;
            var box = DecorationBox(rom, layoutOffset, g.GraphicId);
            return box;
        }

        public static int[]? DecorationBox(ROM rom, int layoutOffset, int index)
        {
            byte[] b = rom.RawBytes;
            int count = BitConverter.ToInt32(b, layoutOffset + GraphicObjectsCountOffset);
            int baseAt = ToFileOffset(BitConverter.ToUInt32(b, layoutOffset + GraphicObjectsPtrOffset), b.Length);
            if (baseAt < 0 || index < 0 || index >= count) return null;
            int r = baseAt + index * EntityReader.GraphicObjectRecordSize;
            return new[] { BitConverter.ToInt32(b, r), BitConverter.ToInt32(b, r + 4), BitConverter.ToInt32(b, r + 8), BitConverter.ToInt32(b, r + 12) };
        }

        // ---------------------------------------------------------------- collision

        /// <summary>Sets every 8x8 tile CollisionList_InsertRemove would touch for this rect, in the walk grid and the shot mask.</summary>
        // Applied in order, with the grid opened once (its first change copies the whole 8 KB grid to free space).
        // Only the walk grid: under 20 of the 22 shipped rocks the shot mask is already open (the rock entity itself
        // stops shots), so it's left alone; the scripts' RemoveRectFromMask2 still runs when a rock breaks.
        private static void SetRectPassable(ROM rom, int layoutOffset, params (int[] Rect, bool Passable)[] edits)
        {
            foreach (int field in new[] { CollisionFieldOffset })
            {
                var grid = CollisionEditor.Open(rom, layoutOffset, field);
                if (grid == null) continue;
                foreach (var (r, passable) in edits)
                {
                    int tx1 = Math.Max(0, r[0]) >> 3, ty1 = Math.Max(0, r[1]) >> 3;
                    int tx2 = r[2] < 2048 ? (r[2] + 7) >> 3 : 256, ty2 = r[3] < 2048 ? (r[3] + 7) >> 3 : 256;
                    for (int ty = ty1; ty < ty2; ty++)
                        for (int tx = tx1; tx < tx2; tx++)
                            grid.SetAllowed(tx, ty, passable);
                }
            }
        }

        private static void SetRectPassable(ROM rom, int layoutOffset, int[] r, bool passable) => SetRectPassable(rom, layoutOffset, (r, passable));

        // ---------------------------------------------------------------- script rewriting

        /// <summary>
        /// Gives this field's script its own copy when something else points at it too (Z10A5 and Z10A22 share an
        /// entry script; Z2A32 and Z3A7 share a condition), so editing one gate never changes another.
        /// </summary>
        private static int MakePrivate(ROM rom, int pointerField, int script)
        {
            byte[] b = rom.RawBytes;
            if (CountPointersTo(b, script) <= 1) return script;
            int len = ScriptLength(b, script);
            if (len <= 0) return script;
            int copy = rom.AllocateFreeSpace(len);
            rom.WriteBytesAt(copy, rom.ReadBytesAt(script, len));
            rom.PatchInt32(pointerField, 0x08000000 | copy);
            return copy;
        }

        /// <summary>
        /// Replaces every PushMultiVarint(4) equal to <paramref name="from"/> in the script with <paramref name="to"/>.
        /// In place when the new numbers fit the old bytes; otherwise (only for scripts without jumps) the script is
        /// re-emitted into free space and the pointer field repointed. False if it couldn't be done.
        /// </summary>
        private static bool RewriteRects(ROM rom, int pointerField, int[] from, int[] to)
        {
            byte[] b = rom.RawBytes;
            int script = ToFileOffset(BitConverter.ToUInt32(b, pointerField), b.Length);
            if (script < 0) return false;
            script = MakePrivate(rom, pointerField, script);
            b = rom.RawBytes;
            var tokens = Tokenize(b, script);
            if (tokens == null) return false;

            var hits = tokens.Where(t => t.Op == 0x1D && b[t.At + 1] == 4 && ReadRect(b, t.At).SequenceEqual(from)).ToList();
            if (hits.Count == 0) return true;

            bool fits = true;
            foreach (var t in hits)
            {
                var bytes = new List<byte> { 0x1D, 4 };
                int p = t.At + 2;
                foreach (int v in to)
                {
                    int start = p; ReadVarint(b, ref p);
                    var enc = EncodeVarint(v, p - start);
                    if (enc.Length != p - start) { fits = false; break; }
                    bytes.AddRange(enc);
                }
                if (!fits) break;
            }

            if (fits)
            {
                foreach (var t in hits)
                {
                    var bytes = new List<byte> { 0x1D, 4 };
                    int p = t.At + 2;
                    foreach (int v in to) { int start = p; ReadVarint(b, ref p); bytes.AddRange(EncodeVarint(v, p - start)); }
                    rom.WriteBytesAt(t.At, bytes.ToArray());
                }
                return true;
            }

            if (tokens.Any(t => t.Op is 0x12 or 0x13 or 0x1C)) return false; // lengths would shift jump targets

            var output = new List<byte>();
            foreach (var t in tokens)
                output.AddRange(hits.Contains(t) ? PushRect(to) : rom.ReadBytesAt(t.At, t.Length));
            int copy = rom.AllocateFreeSpace(output.Count);
            rom.WriteBytesAt(copy, output.ToArray());
            rom.PatchInt32(pointerField, 0x08000000 | copy);
            return true;
        }

        private static byte[] EntryBlock(int flag, int graphicId, int[] rect, bool always)
        {
            var body = new List<byte>();
            body.AddRange(Push(graphicId)); body.AddRange(new byte[] { 0x02, 30 });       // op30: remove the rock
            body.AddRange(PushRect(rect)); body.AddRange(new byte[] { 0x02, 71 });        // SetCollisionRect: walkable
            body.AddRange(PushRect(rect)); body.AddRange(new byte[] { 0x02, 117 });       // RemoveRectFromMask2: shots pass
            if (always) return body.ToArray();
            var block = new List<byte>();
            block.AddRange(Push(flag)); block.AddRange(new byte[] { 0x02, 19, 0x13, (byte)body.Count }); // if (TestStoryFlag(F))
            block.AddRange(body);
            return block.ToArray();
        }

        /// <summary>Puts <paramref name="block"/> in front of the map's entry script (a fresh one if it has none).</summary>
        private static bool PrependToEntryScript(ROM rom, int mapEntryAddress, byte[] block)
        {
            byte[] b = rom.RawBytes;
            int old = ToFileOffset(BitConverter.ToUInt32(b, mapEntryAddress + EntryScriptOffset), b.Length);
            byte[] rest = new byte[] { 0x11 };
            if (old >= 0)
            {
                int len = ScriptLength(b, old);
                if (len <= 0) return false;
                rest = rom.ReadBytesAt(old, len);
            }
            var bytes = block.Concat(rest).ToArray();
            int at = rom.AllocateFreeSpace(bytes.Length);
            rom.WriteBytesAt(at, bytes);
            rom.PatchInt32(mapEntryAddress + EntryScriptOffset, 0x08000000 | at); // the old copy stays as dead bytes (no reclaiming here)
            return true;
        }

        // Turns an existing "if (TestStoryFlag(F))" into always (true) or never (false), in place. The flag
        // value is still on the stack, so: Not(Not F) = 1, or Not(Negate F) = 0 -- two bytes either way.
        private static void ForceEntryBlock(ROM rom, int mapEntryAddress, int flagPushAt, bool always)
        {
            byte[] b = rom.RawBytes;
            int script = ToFileOffset(BitConverter.ToUInt32(b, mapEntryAddress + EntryScriptOffset), b.Length);
            int delta = flagPushAt - script;
            int priv = MakePrivate(rom, mapEntryAddress + EntryScriptOffset, script);
            int at = priv + delta;
            b = rom.RawBytes;
            int stepAt = at + (b[at] == 0x00 ? 2 : 1 + EncodedLength(b, at + 1));
            rom.WriteBytesAt(stepAt, always ? new byte[] { 0x05, 0x05 } : new byte[] { 0x06, 0x05 });
        }

        private static int EncodedLength(byte[] b, int p) { int s = p; while ((b[p] & 0x80) != 0) p++; return p + 1 - s; }

        // ---------------------------------------------------------------- operations

        /// <summary>
        /// Moves everything that belongs to a gate by (dx, dy): the rock in every layout whose graphicObjects[id]
        /// still sits on the old rect, the rects in the hit script and the entry-script block, and the blocked
        /// collision/shot-mask tiles. The gate record itself is moved by the caller (the editor drags it live).
        /// </summary>
        public static string Move(ROM rom, MapEntry entry, int mapEntryAddress, IReadOnlyList<int> layouts, int recordAddress, int dx, int dy)
        {
            if (dx == 0 && dy == 0) return "";
            var g = Read(rom, entry, recordAddress);
            if (g == null) return "";

            foreach (int layout in layouts)
            {
                var box = DecorationBox(rom, layout, g.GraphicId);
                if (box == null) continue;
                var oldRect = g.Rect ?? box;
                if (box[2] <= oldRect[0] || box[0] >= oldRect[2] || box[3] <= oldRect[1] || box[1] >= oldRect[3]) continue; // not this gate's rock

                int baseAt = ToFileOffset(BitConverter.ToUInt32(rom.RawBytes, layout + GraphicObjectsPtrOffset), rom.Length);
                int r = baseAt + g.GraphicId * EntityReader.GraphicObjectRecordSize;
                rom.PatchInt32(r, box[0] + dx); rom.PatchInt32(r + 4, box[1] + dy);
                rom.PatchInt32(r + 8, box[2] + dx); rom.PatchInt32(r + 12, box[3] + dy);

                if (g.Items.Count > 0) // a real barrier: its tiles are blocked until broken
                {
                    var newRect = new[] { oldRect[0] + dx, oldRect[1] + dy, oldRect[2] + dx, oldRect[3] + dy };
                    SetRectPassable(rom, layout, (oldRect, true), (newRect, false));
                }
            }

            if (g.Rect == null) return g.Note;
            var to = new[] { g.Rect[0] + dx, g.Rect[1] + dy, g.Rect[2] + dx, g.Rect[3] + dy };
            string problems = "";
            foreach (int item in g.Items)
                if (!RewriteRects(rom, item + 12, g.Rect, to)) problems = "Couldn't update the rock's hit script. ";

            if (g.EntryBlock >= 0 && !RewriteRects(rom, mapEntryAddress + EntryScriptOffset, g.Rect, to))
            {
                // The new numbers don't fit in place and the entry script has jumps: switch the old block off
                // and put a fresh one (same flag, new rect) in front instead.
                ForceEntryBlock(rom, mapEntryAddress, g.EntryBlock, always: false);
                if (g.Flag is not int f || !PrependToEntryScript(rom, mapEntryAddress, EntryBlock(f, g.GraphicId, to, false)))
                    problems += "Couldn't update the map's entry script. ";
            }
            entry.RefreshFrom(rom, mapEntryAddress);
            return (problems + g.Note).Trim();
        }

        /// <summary>
        /// Removes a gate for good. The rock is left in the layout (decoration indices can't shift: other items
        /// and gates refer to them by index), and the map's entry script removes it on every load -- the same
        /// thing the game does once a rock has been broken. The barrier item and the gate record are deleted
        /// and the tiles under it made passable.
        /// </summary>
        public static string Delete(ROM rom, MapEntry entry, int mapEntryAddress, IReadOnlyList<int> layouts, int recordAddress)
        {
            var g = Read(rom, entry, recordAddress);
            if (g == null) return "That gate couldn't be read.";
            int[]? rect = layouts.Count > 0 ? BarrierRect(rom, g, layouts[0]) : g.Rect;

            // 1. The rock goes away on every load.
            bool removedRock = false;
            if (g.EntryBlock >= 0) { ForceEntryBlock(rom, mapEntryAddress, g.EntryBlock, always: true); removedRock = true; }
            else if (rect != null) removedRock = PrependToEntryScript(rom, mapEntryAddress, EntryBlock(0, g.GraphicId, rect, always: true));

            // 2. Passable in the data too, so the editor shows it.
            if (rect != null)
                foreach (int layout in layouts) SetRectPassable(rom, layout, rect, true);

            // 3. Barrier items out of mapItems[].
            if (g.Items.Count > 0)
            {
                byte[] b = rom.RawBytes;
                var keep = new List<byte>();
                int kept = 0;
                for (int i = 0; i < entry.ItemCount; i++)
                {
                    uint ptr = BitConverter.ToUInt32(b, entry.MapItems + i * 4);
                    if (g.Items.Contains(ToFileOffset(ptr, b.Length))) continue;
                    keep.AddRange(BitConverter.GetBytes(ptr)); kept++;
                }
                int at = kept == 0 ? 0 : rom.AllocateFreeSpace(keep.Count);
                if (kept > 0) rom.WriteBytesAt(at, keep.ToArray());
                rom.PatchInt32(mapEntryAddress + 0x18, kept == 0 ? 0 : 0x08000000 | at);
                rom.PatchByte(mapEntryAddress + 0x05, (byte)kept);
            }

            // 4. The gate record out of npcArray[].
            {
                byte[] b = rom.RawBytes;
                var keep = new List<byte>();
                int kept = 0;
                for (int i = 0; i < entry.NpcCount; i++)
                {
                    int rec = entry.NpcArray + i * RecordSize;
                    if (rec == recordAddress) continue;
                    keep.AddRange(rom.ReadBytesAt(rec, RecordSize)); kept++;
                }
                int at = kept == 0 ? 0 : rom.AllocateFreeSpace(keep.Count);
                if (kept > 0) rom.WriteBytesAt(at, keep.ToArray());
                rom.PatchInt32(mapEntryAddress + 0x20, kept == 0 ? 0 : 0x08000000 | at);
                rom.PatchByte(mapEntryAddress + 0x07, (byte)kept);
            }

            entry.RefreshFrom(rom, mapEntryAddress);
            return removedRock ? g.Note : ("The gate is gone, but its rock couldn't be scripted away (the entry script couldn't be read). " + g.Note).Trim();
        }

        /// <summary>The lowest story flag at or above <see cref="FirstNewStoryFlag"/> that no script sets, clears or tests.</summary>
        public static int FindFreeStoryFlag(ROM rom, ISet<int>? alsoTaken = null)
        {
            byte[] b = rom.RawBytes;
            var used = new HashSet<int>(alsoTaken ?? new HashSet<int>());
            for (int p = 0; p + 3 < b.Length; p++)
            {
                byte op;
                if (b[p] == 0x00) { op = b[p + 3]; if (b[p + 2] == 0x02 && op is 19 or 20 or 27 or 28) used.Add(b[p + 1]); continue; }
                if (b[p] != 0x01) continue;
                int q = p + 1, len = 0;
                while (q < b.Length && (b[q] & 0x80) != 0 && len < 4) { q++; len++; }
                if (q + 2 >= b.Length || len >= 4) continue;
                if (b[q + 1] == 0x02 && b[q + 2] is 19 or 20 or 27 or 28) { int r = p + 1; used.Add(ReadVarint(b, ref r)); }
            }
            int flag = FirstNewStoryFlag;
            while (used.Contains(flag)) flag++;
            return flag;
        }

        /// <summary>
        /// Builds a complete new gate at (x, y) (the rock's centre, like every shipped gate): a rock copied from the
        /// layout's decoration <paramref name="rockTemplate"/> (file offset of a graphicObjects record), the gate
        /// record, a barrier item with its condition and hit script, an entry-script block on a fresh story flag,
        /// and blocked collision under it. Only for maps with one layout: the gate refers to the rock by index,
        /// and another layout would have a different decoration (or none) at that index.
        /// </summary>
        public static int PlaceNew(ROM rom, MapEntry entry, int mapEntryAddress, IReadOnlyList<int> layouts, int x, int y,
            int character, int level, int rockTemplate, ISet<int> flagsTaken, int rockLayout = 0)
        {
            if (layouts.Count != 1)
                throw new InvalidOperationException("Level gates can only be placed on maps with one layout (this one has " + layouts.Count + "): the gate finds its rock by index, which differs per layout.");
            if (character is < 0 or > 5) throw new InvalidOperationException("Pick a character for the gate.");
            int layout = layouts[0];
            byte[] b = rom.RawBytes;

            // --- the rock: a copy of the chosen decoration, centred on (x, y)
            int count = BitConverter.ToInt32(b, layout + GraphicObjectsCountOffset);
            int baseAt = ToFileOffset(BitConverter.ToUInt32(b, layout + GraphicObjectsPtrOffset), b.Length);
            if (count >= 255) throw new InvalidOperationException("This layout already has 255 decorations; a gate's rock index is one byte.");
            byte[] template = Decorations.TemplateFor(rom, layout, rockTemplate, rockLayout); // imports the frame if the rock is from another map
            b = rom.RawBytes;
            int w = BitConverter.ToInt32(template, 8) - BitConverter.ToInt32(template, 0);
            int h = BitConverter.ToInt32(template, 12) - BitConverter.ToInt32(template, 4);
            var rect = new[] { x - w / 2, y - h / 2, x - w / 2 + w, y - h / 2 + h };
            byte[] rock = (byte[])template.Clone();
            for (int i = 0; i < 4; i++) BitConverter.GetBytes(rect[i]).CopyTo(rock, i * 4);
            rock[Decorations.VisibleFlagOffset] = 1; // shown until broken (the entry block hides it after)

            var objects = new List<byte>(baseAt < 0 ? Array.Empty<byte>() : rom.ReadBytesAt(baseAt, count * EntityReader.GraphicObjectRecordSize));
            objects.AddRange(rock);
            int objectsAt = rom.AllocateFreeSpace(objects.Count);
            rom.WriteBytesAt(objectsAt, objects.ToArray());
            rom.PatchInt32(layout + GraphicObjectsCountOffset, count + 1);
            rom.PatchInt32(layout + GraphicObjectsPtrOffset, 0x08000000 | objectsAt);
            int graphicId = count;

            // --- scripts
            int flag = FindFreeStoryFlag(rom, flagsTaken);
            flagsTaken.Add(flag);
            byte c = (byte)character, lv = (byte)Math.Clamp(level, 0, 255);
            byte[] condition = { 0x02, 11, 0x00, c, 0x0B, 0x00, c, 0x02, 22, 0x00, lv, 0x0E, 0x04, 0x11 };
            var hit = new List<byte> { 0x00, 47, 0x02, 46 };                                 // PlayAudio(47): the break sound
            hit.AddRange(PushRect(rect)); hit.AddRange(new byte[] { 0x02, 71 });
            hit.AddRange(PushRect(rect)); hit.AddRange(new byte[] { 0x02, 117 });
            hit.AddRange(Push(flag)); hit.AddRange(new byte[] { 0x02, 27, 0x11 });           // SetStoryFlag(F)
            int conditionAt = rom.AllocateFreeSpace(condition.Length);
            rom.WriteBytesAt(conditionAt, condition);
            int hitAt = rom.AllocateFreeSpace(hit.Count);
            rom.WriteBytesAt(hitAt, hit.ToArray());

            // --- barrier item, appended to mapItems[]
            var item = new List<byte>();
            item.AddRange(BitConverter.GetBytes(0x08000000 | conditionAt));
            item.AddRange(new byte[] { (byte)graphicId, 1, 3, 0 });
            item.AddRange(BitConverter.GetBytes(BarrierItemHandler));
            item.AddRange(BitConverter.GetBytes(0x08000000 | hitAt));
            int itemAt = rom.AllocateFreeSpace(item.Count);
            rom.WriteBytesAt(itemAt, item.ToArray());

            var items = new List<byte>(entry.MapItems == 0 || entry.ItemCount == 0 ? Array.Empty<byte>() : rom.ReadBytesAt(entry.MapItems, entry.ItemCount * 4));
            items.AddRange(BitConverter.GetBytes(0x08000000 | itemAt));
            int itemsAt = rom.AllocateFreeSpace(items.Count);
            rom.WriteBytesAt(itemsAt, items.ToArray());
            rom.PatchInt32(mapEntryAddress + 0x18, 0x08000000 | itemsAt);
            rom.PatchByte(mapEntryAddress + 0x05, (byte)Math.Min(255, entry.ItemCount + 1));

            // --- gate record, appended to npcArray[]
            var gates = new List<byte>(entry.NpcArray == 0 || entry.NpcCount == 0 ? Array.Empty<byte>() : rom.ReadBytesAt(entry.NpcArray, entry.NpcCount * RecordSize));
            gates.AddRange(BitConverter.GetBytes((short)x));
            gates.AddRange(BitConverter.GetBytes((short)y));
            gates.AddRange(new[] { (byte)graphicId, StyleForCharacter[character], lv, c });
            int gatesAt = rom.AllocateFreeSpace(gates.Count);
            rom.WriteBytesAt(gatesAt, gates.ToArray());
            rom.PatchInt32(mapEntryAddress + 0x20, 0x08000000 | gatesAt);
            rom.PatchByte(mapEntryAddress + 0x07, (byte)Math.Min(255, entry.NpcCount + 1));

            // --- once broken, stays broken; until then the tiles under it are blocked
            if (!PrependToEntryScript(rom, mapEntryAddress, EntryBlock(flag, graphicId, rect, false)))
                throw new InvalidOperationException("This map's entry script couldn't be read, so a broken gate wouldn't stay broken. Nothing about the gate was finished.");
            SetRectPassable(rom, layout, rect, false);

            entry.RefreshFrom(rom, mapEntryAddress);
            return gatesAt + (entry.NpcCount - 1) * RecordSize;
        }

        /// <summary>
        /// Saves placed decorations (EntityKind.Decoration, SourceAddress 0) into the layout's graphicObjects[]:
        /// each copies its template record (Entity.Handler = file offset of an existing decoration, on this layout
        /// or, with Entity.StatIndex = its layout, on any map -- see Decorations.TemplateFor) with the box moved to
        /// the entity's X/Y. Appending keeps every existing index, which items and gates refer to.
        /// </summary>
        public static int PersistNewDecorations(ROM rom, int layoutOffset, List<Entity> entities)
        {
            var placed = entities.Where(e => e.Kind == EntityKind.Decoration && e.SourceAddress == 0 && e.Handler > 0).ToList();
            if (placed.Count == 0) return 0;

            byte[] b = rom.RawBytes;
            int count = BitConverter.ToInt32(b, layoutOffset + GraphicObjectsCountOffset);
            int baseAt = ToFileOffset(BitConverter.ToUInt32(b, layoutOffset + GraphicObjectsPtrOffset), b.Length);
            var bytes = new List<byte>(baseAt < 0 ? Array.Empty<byte>() : rom.ReadBytesAt(baseAt, count * EntityReader.GraphicObjectRecordSize));
            if (count + placed.Count > 255)
                throw new InvalidOperationException("A layout can hold at most 255 decorations (items and gates refer to them by a one-byte index).");
            foreach (var d in placed)
            {
                // StatIndex = the template's layout when it comes from another map (0 = this layout): its frame's
                // tiles are copied into this layout's sheet first.
                byte[] rec = Decorations.TemplateFor(rom, layoutOffset, d.Handler, d.StatIndex);
                int w = BitConverter.ToInt32(rec, 8) - BitConverter.ToInt32(rec, 0);
                int h = BitConverter.ToInt32(rec, 12) - BitConverter.ToInt32(rec, 4);
                BitConverter.GetBytes(d.X).CopyTo(rec, 0);
                BitConverter.GetBytes(d.Y).CopyTo(rec, 4);
                BitConverter.GetBytes(d.X + w).CopyTo(rec, 8);
                BitConverter.GetBytes(d.Y + h).CopyTo(rec, 12);
                rec[Decorations.VisibleFlagOffset] = 1; // the template may be an event decoration that starts hidden
                bytes.AddRange(rec);
            }
            int at = rom.AllocateFreeSpace(bytes.Count);
            rom.WriteBytesAt(at, bytes.ToArray());
            rom.PatchInt32(layoutOffset + GraphicObjectsCountOffset, count + placed.Count);
            rom.PatchInt32(layoutOffset + GraphicObjectsPtrOffset, 0x08000000 | at);
            return placed.Count;
        }

        /// <summary>
        /// Saves placed gates (EntityKind.LevelGate, SourceAddress 0: TypeId = level, Variant = character,
        /// Handler = rock template record, StatIndex = that record's layout when it's from another map, else 0).
        /// </summary>
        public static int PersistNewGates(ROM rom, MapEntry entry, int mapEntryAddress, IReadOnlyList<int> layouts, List<Entity> entities)
        {
            var placed = entities.Where(e => e.Kind == EntityKind.LevelGate && e.SourceAddress == 0).ToList();
            var taken = new HashSet<int>();
            foreach (var g in placed)
                PlaceNew(rom, entry, mapEntryAddress, layouts, g.X, g.Y, g.Variant, g.TypeId, g.Handler, taken, g.StatIndex);
            return placed.Count;
        }
    }
}
