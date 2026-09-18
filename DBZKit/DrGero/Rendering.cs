using DrGero.Config;
using DrGero.IO;
using DrGero.Types;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using static DrGero.Types.MapEntry;

namespace DrGero.Rendering
{
    public static class EntityReader
    {
        private const int NpcEntrySize = 8;
        private const int MapTriggerEntrySize = 8;

        public static List<Entity> ReadMapTriggers(ROM rom, MapEntry entry)
        {
            var result = new List<Entity>();

            if (entry.MapTriggers == 0 || entry.TriggerCount == 0)
                return result;

            rom.PushPosition(entry.MapTriggers);

            for (int i = 0; i < entry.TriggerCount; i++)
            {
                int entryAddress = entry.MapTriggers + (i * MapTriggerEntrySize);

                int vTable = rom.ReadPointer();
                int dataPtr = rom.ReadPointer();

                if (dataPtr != 0)
                {
                    rom.PushPosition(dataPtr);
                    rom.Skip(8); // condition + function pointers, common to all trigger variants
                    int coordAddress = dataPtr + 8;

                    // CONFIRMED via the in-game trigger hit-test (sub_800BFC0 in the ROM):
                    // it tests player position as x1<=X<x2 && y1<=Y<y2 against these exact
                    // four fields, so all four trigger variants (DualRect/Dialog/Script/
                    // SavePoint) store their zone as absolute corners (x1,y1)-(x2,y2) with
                    // an exclusive upper bound. IDA previously labeled the third/fourth
                    // fields "width"/"height" on Dialog/Script/SavePoint; that was a stale
                    // placeholder guess and has been corrected to x2/y2 to match DualRect.
                    int x1 = (short)rom.ReadShort();
                    int y1 = (short)rom.ReadShort();
                    int x2 = (short)rom.ReadShort();
                    int y2 = (short)rom.ReadShort();
                    rom.PopPosition();

                    // Normalize so X/Y is always the top-left corner and Width/Height
                    // are non-negative, regardless of which corner the ROM data
                    // actually stores first.
                    int left = Math.Min(x1, x2);
                    int top = Math.Min(y1, y2);
                    int width = Math.Abs(x2 - x1);
                    int height = Math.Abs(y2 - y1);

                    result.Add(new Entity(EntityKind.Trigger, left, top, vTable, entryAddress, width, height, coordAddress));
                }
            }

            rom.PopPosition();
            return result;
        }

        // CONFIRMED via IDA 2026-09 (Map_InitTriggers -> MapRenderer_LoadVariationTriggers ->
        // TriggerList_AddFromArray): triggers don't only come from MapEntry.mapTriggers (read
        // above) -- Map_VariationEntry has a SECOND, entirely separate trigger source at
        // +0x40 (count) / +0x44 (array), loaded every time a map loads regardless of which
        // MapEntry variation is active. TriggerList_AddFromArray's call convention
        // (a1=*(array+4*i), a2=**(array+4*i)) exactly matches Map_InitTriggers' own
        // (a1=trigger->function, a2=*trigger->function), so each array slot is a DIRECT
        // pointer to a trigger record -- same shape as ReadMapTriggers' dataPtr (skip 8
        // bytes, then x1/y1/x2/y2 as signed int16 corners at +8), just missing the outer
        // {vTable,dataPtr} wrapper mapTriggers[] uses. The map editor previously only read
        // mapTriggers[], so anything registered exclusively here (a strong candidate for the
        // reported "save point renders as two halves" symptom, if one half lives in each
        // array) was invisible. Not independently confirmed that SavePoint specifically lives
        // here vs. mapTriggers -- confirmed only that this array exists, is real, and was
        // previously unread.
        public static List<Entity> ReadVariationTriggers(ROM rom, int mapOffset)
        {
            var result = new List<Entity>();

            rom.PushPosition(mapOffset + 0x40);
            int count = rom.ReadInt();
            int arrayPtr = rom.ReadPointer();
            rom.PopPosition();

            if (arrayPtr == 0 || count <= 0)
                return result;

            rom.PushPosition(arrayPtr);

            for (int i = 0; i < count; i++)
            {
                int entryAddress = arrayPtr + (i * 4);
                int dataPtr = rom.ReadPointer();

                if (dataPtr != 0)
                {
                    rom.PushPosition(dataPtr);
                    rom.Skip(8); // condition + function pointers, same layout as MapTriggerDialog
                    int coordAddress = dataPtr + 8;

                    int x1 = (short)rom.ReadShort();
                    int y1 = (short)rom.ReadShort();
                    int x2 = (short)rom.ReadShort();
                    int y2 = (short)rom.ReadShort();
                    rom.PopPosition();

                    int left = Math.Min(x1, x2);
                    int top = Math.Min(y1, y2);
                    int width = Math.Abs(x2 - x1);
                    int height = Math.Abs(y2 - y1);

                    result.Add(new Entity(EntityKind.Trigger, left, top, 0, entryAddress, width, height, coordAddress));
                }
            }

            rom.PopPosition();
            return result;
        }

        public static List<Entity> ReadObjectArray(ROM rom, MapEntry entry)
        {
            var result = new List<Entity>();

            if (entry.MapObjects == 0 || entry.ObjectCount == 0)
                return result;

            rom.PushPosition(entry.MapObjects);

            for (int i = 0; i < entry.ObjectCount; i++)
            {
                int entryAddress = entry.MapObjects + (i * 4); // flat array of 4-byte pointers, unlike triggers' vTable+dataPtr pairs
                int objectPtr = rom.ReadPointer();

                if (objectPtr != 0)
                {
                    rom.PushPosition(objectPtr);
                    rom.Skip(4); // itemSpawnCondition ptr
                    int itemId = rom.ReadInt();

                    // CONFIRMED via raw disasm of sub_800FCCC (object-spawn helper called
                    // from WorldItemObject_Spawn): `LDM R6!, {R1,R2}` loads two full 32-bit
                    // words from this field, so Map_Object.spawnPoint really is LargePoint
                    // (2x int32 / 8 bytes), not 2x int16.
                    int coordAddress = objectPtr + 8;
                    int x = rom.ReadInt();
                    int y = rom.ReadInt();
                    rom.PopPosition();

                    result.Add(new Entity(EntityKind.Object, x, y, itemId, entryAddress, PositionAddress: coordAddress));
                }
            }

            rom.PopPosition();
            return result;
        }

        // CONFIRMED via IDA (see LevelGate_Create, formerly NPCObject_Create):
        // this array does not hold walking NPCs. Each entry is a field barrier
        // that shows a required-level number (or '?' before it's been scanned)
        // above it. templateIndex looks up a shared graphic via a "type 6"
        // template registry (not traced); barrierStyle==2 always shows '?';
        // requiredLevel is the number rendered; scanFlagIndex indexes a
        // reveal-state bit array. Field names beyond x/y are medium-confidence.
        public static List<Entity> ReadLevelGates(ROM rom, MapEntry entry)
        {
            var result = new List<Entity>();

            if (entry.NpcArray == 0 || entry.NpcCount == 0)
                return result;

            rom.PushPosition(entry.NpcArray);

            for (int i = 0; i < entry.NpcCount; i++)
            {
                int entryAddress = entry.NpcArray + (i * NpcEntrySize);

                int x = rom.ReadShort();
                int y = rom.ReadShort();
                int templateIndex = rom.ReadByte();
                int barrierStyle = rom.ReadByte();
                int requiredLevel = rom.ReadByte();
                int scanFlagIndex = rom.ReadByte();

                result.Add(new Entity(EntityKind.LevelGate, x, y, requiredLevel, entryAddress));
            }

            rom.PopPosition();
            return result;
        }

        // Real walking/talking NPCs (and presumably other scripted spawns:
        // enemies, cutscene actors) live here, not in the level-gate array.
        // CONFIRMED via IDA: MapEntry.mapScripts is an array of pointers to
        // plain data records (NOT compiled per-map code) dispatched through a
        // small shared set of "handler" functions -- Map_LoadInternal calls
        // `*script` (the handler field) directly on the record itself. The
        // handler at CharacterSpawnHandlerOffset is MapScript_CreateCharacter,
        // which resolves spriteId via Character_GetSpriteId (the same function
        // used for the player's own sprite) and spawns a full 364-byte Entity
        // at `position`. This constant is this ROM build's compiled address
        // for that function (thumb-bit set, as stored in ROM data) -- same
        // caveat as TiledLayerType below: will differ per ROM build.
        internal const int CharacterSpawnHandlerOffset = 0x00D6DF;
        internal const int MapScriptRecordSize = 32; // MapScriptSpawnMultiConditional

        // CONFIRMED via IDA 2026-09-13 (disassembly-level, not decompiler
        // pseudocode -- see MapScript_CreateSprite @0x800B710): a second
        // mapScripts handler that spawns a CharacterSprite entity (a
        // party-member-shaped sprite; ArenaAlloc 388 bytes) from the SAME
        // MapScriptSpawnMultiConditional-shaped record as
        // CharacterSpawnHandlerOffset, but reads the DISPLAY sprite from a
        // different field: `LDR R0,[R0,#0x10]` -> Character_GetSpriteId, i.e.
        // record+0x10 ('actionData'), not record+0xC. record+0xC is instead
        // passed straight through into the entity (ends up at entity+0x180,
        // confirmed via `STR R6,[R1,R4]` with R1=0x180) as a raw graphics
        // pointer the entity's own vtable slot 0 dispatches through -- not a
        // display id, so it's skipped here rather than misread as one.
        //
        // Also confirmed (same session): entities from this handler have NO
        // working interactivity. Every collision type this entity's shared
        // OnCollision (CharacterSprite_OnCollision, vtable slot 9 shared with
        // PlayerObject_Create) doesn't special-case falls through to a no-op
        // chain (Entity_HandleCollision_Shared -> _Ext1 -> _Ext2), and the
        // record's own scriptFunc field (+0x14, passed by address into the
        // entity's action-record like an interact/destroy callback would be)
        // was a bare literal (e.g. `1`) in the one real example checked
        // (Zone1/Area1's tutorial-area sprite), not a function pointer -- so
        // these are decorative/background sprites, NOT talkable NPCs, even
        // though they render as full character entities in-game. Still drawn
        // here (as EntityKind.Character, same as CharacterSpawnHandlerOffset)
        // because they're real spawned entities worth seeing on the map; the
        // status bar's "spriteId" label is this handler's actionData value.
        private const int SpriteSpawnHandlerOffset = 0x00B711;

        public static List<Entity> ReadMapScripts(ROM rom, MapEntry entry)
        {
            var result = new List<Entity>();

            if (entry.MapScripts == 0 || entry.ScriptCount == 0)
                return result;

            rom.PushPosition(entry.MapScripts);

            for (int i = 0; i < entry.ScriptCount; i++)
            {
                int tableSlotAddress = entry.MapScripts + (i * 4);
                int recordAddress = rom.ReadPointer();

                if (recordAddress == 0)
                    continue;

                rom.PushPosition(recordAddress);
                int handler = rom.ReadPointer();

                if (handler == CharacterSpawnHandlerOffset)
                {
                    rom.Skip(4); // flags/condition
                    int coordAddress = recordAddress + 8;
                    int x = rom.ReadShort();
                    int y = rom.ReadShort();
                    int spriteId = rom.ReadInt();

                    result.Add(new Entity(EntityKind.Character, x, y, spriteId, recordAddress, PositionAddress: coordAddress));
                }
                else if (handler == SpriteSpawnHandlerOffset)
                {
                    rom.Skip(4); // flags/condition
                    int coordAddress = recordAddress + 8;
                    int x = rom.ReadShort();
                    int y = rom.ReadShort();
                    rom.Skip(4); // record+0xC -- passed through to the entity raw, not a display id (see comment above)
                    int spriteId = rom.ReadInt(); // record+0x10 (actionData) -- feeds Character_GetSpriteId

                    result.Add(new Entity(EntityKind.Character, x, y, spriteId, recordAddress, PositionAddress: coordAddress));
                }
                // Other handlers may exist in mapScripts (enemies, effects,
                // etc.) but haven't been identified yet -- skipped rather
                // than guessing at their record layout/position.

                rom.PopPosition();
            }

            rom.PopPosition();
            return result;
        }

        /// <summary>
        /// Scans every map's mapObjects[] for existing entries that share the given
        /// itemId, returning each distinct (onPickup, collectionMsg) pair found along
        /// with the map it came from. Lets the editor offer a real pickup script
        /// choice when placing a new item, instead of always guessing from whatever
        /// object happens to be first on the target map.
        /// </summary>
        public static List<(int OnPickup, int CollectionMsg, string MapName)> FindPickupTemplates(ROM rom, IReadOnlyList<MapEntry> mapEntries, int itemId)
        {
            var seen = new HashSet<(int, int)>();
            var result = new List<(int, int, string)>();

            foreach (var entry in mapEntries)
            {
                if (entry.MapObjects == 0 || entry.ObjectCount == 0)
                    continue;

                rom.PushPosition(entry.MapObjects);
                for (int i = 0; i < entry.ObjectCount; i++)
                {
                    int objectPtr = rom.ReadPointer();
                    if (objectPtr == 0) continue;

                    rom.PushPosition(objectPtr + 4);
                    int objItemId = rom.ReadInt();
                    if (objItemId != itemId) { rom.PopPosition(); continue; }

                    rom.PushPosition(objectPtr + 0x10);
                    int onPickup = rom.ReadInt();
                    int collectionMsg = rom.ReadInt();
                    rom.PopPosition();
                    rom.PopPosition();

                    if (seen.Add((onPickup, collectionMsg)))
                        result.Add((onPickup, collectionMsg, entry.Name));
                }
                rom.PopPosition();
            }

            return result;
        }

        internal const int GraphicObjectRecordSize = 24;

        // Ground item pickups (potions, capsules, etc.) don't store x/y directly.
        // CONFIRMED structurally via IDA (MapItem_Spawn -> MapItem_CreateCollision
        // Bounds): each MapItem's itemIndex is looked up ("type 6" entity query)
        // and the found entity stores a pointer to a Map_VariationEntry
        // graphicObjects[] record (the same decorative-object table
        // Map_InitTilemap uses) at the EXACT offset MapItem_CreateCollisionBounds
        // reads from -- confirmed by matching offsets in both functions' raw
        // disasm, not guessed. That record's [x0,y0,x1,y1] pixel bounding box
        // (offsets 0x00/0x04/0x08/0x0C) seeds the item's real in-game position.
        // Verified against real ROM data: itemIndex=0 in a real map resolved to
        // a plausible 32x32 box safely inside that map's bounds.
        //
        // NOT independently proven: that itemIndex equals graphicObjects' plain
        // array position (vs. some other registration order). This is the most
        // likely design and matched in the one case tested, but could be off
        // for some maps -- acceptable per approximate-position tolerance, not
        // suitable for anything requiring exact placement.
        public static List<Entity> ReadMapItems(ROM rom, MapEntry entry, int mapOffset)
        {
            var result = new List<Entity>();

            if (entry.MapItems == 0 || entry.ItemCount == 0)
                return result;

            rom.PushPosition(mapOffset + 0x24);
            int graphicObjectCount = rom.ReadInt();
            int graphicObjectsBase = rom.ReadPointer();
            rom.PopPosition();

            if (graphicObjectsBase == 0)
                return result;

            rom.PushPosition(entry.MapItems);

            for (int i = 0; i < entry.ItemCount; i++)
            {
                int itemAddress = rom.ReadPointer();
                if (itemAddress == 0)
                    continue;

                rom.PushPosition(itemAddress);
                rom.Skip(4); // flags/condition
                int itemIndex = rom.ReadByte();
                rom.PopPosition();

                if (itemIndex < 0 || itemIndex >= graphicObjectCount)
                    continue; // out of range -- don't guess a position

                rom.PushPosition(graphicObjectsBase + (itemIndex * GraphicObjectRecordSize));
                int x0 = rom.ReadInt();
                int y0 = rom.ReadInt();
                int x1 = rom.ReadInt();
                int y1 = rom.ReadInt();
                rom.PopPosition();

                int x = (x0 + x1) / 2;
                int y = (y0 + y1) / 2;

                result.Add(new Entity(EntityKind.Item, x, y, itemIndex, itemAddress));
            }

            rom.PopPosition();
            return result;
        }

        // CONFIRMED via IDA 2026-09 (raw disasm of sub_80073EA/sub_800733E, Map_InitTilemap's
        // graphicObjects constructors): every entry in Map_VariationEntry.graphicObjects[] is
        // spawned as a real, visible OAM sprite decoration (trees, rocks, flight pads, etc),
        // entirely separate from mapItems/mapObjects. Previously these were baked straight
        // into the static map bitmap (see MapRenderer.RenderGraphicObjectSprites' doc) with no
        // way to select or move them -- read as real Entity records instead, same as pickups.
        // X/Y is the box's top-left (x0,y0), matching how the record's own [x0,y0,x1,y1] is
        // used directly as a draw position elsewhere, not a center point like EntityKind.Item.
        public static List<Entity> ReadGraphicObjects(ROM rom, int mapOffset)
        {
            var result = new List<Entity>();

            rom.PushPosition(mapOffset + 0x24);
            int graphicObjectCount = rom.ReadInt();
            int graphicObjectsBase = rom.ReadPointer();
            rom.PopPosition();

            if (graphicObjectCount <= 0 || graphicObjectsBase == 0)
                return result;

            for (int i = 0; i < graphicObjectCount; i++)
            {
                int recordBase = graphicObjectsBase + (i * GraphicObjectRecordSize);
                rom.PushPosition(recordBase);
                int x0 = rom.ReadInt();
                int y0 = rom.ReadInt();
                int x1 = rom.ReadInt();
                int y1 = rom.ReadInt();
                rom.PopPosition();

                int width = x1 - x0;
                int height = y1 - y0;
                if (width <= 0 || height <= 0) continue; // out of range -- don't guess a position

                result.Add(new Entity(EntityKind.Decoration, x0, y0, i, recordBase, width, height, recordBase));
            }

            return result;
        }
    }

    /// <summary>
    /// Writes edited entity positions back into a ROM's byte buffer, at the
    /// exact offsets EntityReader confirmed for each kind. Trigger corners are
    /// always written in canonical min/max order regardless of how the
    /// original data was ordered -- the game's hit-test (x1&lt;=X&lt;x2 &amp;&amp;
    /// y1&lt;=Y&lt;y2) only requires x1&lt;x2 and y1&lt;y2, so this is safe.
    /// </summary>
    public static class EntityWriter
    {
        public static void WritePosition(ROM rom, Entity entity, int newX, int newY)
        {
            // Address 0 is never a legitimate entity position -- it's the start
            // of the ROM header. CONFIRMED via a real corrupted save: a
            // not-yet-persisted entity (SourceAddress == 0, so PositionAddress
            // also defaults to 0) reached this method and clobbered the boot
            // instruction at file offset 0, producing an "unrecognized ROM
            // format" load failure. Callers should already be filtering these
            // out (see toolStrip_SaveROM_Click), but guard here too so this
            // specific failure mode can't recur regardless of caller mistakes.
            if (entity.PositionAddress == 0)
                throw new InvalidOperationException(
                    "Refusing to write a position at ROM address 0 (the header) -- this entity has no real " +
                    "PositionAddress yet, which means it hasn't been persisted to the ROM (SourceAddress == 0).");

            switch (entity.Kind)
            {
                case EntityKind.LevelGate:
                case EntityKind.Character:
                    rom.PatchInt16(entity.PositionAddress, (short)newX);
                    rom.PatchInt16(entity.PositionAddress + 2, (short)newY);
                    break;

                case EntityKind.Object:
                    rom.PatchInt32(entity.PositionAddress, newX);
                    rom.PatchInt32(entity.PositionAddress + 4, newY);
                    break;

                case EntityKind.Trigger:
                    rom.PatchInt16(entity.PositionAddress + 0, (short)newX);
                    rom.PatchInt16(entity.PositionAddress + 2, (short)newY);
                    rom.PatchInt16(entity.PositionAddress + 4, (short)(newX + entity.Width));
                    rom.PatchInt16(entity.PositionAddress + 6, (short)(newY + entity.Height));
                    break;

                case EntityKind.Decoration:
                    // [x0,y0,x1,y1] box -- newX/newY is the top-left (x0,y0), so x1/y1 shift
                    // by the same delta to keep the box's own width/height (and therefore
                    // which tiles/how many tiles get drawn) unchanged.
                    rom.PatchInt32(entity.PositionAddress + 0, newX);
                    rom.PatchInt32(entity.PositionAddress + 4, newY);
                    rom.PatchInt32(entity.PositionAddress + 8, newX + entity.Width);
                    rom.PatchInt32(entity.PositionAddress + 12, newY + entity.Height);
                    break;

                default:
                    throw new NotSupportedException($"Position write-back not implemented for {entity.Kind}.");
            }
        }

        /// <summary>
        /// Persists newly-placed EntityKind.Item entities (SourceAddress == 0,
        /// added via the editor's Items tab) into the ROM. Since MapItem doesn't
        /// store x/y directly (see EntityReader.ReadMapItems), this grows TWO
        /// arrays: the map variation's graphicObjects[] (to supply a position)
        /// and the map's mapItems[] (the item records themselves). New/combined
        /// array contents are appended at the end of the ROM file, and
        /// MapEntry.MapItems/ItemCount + the variation's graphicObjectCount/
        /// graphicObjects fields are repointed at the new location.
        ///
        /// The old array storage is deliberately left in place rather than
        /// zeroed (an earlier version zeroed it; changed after a second report
        /// of a map still failing to load even with a valid tail-byte template
        /// -- zeroing risked stomping adjacent live data if the old array's
        /// byte-count was even slightly miscalculated, since ROM data is
        /// packed tight with no padding between structures). This leaves
        /// harmless dead bytes behind instead.
        ///
        /// Best-effort / experimental: new MapItem records copy handler/
        /// dialogSeq/itemCount/unk2 from an existing item on the same map when
        /// one exists (their exact runtime semantics aren't fully confirmed --
        /// see EntityReader.ReadMapItems). CONFIRMED via IDA (Condition_Evaluate):
        /// a null flags/condition pointer always evaluates true, so new items
        /// (flags left 0) will spawn. If a map has NO existing item to copy a
        /// template from, handler/dialogSeq default to 0 -- untested; if an
        /// item placed on such a map behaves oddly on pickup, that's why.
        ///
        /// IMPORTANT (fixed after a real crash report): graphicObjects[] isn't
        /// only a position source -- CONFIRMED via IDA (sub_8012C94, shared by
        /// both the affine and regular tile-rendering paths, and its callers
        /// sub_8012F40/sub_800733E), every entry in this array is ALSO spawned
        /// as a real visible decoration by Map_InitTilemap every time the map
        /// loads, and bytes 0x10-0x17 of each 24-byte record feed directly into
        /// that decoration's tile-slot/dimension math (byte 0x16 in particular
        /// becomes part of a width computation). The first version of this
        /// method zeroed those bytes as "best effort", which produced values
        /// the game's decoration spawner wasn't expecting and hung the map load
        /// (reported: a specific map -- "the Arena" -- stopped loading after
        /// placing an item there). Fixed by copying those 8 unknown bytes from
        /// graphicObjects[0] (a real, already-working record) instead of
        /// zeroing them, for every new record. This produces a duplicate (not
        /// unique) decoration graphic/tile-slot for placed items, but it's a
        /// KNOWN-GOOD configuration rather than an invented one.
        /// Still an open edge case: a map with ZERO pre-existing graphicObjects
        /// has no real record to copy from. The very first new item on such a
        /// map is safe regardless (index 0 goes through sub_8012FCA/sub_80073EA,
        /// which don't read these tail bytes at all), but a SECOND new item on
        /// CORRECTION (previously claimed index 0 didn't need this template and
        /// was safe regardless -- that was WRONG): sub_8012C94 is called for
        /// EVERY graphicObjects entry, including index 0 (via sub_8012FCA/
        /// sub_80073EA), and unconditionally reads byte offset 0x16 of whatever
        /// record it's given, using it as `result+10 = byte[0x16] - word0`. On a
        /// map with zero pre-existing graphicObjects there is no real record to
        /// copy a template from, so a lone new item there still gets a zeroed
        /// byte 0x16 -- likely underflowing that subtraction into a huge value.
        /// This method now REFUSES to place items on such a map rather than
        /// guess, since testing confirmed a map in this exact state (0 existing
        /// graphicObjects) hung on load after a single new item was added.
        ///
        /// Does not update the live in-memory ROM/ _currentEntities -- reopen
        /// the saved file to keep editing newly-placed items further.
        /// </summary>
        public static int PersistNewItems(ROM rom, MapEntry entry, int mapEntryAddress, int mapOffset, List<Entity> entities)
        {
            var newItems = entities.Where(e => e.Kind == EntityKind.Item && e.SourceAddress == 0).ToList();
            if (newItems.Count == 0)
                return 0;

            const int halfSize = 16; // 32x32 box -- matches the real example this format was verified against

            // --- Grow graphicObjects[] (supplies position; see ReadMapItems) ---
            int graphicObjectCountAddr = mapOffset + 0x24;
            int graphicObjectsPtrAddr = mapOffset + 0x28;

            rom.PushPosition(graphicObjectCountAddr);
            int oldGraphicObjectCount = rom.ReadInt();
            int oldGraphicObjectsPtr = rom.ReadPointer();
            rom.PopPosition();

            if (oldGraphicObjectCount == 0)
            {
                throw new InvalidOperationException(
                    "This map has no pre-existing decorations (graphicObjects) to use as a template for the new " +
                    "item's unknown fields, and a real test confirmed placing an item in this situation hangs the " +
                    "game on load. Placing items is only supported on maps that already have at least one item or " +
                    "decoration on them.");
            }

            byte[] existingGraphicObjects = rom.ReadBytesAt(oldGraphicObjectsPtr, oldGraphicObjectCount * EntityReader.GraphicObjectRecordSize);

            // Bytes 0x10-0x17 of a real record -- used as the template for new
            // records' unknown tail instead of zero (see method doc for why).
            byte[] tailTemplate = existingGraphicObjects.Length >= 24
                ? existingGraphicObjects[0x10..0x18]
                : new byte[8];

            var graphicObjectsBytes = new List<byte>(existingGraphicObjects);
            foreach (var item in newItems)
            {
                graphicObjectsBytes.AddRange(BitConverter.GetBytes(item.X - halfSize));
                graphicObjectsBytes.AddRange(BitConverter.GetBytes(item.Y - halfSize));
                graphicObjectsBytes.AddRange(BitConverter.GetBytes(item.X + halfSize));
                graphicObjectsBytes.AddRange(BitConverter.GetBytes(item.Y + halfSize));
                graphicObjectsBytes.AddRange(tailTemplate);
            }

            int newGraphicObjectsPtr = rom.AppendBytes(graphicObjectsBytes.ToArray());
            // NOTE: the old array storage is intentionally left in place, not
            // zeroed -- see method doc. It becomes harmless dead bytes.

            rom.PatchInt32(graphicObjectCountAddr, oldGraphicObjectCount + newItems.Count);
            rom.PatchInt32(graphicObjectsPtrAddr, 0x08000000 | newGraphicObjectsPtr);

            // --- Find a template item on this map for handler/dialogSeq/itemCount/unk2 ---
            int templateHandler = 0, templateDialogSeq = 0, templateUnk2 = 0;
            int templateItemCountByte = 1;

            if (entry.MapItems != 0 && entry.ItemCount > 0)
            {
                rom.PushPosition(entry.MapItems);
                int firstItemPtr = rom.ReadPointer();
                rom.PopPosition();

                if (firstItemPtr != 0)
                {
                    rom.PushPosition(firstItemPtr + 5);
                    templateItemCountByte = rom.ReadByte();
                    templateUnk2 = rom.ReadShort();
                    templateHandler = rom.ReadInt();
                    templateDialogSeq = rom.ReadInt();
                    rom.PopPosition();
                }
            }

            // --- Append new MapItem records ---
            var newItemAddresses = new List<int>();
            for (int i = 0; i < newItems.Count; i++)
            {
                int graphicObjectIndex = oldGraphicObjectCount + i;

                var record = new List<byte>();
                record.AddRange(BitConverter.GetBytes(0)); // flags/condition = null -> Condition_Evaluate always true
                record.Add((byte)graphicObjectIndex);
                record.Add((byte)templateItemCountByte);
                record.AddRange(BitConverter.GetBytes((short)templateUnk2));
                record.AddRange(BitConverter.GetBytes(templateHandler));
                record.AddRange(BitConverter.GetBytes(templateDialogSeq));

                newItemAddresses.Add(rom.AppendBytes(record.ToArray()));
            }

            // --- Grow mapItems[] (pointer array) ---
            var existingItemPtrs = new List<int>();
            if (entry.MapItems != 0 && entry.ItemCount > 0)
            {
                rom.PushPosition(entry.MapItems);
                for (int i = 0; i < entry.ItemCount; i++)
                    existingItemPtrs.Add(rom.ReadInt()); // raw, already-encoded pointer value
                rom.PopPosition();
            }

            var itemPtrArrayBytes = new List<byte>();
            foreach (int ptr in existingItemPtrs)
                itemPtrArrayBytes.AddRange(BitConverter.GetBytes(ptr));
            foreach (int addr in newItemAddresses)
                itemPtrArrayBytes.AddRange(BitConverter.GetBytes(0x08000000 | addr));

            int newMapItemsPtr = rom.AppendBytes(itemPtrArrayBytes.ToArray());
            // NOTE: old mapItems[] storage intentionally left in place, not
            // zeroed -- see method doc.

            rom.PatchInt32(mapEntryAddress + 0x18, 0x08000000 | newMapItemsPtr);
            rom.PatchByte(mapEntryAddress + 0x05, (byte)Math.Min(255, entry.ItemCount + newItems.Count));

            return newItems.Count;
        }

        // --- EntityKind.Object (mapObjects) -- the array actually used for real,
        // pre-existing pickups (confirmed: a real map's 3 "pill capsules" are
        // stored here, not in mapItems). Unlike mapItems, Map_Object stores its
        // position directly (spawnPoint, 2x int32 at offset 8 -- confirmed via
        // raw disasm, see EntityReader.ReadObjectArray) and is spawned straight
        // from that field by WorldItemObject_Spawn, called directly out of
        // Map_LoadInternal's own loop with NO dependency on graphicObjects,
        // Map_InitTilemap, or the "type 6" lookup system that made mapItems
        // placement unsafe. This is the array new items should actually target.

        /// <summary>
        /// Persists newly-placed EntityKind.Object entities (SourceAddress == 0)
        /// into the ROM by growing mapObjects[], the same array real item
        /// pickups are already stored in. Much simpler/safer than
        /// PersistNewItems: no derived position, no secondary array, no
        /// dependency on the map's variation data. New records copy
        /// onPickup/collectionMsg from an existing object on the same map when
        /// one exists (their exact runtime semantics aren't confirmed, but
        /// they're only expected to be read on player pickup interaction, not
        /// at map load, so an all-zero fallback on an object-less map should at
        /// worst misbehave on pickup rather than crash the map load -- untested
        /// either way since every map tested so far has had a template
        /// available). itemSpawnCondition is left null, which CONFIRMED (via
        /// IDA, Condition_Evaluate) always evaluates true.
        ///
        /// Uses ROM.AllocateFreeSpace (real unused space inside the ROM's
        /// original bounds) rather than growing the file, per the file-size
        /// concerns raised on the mapItems attempt.
        /// </summary>
        public static int PersistNewObjects(ROM rom, MapEntry entry, int mapEntryAddress, List<Entity> entities)
        {
            var newObjects = entities.Where(e => e.Kind == EntityKind.Object && e.SourceAddress == 0).ToList();
            if (newObjects.Count == 0)
                return 0;

            int templateOnPickup = 0, templateCollectionMsg = 0;
            if (entry.MapObjects != 0 && entry.ObjectCount > 0)
            {
                rom.PushPosition(entry.MapObjects);
                int firstPtr = rom.ReadPointer();
                rom.PopPosition();

                if (firstPtr != 0)
                {
                    rom.PushPosition(firstPtr + 0x10);
                    templateOnPickup = rom.ReadInt();
                    templateCollectionMsg = rom.ReadInt();
                    rom.PopPosition();
                }
            }

            var newObjectAddresses = new List<int>();
            foreach (var obj in newObjects)
            {
                var record = new List<byte>();
                record.AddRange(BitConverter.GetBytes(0));         // itemSpawnCondition = null -> Condition_Evaluate always true
                record.AddRange(BitConverter.GetBytes(obj.TypeId)); // itemId (g_ItemsInGame index)
                record.AddRange(BitConverter.GetBytes(obj.X));       // spawnPoint.x
                record.AddRange(BitConverter.GetBytes(obj.Y));       // spawnPoint.y
                // Prefer a per-entity override (chosen in the UI from a real matching
                // pickup elsewhere in the ROM) over the generic "first object on this map" guess.
                int onPickup = obj.OnPickup != 0 || obj.CollectionMsg != 0 ? obj.OnPickup : templateOnPickup;
                int collectionMsg = obj.OnPickup != 0 || obj.CollectionMsg != 0 ? obj.CollectionMsg : templateCollectionMsg;
                record.AddRange(BitConverter.GetBytes(onPickup));
                record.AddRange(BitConverter.GetBytes(collectionMsg));

                int addr = rom.AllocateFreeSpace(record.Count);
                rom.WriteBytesAt(addr, record.ToArray());
                newObjectAddresses.Add(addr);
            }

            var existingPtrs = new List<int>();
            if (entry.MapObjects != 0 && entry.ObjectCount > 0)
            {
                rom.PushPosition(entry.MapObjects);
                for (int i = 0; i < entry.ObjectCount; i++)
                    existingPtrs.Add(rom.ReadInt()); // raw, already-encoded pointer value
                rom.PopPosition();
            }

            var ptrArrayBytes = new List<byte>();
            foreach (int ptr in existingPtrs)
                ptrArrayBytes.AddRange(BitConverter.GetBytes(ptr));
            foreach (int addr in newObjectAddresses)
                ptrArrayBytes.AddRange(BitConverter.GetBytes(0x08000000 | addr));

            int newPtrArrayAddr = rom.AllocateFreeSpace(ptrArrayBytes.Count);
            rom.WriteBytesAt(newPtrArrayAddr, ptrArrayBytes.ToArray());
            // Old mapObjects[] storage intentionally left in place, not zeroed.

            rom.PatchInt32(mapEntryAddress + 0x1C, 0x08000000 | newPtrArrayAddr); // mapObjects
            rom.PatchByte(mapEntryAddress + 0x06, (byte)Math.Min(255, entry.ObjectCount + newObjects.Count)); // objectCount

            return newObjects.Count;
        }

        /// <summary>
        /// Persists newly-placed EntityKind.Character entities (SourceAddress == 0) into the
        /// ROM by growing mapScripts[], the pointer array MapScript_CreateCharacter-handled
        /// records already live in (see EntityReader.ReadMapScripts). Each 32-byte record is
        /// only CONFIRMED for its first 16 bytes (handler, flags/condition, x, y, spriteId) --
        /// bytes +0x10..+0x1F are unconfirmed, the exact same situation PersistNewItems hit
        /// with graphicObjects' tail bytes, which a real crash report traced to zeroing an
        /// unconfirmed field the game's spawn code still reads unconditionally. Applying the
        /// same fix here pre-emptively rather than waiting for the same crash to recur: copy
        /// those 16 bytes from an existing CharacterSpawnHandlerOffset record on the SAME map
        /// (a real, already-working record) instead of zeroing them, and REFUSE to place a
        /// character on a map with no such existing record to copy from, rather than guess.
        /// Uses ROM.AllocateFreeSpace, matching PersistNewObjects.
        /// </summary>
        public static int PersistNewCharacters(ROM rom, MapEntry entry, int mapEntryAddress, List<Entity> entities)
        {
            var newCharacters = entities.Where(e => e.Kind == EntityKind.Character && e.SourceAddress == 0).ToList();
            if (newCharacters.Count == 0)
                return 0;

            byte[]? tailTemplate = null;
            var existingRecordPtrs = new List<int>();

            if (entry.MapScripts != 0 && entry.ScriptCount > 0)
            {
                rom.PushPosition(entry.MapScripts);
                for (int i = 0; i < entry.ScriptCount; i++)
                    existingRecordPtrs.Add(rom.ReadInt()); // raw, already-encoded pointer value
                rom.PopPosition();

                foreach (int rawPtr in existingRecordPtrs)
                {
                    int recordAddr = rawPtr & 0x00FFFFFF;
                    if (recordAddr == 0) continue;

                    rom.PushPosition(recordAddr);
                    int handler = rom.ReadPointer();
                    if (handler == EntityReader.CharacterSpawnHandlerOffset)
                    {
                        tailTemplate = rom.ReadBytesAt(recordAddr + 0x10, EntityReader.MapScriptRecordSize - 0x10);
                        rom.PopPosition();
                        break;
                    }
                    rom.PopPosition();
                }
            }

            if (tailTemplate == null)
            {
                throw new InvalidOperationException(
                    "This map has no pre-existing character spawn (MapScript_CreateCharacter) record to use as a " +
                    "template for the new character's unconfirmed tail bytes, and PersistNewItems already hit a real " +
                    "crash from zeroing an analogous unconfirmed field elsewhere. Placing NPCs is only supported on " +
                    "maps that already have at least one character.");
            }

            var newRecordAddresses = new List<int>();
            foreach (var character in newCharacters)
            {
                var record = new List<byte>();
                record.AddRange(BitConverter.GetBytes(0x08000000 | EntityReader.CharacterSpawnHandlerOffset)); // handler
                record.AddRange(BitConverter.GetBytes(0));               // flags/condition = null -> Condition_Evaluate always true
                record.AddRange(BitConverter.GetBytes((short)character.X));
                record.AddRange(BitConverter.GetBytes((short)character.Y));
                record.AddRange(BitConverter.GetBytes(character.TypeId)); // spriteId
                record.AddRange(tailTemplate);

                int addr = rom.AllocateFreeSpace(record.Count);
                rom.WriteBytesAt(addr, record.ToArray());
                newRecordAddresses.Add(addr);
            }

            var ptrArrayBytes = new List<byte>();
            foreach (int ptr in existingRecordPtrs)
                ptrArrayBytes.AddRange(BitConverter.GetBytes(ptr));
            foreach (int addr in newRecordAddresses)
                ptrArrayBytes.AddRange(BitConverter.GetBytes(0x08000000 | addr));

            int newPtrArrayAddr = rom.AllocateFreeSpace(ptrArrayBytes.Count);
            rom.WriteBytesAt(newPtrArrayAddr, ptrArrayBytes.ToArray());
            // Old mapScripts[] storage intentionally left in place, not zeroed.

            rom.PatchInt32(mapEntryAddress + 0x14, 0x08000000 | newPtrArrayAddr); // mapScripts
            rom.PatchByte(mapEntryAddress + 0x04, (byte)Math.Min(255, entry.ScriptCount + newCharacters.Count)); // scriptCount

            return newCharacters.Count;
        }
    }

    /// <summary>
    /// Resolves and renders item icons for EntityKind.Object markers, using the
    /// same g_ItemsInGame table (confirmed via IDA: WorldItemObject_Spawn reads
    /// itemId directly into this table as `itemsBase + itemId*0x38 + 8`) that
    /// the standalone DBZKit Items.cs tool already extracts sprites from.
    /// </summary>
    public static class ItemIconReader
    {
        private const int ItemTableOffset = 0x6ADE24; // g_ItemsInGame, masked to ROM file offset
        private const int ItemEntrySize = 0x38;
        private static readonly int[] SpriteFieldOffsets = [0x14, 0x24, 0x28, 0x2C];

        private static readonly Dictionary<(Game game, int itemId), Bitmap?> cache = [];

        /// <summary>
        /// Reads the OBJ (sprite) palette. Per GBA hardware convention (and
        /// matching this codebase's own BG palette handling), palette index 0
        /// is always transparent -- applied here the same way for sprites.
        /// </summary>
        public static Color[] ReadOBJPalette(ROM rom, Game game)
        {
            rom.PushPosition(game.OBJPaletteOffset);
            var colors = new Color[256];

            for (int i = 0; i < colors.Length; i++)
            {
                int c = rom.ReadShort();
                int r = ((c & 0x1F) * 0x21) >> 2;
                int g = (((c >> 5) & 0x1F) * 0x21) >> 2;
                int b = (((c >> 10) & 0x1F) * 0x21) >> 2;
                colors[i] = Color.FromArgb(0xFF, r, g, b);
            }

            colors[0] = Color.FromArgb(0, 0, 0, 0); // index 0 = transparent
            rom.PopPosition();
            return colors;
        }

        /// <summary>
        /// Enumerates item ids that look like real entries in g_ItemsInGame
        /// (nonzero width/height within a plausible sprite-size range), up to
        /// maxId or until <paramref name="maxConsecutiveMisses"/> invalid
        /// entries in a row are seen (signals we've run past the real table
        /// into unrelated ROM data). There's no known constant for the true
        /// item count, so this is a heuristic scan, not an exact bound.
        /// </summary>
        public static IEnumerable<int> EnumerateValidItemIds(ROM rom, int maxId = 256, int maxConsecutiveMisses = 8)
        {
            int misses = 0;

            for (int itemId = 0; itemId < maxId; itemId++)
            {
                int entryBase = ItemTableOffset + (itemId * ItemEntrySize);
                bool valid = false;

                if (entryBase + ItemEntrySize <= rom.Length)
                {
                    rom.PushPosition(entryBase + 0x08);
                    int width = rom.ReadInt();
                    int height = rom.ReadInt();
                    rom.PopPosition();

                    valid = width > 0 && width <= 64 && height > 0 && height <= 64;
                }

                if (valid)
                {
                    misses = 0;
                    yield return itemId;
                }
                else if (++misses >= maxConsecutiveMisses)
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Returns the first available icon sprite for the given item id, or
        /// null if the item has no sprite / itemId is out of range. Results
        /// are cached per (game, itemId).
        /// </summary>
        public static Bitmap? GetIcon(ROM rom, Game game, int itemId)
        {
            var key = (game, itemId);
            if (cache.TryGetValue(key, out var cached))
                return cached;

            Bitmap? icon = null;

            if (itemId >= 0)
            {
                int entryBase = ItemTableOffset + (itemId * ItemEntrySize);

                if (entryBase + ItemEntrySize <= rom.Length)
                {
                    var palette = ReadOBJPalette(rom, game);

                    foreach (int spriteOff in SpriteFieldOffsets)
                    {
                        rom.PushPosition(entryBase + spriteOff);
                        int spritePtr = rom.ReadPointer();
                        rom.PopPosition();

                        if (spritePtr == 0)
                            continue;

                        try
                        {
                            byte[] data = JCALG1.Decompress(rom, spritePtr);
                            int size = (int)Math.Sqrt(data.Length);

                            if (size > 0 && size * size == data.Length)
                            {
                                icon = RenderIndexed(data, size, size, palette);
                                break;
                            }
                        }
                        catch
                        {
                            // Corrupt/unsupported sprite data -- fall through and try the next slot.
                        }
                    }
                }
            }

            cache[key] = icon;
            return icon;
        }

        internal static Bitmap RenderIndexed(byte[] data, int width, int height, Color[] palette)
        {
            var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, width, height);
            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
            int stride = bmpData.Stride;
            byte[] buffer = new byte[stride * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var c = palette[data[y * width + x]];
                    int offset = (y * stride) + (x * 4);
                    buffer[offset + 0] = c.B;
                    buffer[offset + 1] = c.G;
                    buffer[offset + 2] = c.R;
                    buffer[offset + 3] = c.A;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, bmpData.Scan0, buffer.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        public static void ResetCache(Game game)
        {
            foreach (var key in cache.Keys.Where(k => k.game == game).ToList())
                cache.Remove(key);
        }
    }

    /// <summary>
    /// Renders map tile/layer data to bitmaps. Ported from the original
    /// LOGExtractor/MapViewer MapRenderer, but with all ROM offsets sourced
    /// from a <see cref="Game"/> config instead of hardcoded constants, so
    /// it works across any game definition rather than just one ROM.
    /// </summary>
    public static class MapRenderer
    {
        private const int tile_size = 8;
        private const int chunk_size_tiles = 32;
        private const int chunk_size_pixels = tile_size * chunk_size_tiles;
        private const int tile_per_image = 16 * 16;

        // MapEntry struct layout (0x38 bytes):
        //   +0x00  u8  zone
        //   +0x01  u8  area
        //   +0x02  u8  variation       (runtime flag, NOT an array index)
        //   +0x03  u8  triggerCount
        //   +0x04  u8  scriptCount
        //   +0x05  u8  itemCount
        //   +0x06  u8  objectCount
        //   +0x07  u8  npcCount
        //   +0x08  u32 flags
        //   +0x0C  u32 mapNameIndex
        //   +0x10  ptr mapTriggers
        //   +0x14  ptr mapScripts
        //   +0x18  ptr mapItems
        //   +0x1C  ptr mapObjects
        //   +0x20  ptr npcArray
        //   +0x24  u32 musicId
        //   +0x28  ptr variationScript
        //   +0x2C  ptr variationArray   <-- Map_VariationEntry*
        //   +0x30  ptr entryScript
        //   +0x34  ptr exitScript
        //
        // Map_VariationEntry struct layout (0x50 bytes):
        //   +0x00  ptr MapGraphicObjects  <-- this is the map struct offset

        // Per-game caches. Keyed by Game so switching ROMs doesn't serve stale tiles.
        private static readonly Dictionary<Game, Dictionary<int, Bitmap>> tilesetCaches = [];
        private static readonly Dictionary<Game, Dictionary<int, (Bitmap bitmap, int priority, HashSet<Range> usedTilesets, bool unsupported)>> layerCaches = [];

        public static void ResetCache(Game game)
        {
            tilesetCaches.Remove(game);
            layerCaches.Remove(game);
        }

        public static (Bitmap bitmap, Dictionary<Range, Bitmap> tilesets, HashSet<Range> usedTilesets, bool hasUnsupportedLayer, bool[,] collisionGrid) RenderMap(ROM rom, Game game, int mapOffset, MapRenderOptions options)
        {
            var tilesets = DrawTileset(rom, game, mapOffset);
            var usedTilesets = new HashSet<Range>();

            rom.PushPosition(mapOffset + 0x14);
            var layer0 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            var layer1 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            var layer2 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            var layer3 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            rom.PopPosition();

            // FIXED 2026-09 (found by diffing against the original LOGExtractor
            // MapViewer.MainForm, which this compositing was ported from): the original
            // NEVER sizes or crops the map bitmap using the declared width/height
            // (mapOffset+0x8) at all -- it draws every layer's own natural, uncropped
            // bitmap directly at (0,0) onto a canvas, full stop. Declared width/height is
            // only ever used there to draw a decorative boundary rectangle (the reachable
            // camera/viewport bounds), never to size or clip the actual tile content. A
            // prior version of this port instead built a composite sized to
            // declared+240/+160 and cropped every oversized layer into it, which is what
            // produced the reported black bands / misaligned terrain -- there is no crop
            // to get right here; the fix is to not crop at all. The composite is sized to
            // whatever the largest layer actually needs.
            int width = 1;
            int height = 1;
            foreach (var l in new[] { layer0, layer1, layer2, layer3 })
            {
                width = Math.Max(width, l.bitmap.Width);
                height = Math.Max(height, l.bitmap.Height);
            }

            var composite = new Bitmap(width, height);
            using var g = Graphics.FromImage(composite);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            // CORRECTED 2026-09 (RE-VERIFIED via IDA, replaces the previous "BUG FOUND
            // 2026-09-XX" fix below it, which was itself wrong): the per-layer priority
            // this method used to read from DrawLayer (a byte inside each layer's OWN
            // chunk-header blob, at +0x12) is NOT where real GBA priority comes from.
            // CONFIRMED via raw disasm of Map_InitRenderer (0x8006152) and
            // MapRenderer_ApplyDisplayRegisters (0x8005F1A, the function that actually
            // writes the hardware BG0CNT-BG3CNT registers): priority is stored as 4
            // consecutive bytes at Map_VariationEntry.bgPriorityPacked (+0x04), ONE BYTE
            // PER BG INDEX (byte0=BG0, byte1=BG1, byte2=BG2, byte3=BG3) --
            // `*(a1+120)=LOBYTE(bgPriorityPacked)` etc in Map_InitRenderer, then
            // `BG0CNT = BGCNT_BaseValues[...] + *(a1+120)` etc in
            // MapRenderer_ApplyDisplayRegisters, and the low bits of a real BGCNT register
            // ARE hardware priority. This is a MAP-level value indexed by BG number, not a
            // per-layer-blob field at all. Empirically confirmed wrong before this fix:
            // Zone4/Area1's real bgPriorityPacked bytes are 03 01 03 03 (BG1 drawn on top of
            // the other three), but the old per-layer-header read reported priority=0 for
            // ALL FOUR layers on that map (a uniformly wrong value that collapses to pure
            // BG-index tie-break ordering) -- a real, provable bug, not just an unconfirmed
            // assumption. This was very likely a real contributor to the reported "missing
            // rocks / wrong layer showing" symptom (whichever layer should have been on top
            // per hardware priority could easily end up buried under the wrong one).
            // CONFIRMED via IDA 2026-09 (MapRenderer_ApplyDisplayRegisters, formerly sub_8005F1A,
            // called from Map_InitRenderer): BLDCNT/BLDALPHA -- the GBA alpha-blend registers --
            // are sourced directly from Map_VariationEntry.bgBlendConfig (mapOffset+0x0), NOT
            // computed at runtime. Low 16 bits = BLDCNT (bits0-5 = target1 layer mask [BG0..BG3,
            // OBJ, backdrop], bits6-7 = blend mode [0=none,1=alpha,2=inc,3=dec], bits8-13 =
            // target2 layer mask). High 16 bits = BLDALPHA (bits0-4 = EVA, bits8-12 = EVB,
            // packed one byte per nibble the same way -- low byte of the high word = EVA byte,
            // high byte of the high word = EVB byte). This is the "light source" alpha-blend
            // effect some maps use that previously wasn't read at all, so a blended BG layer
            // just drew fully opaque (the reported gray-smudge symptom) instead of translucent.
            rom.PushPosition(mapOffset + 0x0);
            int packedBlend = rom.ReadInt();
            rom.PopPosition();

            int bldcnt = packedBlend & 0xFFFF;
            int blendMode = (bldcnt >> 6) & 0x3;
            int blendTarget1Mask = bldcnt & 0x3F; // bit index == BG index for bits 0-3
            int evaRaw = (packedBlend >> 16) & 0x1F; // EVA -- weight applied to the target1/top layer's own color
            int evbRaw = (packedBlend >> 24) & 0x1F; // EVB -- weight applied to whatever's already composited beneath it
            float eva = Math.Min(16, evaRaw) / 16f;
            float evb = Math.Min(16, evbRaw) / 16f;

            rom.PushPosition(mapOffset + 0x4);
            int packedPriority = rom.ReadInt();
            rom.PopPosition();
            int[] realPriority =
            [
                packedPriority & 0xFF,
                (packedPriority >> 8) & 0xFF,
                (packedPriority >> 16) & 0xFF,
                (packedPriority >> 24) & 0xFF,
            ];

            var layers = new[]
            {
                (bitmap: layer0.bitmap, priority: realPriority[0], index: 0, show: options.ShowBG0),
                (bitmap: layer1.bitmap, priority: realPriority[1], index: 1, show: options.ShowBG1),
                (bitmap: layer2.bitmap, priority: realPriority[2], index: 2, show: options.ShowBG2),
                (bitmap: layer3.bitmap, priority: realPriority[3], index: 3, show: options.ShowBG3),
            };

            // Paint back-to-front: highest priority value (lowest on-screen) first,
            // ties broken by highest BG index first, so the lowest priority / lowest
            // BG index ends up painted last (on top) -- matching GBA hardware order.
            // Every layer is drawn at its own natural, uncropped size at (0,0) -- see the
            // composite-sizing comment above for why there's no per-layer crop/anchor here.
            foreach (var layer in layers.OrderByDescending(l => l.priority).ThenByDescending(l => l.index))
            {
                if (!layer.show) continue;
                int drawY = 0;

                // Real hardware blends this layer (as the alpha-blend "top"/target1 layer)
                // against whatever's already on screen beneath it as dst' = src*EVA +
                // dst*EVB -- two INDEPENDENT coefficients, not a complementary pair. An
                // earlier version of this approximated it with GDI+ ColorMatrix alpha
                // scaling, which implicitly assumes EVB == 1-EVA; that overweights the
                // source layer's own (often near-black, e.g. a light-source gradient's
                // unlit fringe) color and underweights the destination whenever the ROM's
                // real EVB is larger than 1-EVA, producing exactly the reported
                // "overly black at the edges" look. DrawBlendedLayer below does the real
                // dual-coefficient math per-pixel instead.
                bool isBlendedTop = blendMode == 1 && ((blendTarget1Mask >> layer.index) & 1) != 0;
                if (isBlendedTop)
                {
                    DrawBlendedLayer(layer.bitmap, composite, drawY, eva, evb);
                }
                else
                {
                    g.DrawImage(layer.bitmap, 0, drawY);
                }
            }

            // MOVED 2026-09: graphicObjects decorations (trees, rocks, flight pads, etc)
            // used to be baked directly into this static composite bitmap here. They're
            // now read as real, selectable/draggable Entity records instead (see
            // EntityReader.ReadGraphicObjects + RenderGraphicObjectSprites) and drawn
            // per-frame by the editor's own entity-overlay code, the same way item
            // pickups already work -- baked-in pixels can't be dragged or written back.

            // CONFIRMED (2026-09, by the user checking in-game) that flat gray "void
            // filler" patches on some maps (e.g. Zone2/Area1) are NOT a rendering bug --
            // they're genuinely in the ROM and visible in actual gameplay too. They're
            // real, but they make hand-editing harder to read at a glance, so tint them
            // distinctly here (editor-only cosmetic overlay, doesn't touch what's
            // actually rendered) rather than leave them looking like broken output.
            TintVoidFillerTiles(composite);

            // A map using the unrecognized 0x4FCD layer format (see DrawLayer) would
            // otherwise just look like an ordinary, complete map with a blank/empty
            // background where that layer's content should be -- misleading for an
            // editor. Paint an unmissable hazard-stripe band instead, so it's obvious
            // this map isn't fully rendered rather than genuinely being that empty.
            bool hasUnsupportedLayer = layer0.unsupported || layer1.unsupported || layer2.unsupported || layer3.unsupported;
            if (hasUnsupportedLayer)
            {
                DrawUnsupportedLayerWarning(g, composite.Width, composite.Height);
            }

            // CONFIRMED via IDA 2026-09 (raw disasm of Map_InitRenderer, 0x8006152): the buffer
            // Collision_CheckRect (0x8005D6C) reads from at runtime (g_MapRenderer+0x6C, the
            // global previously auto-named dword_30010FC by IDA -- it's really a field access
            // through off_83ED214, not a standalone global) is filled by
            // `Resource_LoadOrDecompress(src: Map_VariationEntry+0x34, dst: g_MapRenderer+0x6C)`.
            // That's the SAME resource-header format (u32 format flag, u32 size, then
            // stored-or-JCALG1 payload) already handled by JCALG1.Decompress, and the existing
            // in-file comment on Map_InitRenderer calling this field "tilesetGraphicsData" was
            // ALREADY marked unconfirmed there -- this is the correction: +0x34 is the
            // collision bitmap, not tileset graphics. The consumer's own indexing
            // (collisionMap[8*tileY + (tileX>>5)] & (1<<(tileX&0x1F))) fits an 8KB buffer
            // exactly as a 256x256-tile (2048x2048px) 1-bit-per-tile grid, which is also the
            // exact size Map_InitRenderer ArenaAllocs for it (0x2000 bytes = 8192 = 256*256/8).
            var collisionGrid = ReadCollisionMap(rom, mapOffset);
            if (options.ShowCollision)
            {
                DrawCollisionOverlay(collisionGrid, composite);
            }

            return (composite, tilesets, usedTilesets, hasUnsupportedLayer, collisionGrid);
        }

        /// <summary>
        /// Composites a real GBA alpha-blend "target1" layer onto <paramref name="composite"/>
        /// as dst' = src*eva + dst*evb per pixel, matching hardware exactly (two independent
        /// coefficients, not a complementary alpha pair) -- see RenderMap's isBlendedTop
        /// comment for why a GDI+ ColorMatrix approximation isn't good enough. Pixels where
        /// the source is fully transparent (the GBA's single transparent palette index, tile
        /// ID 0 in DrawChunk) are left untouched -- on real hardware a transparent BG pixel
        /// doesn't participate in blending at all, the next visible layer underneath becomes
        /// the effective blend surface instead, which this only approximates by leaving
        /// whatever's already composited beneath it as-is.
        /// </summary>
        private static void DrawBlendedLayer(Bitmap layer, Bitmap composite, int drawY, float eva, float evb)
        {
            var srcRect = new Rectangle(0, 0, layer.Width, layer.Height);
            var srcData = layer.LockBits(srcRect, System.Drawing.Imaging.ImageLockMode.ReadOnly, layer.PixelFormat);
            int srcStride = srcData.Stride;
            byte[] srcBuffer = new byte[srcStride * layer.Height];
            System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcBuffer, 0, srcBuffer.Length);
            layer.UnlockBits(srcData);

            var dstRect = new Rectangle(0, 0, composite.Width, composite.Height);
            var dstData = composite.LockBits(dstRect, System.Drawing.Imaging.ImageLockMode.ReadWrite, composite.PixelFormat);
            int dstStride = dstData.Stride;
            byte[] dstBuffer = new byte[dstStride * composite.Height];
            System.Runtime.InteropServices.Marshal.Copy(dstData.Scan0, dstBuffer, 0, dstBuffer.Length);

            for (int y = 0; y < layer.Height; y++)
            {
                int dy = y + drawY;
                if (dy < 0 || dy >= composite.Height) continue;

                for (int x = 0; x < layer.Width && x < composite.Width; x++)
                {
                    int so = (y * srcStride) + (x * 4);
                    byte srcAlpha = srcBuffer[so + 3];
                    if (srcAlpha == 0) continue; // transparent tile pixel -- not a blend surface

                    int doff = (dy * dstStride) + (x * 4);
                    dstBuffer[doff + 0] = (byte)Math.Clamp((srcBuffer[so + 0] * eva) + (dstBuffer[doff + 0] * evb), 0, 255);
                    dstBuffer[doff + 1] = (byte)Math.Clamp((srcBuffer[so + 1] * eva) + (dstBuffer[doff + 1] * evb), 0, 255);
                    dstBuffer[doff + 2] = (byte)Math.Clamp((srcBuffer[so + 2] * eva) + (dstBuffer[doff + 2] * evb), 0, 255);
                    dstBuffer[doff + 3] = 255;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(dstBuffer, 0, dstData.Scan0, dstBuffer.Length);
            composite.UnlockBits(dstData);
        }

        // CONFIRMED via IDA 2026-09 (Map_InitRenderer @0x8006152: `LDR R0,[R5,#0x34]` / `BL
        // Resource_LoadOrDecompress` into `g_MapRenderer+0x6C`; Collision_CheckRect @0x8005D6C:
        // `collisionMap[8*tileY + (tileX>>5)] & (1 << (tileX & 0x1F))`). One bit per 8x8 tile,
        // 32 tiles packed per 32-bit word, 8 words per tile-row -- supports up to 256x256 tiles
        // (2048x2048px). A set bit means BLOCKED/solid (Collision_CheckRect treats a 0 bit run
        // across the tested rect as ALLOWED and returns BLOCKED as soon as it finds a set bit
        // in range). Returns a full 256x256 grid regardless of the map's real (usually smaller)
        // pixel size -- callers should only look at [0..width/8, 0..height/8].
        public static bool[,] ReadCollisionMap(ROM rom, int mapOffset)
        {
            const int tilesPerSide = 256;
            var grid = new bool[tilesPerSide, tilesPerSide];

            rom.PushPosition(mapOffset + 0x34);
            int collisionPtr = rom.ReadPointer();
            rom.PopPosition();

            if (collisionPtr == 0)
                return grid;

            byte[] data;
            try
            {
                data = JCALG1.Decompress(rom, collisionPtr);
            }
            catch
            {
                return grid; // corrupt/unsupported for this map -- report "all clear" rather than guess
            }

            for (int tileY = 0; tileY < tilesPerSide; tileY++)
            {
                for (int wordX = 0; wordX < 8; wordX++)
                {
                    int byteOffset = ((8 * tileY) + wordX) * 4;
                    if (byteOffset + 4 > data.Length)
                        continue;

                    uint word = BitConverter.ToUInt32(data, byteOffset);
                    if (word == 0) continue;

                    for (int bit = 0; bit < 32; bit++)
                    {
                        if ((word & (1u << bit)) != 0)
                            grid[(wordX * 32) + bit, tileY] = true;
                    }
                }
            }

            return grid;
        }

        /// <summary>
        /// Editor-only overlay: tints solid/blocked tiles from <see cref="ReadCollisionMap"/>
        /// semi-transparent red directly onto an already-rendered map bitmap (or a cropped
        /// viewport of one -- pass <paramref name="originTileX"/>/<paramref name="originTileY"/>
        /// to offset into the grid for a crop that doesn't start at tile 0,0).
        /// </summary>
        public static void DrawCollisionOverlay(bool[,] collisionGrid, Bitmap target, int originTileX = 0, int originTileY = 0, Color? color = null)
        {
            var tint = color ?? Color.FromArgb(110, 220, 30, 30);
            int gridW = collisionGrid.GetLength(0);
            int gridH = collisionGrid.GetLength(1);

            int colsVisible = (target.Width / tile_size) + 1;
            int rowsVisible = (target.Height / tile_size) + 1;

            using var g = Graphics.FromImage(target);
            using var brush = new SolidBrush(tint);

            for (int ty = 0; ty < rowsVisible; ty++)
            {
                int gy = originTileY + ty;
                if (gy < 0 || gy >= gridH) continue;

                for (int tx = 0; tx < colsVisible; tx++)
                {
                    int gx = originTileX + tx;
                    if (gx < 0 || gx >= gridW) continue;

                    if (collisionGrid[gx, gy])
                        g.FillRectangle(brush, tx * tile_size, ty * tile_size, tile_size, tile_size);
                }
            }
        }

        /// <summary>
        /// Renders one Bitmap per OAM sprite decoration (trees, rocks, flight pads, etc)
        /// that Map_InitTilemap spawns from Map_VariationEntry.graphicObjects[] -- a layer
        /// entirely separate from the BG tilemap DrawLayer produces. Keyed by each
        /// record's ROM address, matching EntityReader.ReadGraphicObjects' Entity.SourceAddress,
        /// so the editor can draw/drag them as real entities instead of baking them into
        /// the static map bitmap (which is what this used to do, before decorations became
        /// selectable -- see MapRenderer.RenderMap's "MOVED 2026-09" comment).
        ///
        /// EXPERIMENTAL / best-effort: the overall pipeline (source resource at +0x2C,
        /// decompressed the same way as tilesetGraphicsData, uploaded to OBJ VRAM, records
        /// select a tile-offset into it) is CONFIRMED via raw disasm of
        /// sub_80073EA/sub_800733E/sub_80072C4.
        ///
        /// Bit depth is 8bpp/1-byte-per-pixel (matches ItemIconReader's confirmed format for
        /// this game) -- CONFIRMED empirically 2026-09: an actual test run of this exact
        /// method against Z1A1's ROM data rendered a clean, coherent rock at 8bpp, and pure
        /// noise at 4bpp (the 32-byte VRAM DMA stride theory tried twice before this was
        /// wrong -- that granularity is just the GBA's fixed OBJ tile-*slot* size regardless
        /// of bit depth, not a format indicator).
        ///
        /// frameOffset unit CONFIRMED 2026-09 via full disasm of sub_80067AC (the VRAM
        /// tile-slot allocator reached through the tileBase lookup both graphicObjects
        /// constructors perform): every return path is `2 * slotIndex` -- it allocates in
        /// 64-byte (one 8bpp tile) granularity internally but reports the result in native
        /// OAM tile-index units, which are 32-byte granularity (an 8bpp tile spans two such
        /// units). Confirmed empirically too: Z1A1 has 3 identical rock decorations, one at
        /// frameOffset=0 (renders fine either way) and two at frameOffset=32, which is
        /// already out of bounds for the map's actual 32-tile (2048-byte) sprite sheet under
        /// a direct/undivided read -- exactly the reported "rocks invisible" bug. Dividing
        /// frameOffset by 2 before using it as a local (64-byte-tile) index puts it at tile
        /// 16, well within range, and renders the identical clean rock as frameOffset=0.
        ///
        /// Tile packing within a multi-tile sprite is still assumed row-major from that
        /// local tile base (`localBase + ty*tilesWide + tx`) -- confirmed correct for a 4x4
        /// single-row-of-itself block (Z1A1's rocks), but not independently re-verified for
        /// a wider, multi-row sprite (e.g. save points) now that the /2 fix changes which
        /// tiles such a sprite actually reads -- worth re-checking those next.
        /// </summary>
        public static Dictionary<int, Bitmap> RenderGraphicObjectSprites(ROM rom, Game game, int mapOffset)
        {
            var sprites = new Dictionary<int, Bitmap>();

            rom.PushPosition(mapOffset + 0x24);
            int graphicObjectCount = rom.ReadInt();
            int graphicObjectsBase = rom.ReadPointer();
            rom.PopPosition();

            if (graphicObjectCount <= 0 || graphicObjectsBase == 0) return sprites;

            // CONFIRMED via raw disasm (sub_80072C4): Resource_LoadOrDecompress reads
            // directly from Map_VariationEntry+0x2C, the same decompression pipeline
            // DrawTileset already uses for tilesetGraphicsData/the static tileset blob.
            rom.PushPosition(mapOffset + 0x2C);
            int sheetPtr = rom.ReadPointer();
            rom.PopPosition();
            if (sheetPtr == 0) return sprites;

            byte[] sheetData;
            try
            {
                sheetData = JCALG1.Decompress(rom, sheetPtr);
            }
            catch
            {
                return sprites; // unsupported/corrupt for this map -- skip decorations rather than guess
            }

            var palette = ItemIconReader.ReadOBJPalette(rom, game);
            const int bytesPerTile = 64; // see method doc -- 8bpp; the 32-byte DMA stride theory was wrong (see doc)

            for (int i = 0; i < graphicObjectCount; i++)
            {
                int recordBase = graphicObjectsBase + (i * EntityReader.GraphicObjectRecordSize);
                rom.PushPosition(recordBase);
                int x0 = rom.ReadInt();
                int y0 = rom.ReadInt();
                int x1 = rom.ReadInt();
                int y1 = rom.ReadInt();
                rom.Skip(4); // +0x10..0x13, unconfirmed -- not needed for position/frame
                int frameOffset = rom.ReadShort(); // +0x14, CONFIRMED u16 (raw disasm, sub_800733E: LDRH [record+0x14])
                rom.PopPosition();

                int width = x1 - x0;
                int height = y1 - y0;
                if (width <= 0 || height <= 0 || width > 256 || height > 256) continue; // sanity guard, not a confirmed bound

                int tilesWide = Math.Max(1, width / tile_size);
                int tilesTall = Math.Max(1, height / tile_size);

                // /2: frameOffset is in native OAM tile-index units (32-byte granularity,
                // CONFIRMED via sub_80067AC's `2 * slotIndex` return), but this buffer is
                // indexed in 64-byte (8bpp) tile units -- see method doc.
                int localTileBase = frameOffset / 2;

                var sprite = new Bitmap(tilesWide * tile_size, tilesTall * tile_size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(sprite))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;

                    for (int ty = 0; ty < tilesTall; ty++)
                    {
                        for (int tx = 0; tx < tilesWide; tx++)
                        {
                            int tileIndex = localTileBase + (ty * tilesWide) + tx;
                            int srcOffset = tileIndex * bytesPerTile;
                            if (srcOffset < 0 || srcOffset + bytesPerTile > sheetData.Length) continue;

                            using var tileBmp = ItemIconReader.RenderIndexed(sheetData.AsSpan(srcOffset, bytesPerTile).ToArray(), tile_size, tile_size, palette);
                            g.DrawImage(tileBmp, tx * tile_size, ty * tile_size);
                        }
                    }
                }

                sprites[recordBase] = sprite;
            }

            return sprites;
        }

        private static void DrawUnsupportedLayerWarning(Graphics g, int width, int height)
        {
            const int stripeHeight = 24;
            using var stripeBrush = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.Magenta, Color.FromArgb(180, 0, 0, 0));
            g.FillRectangle(stripeBrush, 0, 0, width, stripeHeight);
            g.FillRectangle(stripeBrush, 0, Math.Max(0, height - stripeHeight), width, stripeHeight);

            using var font = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold);
            const string text = "UNSUPPORTED LAYER FORMAT (0x4FCD) -- this map is NOT fully rendered";
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString(text, font, textBrush, 2, 2);
        }

        /// <summary>
        /// Editor-only cosmetic pass: finds 8x8 tile cells that are a single, perfectly
        /// flat color (real terrain/decoration tiles always have pixel-level texture;
        /// a completely uniform tile is a strong signal of "void filler" rather than
        /// designed content) and, when that exact color repeats often enough across the
        /// map to look like a genuine filler pattern rather than a coincidental flat
        /// design element, checkerboards it with a muted tint. Does not alter what's
        /// actually decoded/rendered -- purely a readability aid layered on top.
        /// </summary>
        private static void TintVoidFillerTiles(Bitmap composite)
        {
            const int minFlatTilesToCountAsFiller = 8;
            var tintColor = Color.FromArgb(140, 90, 60, 160); // muted violet, distinct from any real terrain palette

            int cols = composite.Width / tile_size;
            int rows = composite.Height / tile_size;
            if (cols == 0 || rows == 0) return;

            var rect = new Rectangle(0, 0, composite.Width, composite.Height);
            var data = composite.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, composite.PixelFormat);
            int stride = data.Stride;
            int byteCount = stride * composite.Height;
            byte[] buffer = new byte[byteCount];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, byteCount);

            int PixelOffset(int px, int py) => (py * stride) + (px * 4);

            var flatTileColorCounts = new Dictionary<int, int>();
            var flatTileCells = new List<(int col, int row, int argb)>();

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int baseX = col * tile_size;
                    int baseY = row * tile_size;
                    int first = PixelOffset(baseX, baseY);
                    byte a0 = buffer[first + 3];
                    if (a0 == 0) continue; // untouched/transparent cell, not a filler tile

                    byte b0 = buffer[first + 0], g0 = buffer[first + 1], r0 = buffer[first + 2];
                    bool flat = true;

                    for (int y = 0; y < tile_size && flat; y++)
                    {
                        for (int x = 0; x < tile_size; x++)
                        {
                            int o = PixelOffset(baseX + x, baseY + y);
                            if (buffer[o + 0] != b0 || buffer[o + 1] != g0 || buffer[o + 2] != r0 || buffer[o + 3] != a0)
                            {
                                flat = false;
                                break;
                            }
                        }
                    }

                    if (!flat) continue;

                    int argb = (a0 << 24) | (r0 << 16) | (g0 << 8) | b0;
                    flatTileColorCounts[argb] = flatTileColorCounts.GetValueOrDefault(argb) + 1;
                    flatTileCells.Add((col, row, argb));
                }
            }

            var fillerColors = new HashSet<int>(
                flatTileColorCounts.Where(kv => kv.Value >= minFlatTilesToCountAsFiller).Select(kv => kv.Key));

            if (fillerColors.Count > 0)
            {
                foreach (var (col, row, argb) in flatTileCells)
                {
                    if (!fillerColors.Contains(argb)) continue;

                    int baseX = col * tile_size;
                    int baseY = row * tile_size;
                    for (int y = 0; y < tile_size; y++)
                    {
                        for (int x = 0; x < tile_size; x++)
                        {
                            if (((x + y) & 1) != 0) continue; // checkerboard: only tint every other pixel
                            int o = PixelOffset(baseX + x, baseY + y);
                            float srcA = tintColor.A / 255f;
                            buffer[o + 0] = (byte)((tintColor.B * srcA) + (buffer[o + 0] * (1 - srcA)));
                            buffer[o + 1] = (byte)((tintColor.G * srcA) + (buffer[o + 1] * (1 - srcA)));
                            buffer[o + 2] = (byte)((tintColor.R * srcA) + (buffer[o + 2] * (1 - srcA)));
                        }
                    }
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, data.Scan0, byteCount);
            composite.UnlockBits(data);
        }

        public static Dictionary<Range, Bitmap> DrawTileset(ROM rom, Game game, int mapOffset)
        {
            var pal = ReadBGPalette(rom, game);

            // animated tile sequences
            rom.PushPosition(mapOffset + 0xC);
            int numberOfSequences = rom.ReadInt();
            int sequencePtr = rom.ReadPointer();

            var tilesets = new Dictionary<Range, Bitmap>();

            if (numberOfSequences > 0 && sequencePtr != 0x0)
            {
                rom.Seek(sequencePtr);
                for (int i = 0; i < numberOfSequences; i++)
                {
                    int sequenceStructPtr = rom.ReadPointer();
                    rom.PushPosition(sequenceStructPtr);

                    int numberOfStrips = rom.ReadByte();
                    int numberOfFrames = rom.ReadByte();
                    int vramOffset = rom.ReadShort();

                    if (numberOfFrames <= 0 || numberOfStrips <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Bad animated-sequence header at 0x{sequenceStructPtr:X} (sequence {i}/{numberOfSequences}): " +
                            $"numberOfStrips={numberOfStrips}, numberOfFrames={numberOfFrames}, vramOffset={vramOffset}");
                    }

                    var sequenceImage = new Bitmap(numberOfFrames * 8, numberOfStrips * 8, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var seqRect = new Rectangle(0, 0, sequenceImage.Width, sequenceImage.Height);
                    var seqData = sequenceImage.LockBits(seqRect, System.Drawing.Imaging.ImageLockMode.WriteOnly, sequenceImage.PixelFormat);
                    int seqStride = seqData.Stride;
                    byte[] seqBuffer = new byte[seqStride * sequenceImage.Height];

                    for (int j = 0; j < numberOfStrips; j++)
                    {
                        int stripAddress = rom.ReadPointer();
                        var imgData = JCALG1.DecompressUnknownHeader(rom, stripAddress);

                        for (int src = 0; src < imgData.Length; src += 64)
                        {
                            int dx = (src / 64) * 8;
                            int dy = j * 8;

                            for (int y = 0; y < 8; y++)
                            {
                                for (int x = 0; x < 8; x++)
                                {
                                    var c = pal[imgData[src + (y * 8) + x]];
                                    int offset = ((dy + y) * seqStride) + ((dx + x) * 4);
                                    seqBuffer[offset + 0] = c.B;
                                    seqBuffer[offset + 1] = c.G;
                                    seqBuffer[offset + 2] = c.R;
                                    seqBuffer[offset + 3] = c.A;
                                }
                            }
                        }
                    }

                    System.Runtime.InteropServices.Marshal.Copy(seqBuffer, 0, seqData.Scan0, seqBuffer.Length);
                    sequenceImage.UnlockBits(seqData);

                    tilesets.Add(new Range(vramOffset, vramOffset + numberOfFrames - 1), sequenceImage);
                    rom.PopPosition();
                }
            }
            rom.PopPosition();

            // static tileset
            // Each entry in tilesetBytes is a 2-byte delta: the tile index advances by
            // that delta before placing the next tile. The destination slot in the tileset
            // bitmap is simply the entry's sequential position (i/2), independent of the
            // accumulated tile index used to look up which atlas tile to draw.
            rom.PushPosition(mapOffset + 0x48);
            byte[] tilesetBytes = JCALG1.Decompress(rom, rom.ReadPointer());
            int tileCount = tilesetBytes.Length / 2;
            int tsColumns = 16;
            int imageWidth = tsColumns * 8;
            int imageHeight = (int)Math.Ceiling((float)tileCount / tsColumns) * 8;

            if (tilesetBytes.Length == 0 || imageHeight <= 0)
            {
                throw new InvalidOperationException(
                    $"Static tileset decompressed to {tilesetBytes.Length} bytes at mapOffset 0x{mapOffset:X} (+0x48) " +
                    $"-> imageWidth={imageWidth}, imageHeight={imageHeight}");
            }

            var tileset = new Bitmap(imageWidth, Math.Max(8, imageHeight));
            using var ts = Graphics.FromImage(tileset);
            ts.InterpolationMode = InterpolationMode.NearestNeighbor;
            ts.PixelOffsetMode = PixelOffsetMode.Half;

            int currentTileIndex = 0;
            for (int i = 0; i < tilesetBytes.Length; i += 2)
            {
                // Delta-decode: advance the atlas tile index by the stored offset.
                // Tried reinterpreting this as signed (2026-09) to explain a gray-patch
                // rendering defect on Zone2/Area1 -- made no visible difference on that
                // map, so it wasn't the cause. Reverted to unsigned; ruled out, not fixed.
                int delta = tilesetBytes[i] | (tilesetBytes[i + 1] << 8);
                currentTileIndex += delta;

                // Source: which tile in the atlas to pull from.
                var src = GetTilesetImage(rom, game, currentTileIndex, pal);
                int atlasLocal = currentTileIndex % 256;
                int srcX = (atlasLocal % 16) * tile_size;
                int srcY = (atlasLocal / 16) * tile_size;

                // Destination: sequential slot in the output tileset bitmap.
                int slot = i / 2;
                int destX = (slot % tsColumns) * tile_size;
                int destY = (slot / tsColumns) * tile_size;

                ts.DrawImage(src, destX, destY, new Rectangle(srcX, srcY, tile_size, tile_size), GraphicsUnit.Pixel);

                // Advance past this tile so the next delta is relative to the tile
                // after the one we just placed.
                currentTileIndex++;
            }

            // The static tileset covers slots 0..(tileCount-1).
            tilesets.Add(new Range(0, tileCount - 1), tileset);
            rom.PopPosition();

            return tilesets;
        }

        public static Bitmap GetTilesetImage(ROM rom, Game game, int tileId, Color[] palette)
        {
            int tilesetId = tileId / tile_per_image;
            var cache = GetTilesetCache(game);

            if (cache.TryGetValue(tilesetId, out var bitmap))
                return bitmap;

            rom.PushPosition(game.TileAtlasOffset + (tilesetId * 4));
            int blobAddress = rom.ReadPointer();
            var imgData = JCALG1.Decompress(rom, blobAddress);

            bitmap = new Bitmap(128, 128, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
            int stride = bmpData.Stride;
            byte[] buffer = new byte[stride * bitmap.Height];

            for (int src = 0; src < imgData.Length; src += 64)
            {
                int dx = (src % 1024) / 8;
                int dy = (src / 1024) * 8;

                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        var c = palette[imgData[src + (y * 8) + x]];
                        int offset = ((dy + y) * stride) + ((dx + x) * 4);
                        buffer[offset + 0] = c.B;
                        buffer[offset + 1] = c.G;
                        buffer[offset + 2] = c.R;
                        buffer[offset + 3] = c.A;
                    }
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, bmpData.Scan0, buffer.Length);
            bitmap.UnlockBits(bmpData);

            rom.PopPosition();
            cache.Add(tilesetId, bitmap);
            return bitmap;
        }
        private static Color[] ReadBGPalette(ROM rom, Game game)
        {
            rom.PushPosition(game.BGPaletteOffset);
            var colors = new Color[256];

            for (int i = 0; i < colors.Length; i++)
            {
                int c = rom.ReadShort();
                int r = ((c & 0x1F) * 0x21) >> 2;
                int g = (((c >> 5) & 0x1F) * 0x21) >> 2;
                int b = (((c >> 10) & 0x1F) * 0x21) >> 2;
                colors[i] = Color.FromArgb(0xFF, r, g, b);
            }

            colors[0] = Color.FromArgb(0, 0, 0, 0);
            rom.PopPosition();
            return colors;
        }

        // Layer-type vtable pointers are compiled-in function addresses, so they
        // differ per ROM build even when the underlying format is identical.
        // These are this ROM's values (confirmed against real layer structs):
        //   tiled-grid layer -> 0x0056A1   (original LOGExtractor ROM: 0x8087)
        //
        // CONFIRMED via IDA 2026-09 that this is a SECOND, real, unrecognized
        // format, not corrupt data: a full-ROM scan found 49 layer slots across
        // 22 distinct maps (including major named locations -- West City,
        // Capsule Corporation, Pepper Town, Snowy Highlands, the Cell Games
        // Arena) whose leading dword is consistently 0x00004FCD instead of
        // 0x00056A1. Both values resolve to real, distinct compiled functions
        // in the ROM (sub_80056A0 / sub_8004FCC) matching the same "raw ROM
        // data starts with a dispatch pointer" convention used throughout this
        // engine (MapScript.handler, MapTrigger.function, MapItem.handler are
        // all the same pattern) -- so 0x4FCD is a legitimate second layer
        // format this renderer doesn't understand yet, NOT garbage. Chasing
        // sub_8004FCC's actual field layout is future work; until then, this
        // is flagged (`unsupported`) rather than silently rendered as empty,
        // since a map editor silently hiding real content is worse than
        // admitting it can't show something yet.
        private const int TiledLayerType = 0x0056A1;

        public static (Bitmap bitmap, int priority, bool unsupported) DrawLayer(ROM rom, Game game, int address, Dictionary<Range, Bitmap> tilesets, HashSet<Range> usedTilesets)
        {
            var cache = GetLayerCache(game);

            if (cache.TryGetValue(address, out var cached))
            {
                // Cached layers don't re-walk their chunks, so make sure the
                // ranges they used originally still get reported this time.
                foreach (var range in cached.usedTilesets)
                {
                    usedTilesets.Add(range);
                }
                return (cached.bitmap, cached.priority, cached.unsupported);
            }

            if (address == 0x0)
            {
                return (new Bitmap(1, 1), 0, false);
            }

            rom.PushPosition(address);

            int layerType = rom.ReadPointer();
            if (layerType != TiledLayerType)
            {
                rom.PopPosition();
                return (new Bitmap(1, 1), 0, true);
            }

            rom.Skip(0x9);
            int offX = rom.ReadShort() / 4;
            rom.Skip(0x2);

            // FIXED 2026-09 (found by diffing against the original LOGExtractor
            // MapViewer.MapRenderer.DrawLayer, which this was ported from): this is a
            // plain 16-bit scroll-Y value, `rom.ReadShort() / 4`, exactly like offX --
            // there never was a separate "priority" byte packed in here. A prior version
            // of this port incorrectly masked it to `(offYRaw & 0xFF) / 4` and treated the
            // discarded high byte as an unconfirmed "priority" field. That silently zeroed
            // offY on every real map that had a nonzero high byte (confirmed against real
            // ROM data: Z1A1/Z2A1's layers have raw offY values of 256/512/768, i.e. real
            // offY of 64/128/192 px -- this port was computing 0 for all of them), which is
            // what produced the reported vertical misalignment between BG layers and the
            // collision grid. GBA hardware priority is unrelated and unaffected -- it's
            // still the confirmed MAP-level Map_VariationEntry.bgPriorityPacked (+0x04)
            // field used by RenderMap, one byte per BG index.
            int offYRaw = rom.ReadShort();
            int offY = offYRaw / 4;

            rom.Skip(0x1);
            int numCols = rom.ReadByte();
            int numRows = rom.ReadByte();

            if (Environment.GetEnvironmentVariable("DBGLAYER") == "1")
                Console.WriteLine($"    [layer] addr=0x{address:X} offX(raw/4)={offX} offYRaw=0x{offYRaw:X} offY={offY} numCols={numCols} numRows={numRows}");

            if (offX >= 0x3F00)
            {
                offX -= 0x3F00;
                offX *= -1;
            }

            if (offY >= 0x3F00)
            {
                offY -= 0x3F00;
                offY *= -1;
            }

            var layerImage = new Bitmap(Math.Max(1, (numCols * chunk_size_pixels) - offX), Math.Max(1, (numRows * chunk_size_pixels) - offY));
            using var g = Graphics.FromImage(layerImage);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            rom.Skip(0x2);

            var layerUsedTilesets = new HashSet<Range>();

            for (int r = 0; r < numRows; r++)
            {
                for (int c = 0; c < numCols; c++)
                {
                    int chunkPtr = rom.ReadPointer();
                    if (Environment.GetEnvironmentVariable("DBGLAYER") == "1")
                        Console.WriteLine($"      [chunk] addr=0x{address:X} r={r} c={c} ptr=0x{chunkPtr:X}");
                    using var chunk = DrawChunk(rom, chunkPtr, tilesets, layerUsedTilesets);
                    g.DrawImage(chunk, (chunk_size_pixels * c) - offX, (chunk_size_pixels * r) - offY);
                }
            }

            foreach (var range in layerUsedTilesets)
            {
                usedTilesets.Add(range);
            }

            var result = (layerImage, priority: 0, layerUsedTilesets, unsupported: false); // priority field is dead -- see offY fix comment above; real priority comes from RenderMap's bgPriorityPacked
            cache.Add(address, result);
            rom.PopPosition();

            return (result.layerImage, result.priority, result.unsupported);
        }
        private static Bitmap DrawChunk(ROM rom, int address, Dictionary<Range, Bitmap> tilesets, HashSet<Range> usedTilesets)
        {
            var chunk_image = new Bitmap(chunk_size_pixels, chunk_size_pixels);

            if (address == 0x0)
            {
                return chunk_image;
            }

            using var g = Graphics.FromImage(chunk_image);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            byte[] bytes = JCALG1.Decompress(rom, address);

            int x = 0;
            int y = 0;

            for (int i = 0; i < bytes.Length; i += 2)
            {
                byte lsb = bytes[i];
                byte msb = bytes[i + 1];
                int entry = (msb << 8) + lsb;

                int tileId = entry & 0x3FF;
                bool flipX = (entry & (1 << 10)) != 0;
                bool flipY = (entry & (1 << 11)) != 0;

                // Tile ID 0 is the GBA transparent/blank tile — skip drawing but still
                // advance position so subsequent tiles land in the right slot.
                if (tileId != 0)
                {
                    bool found = false;
                    Range range = default;

                    foreach (var k in tilesets.Keys)
                    {
                        if (tileId >= k.Start.Value && tileId <= k.End.Value)
                        {
                            range = k;
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        usedTilesets.Add(range);

                        var tileset = tilesets[range];
                        int columns = tileset.Width / tile_size;
                        int l = tileId - range.Start.Value;
                        int srcX = (l % columns) * tile_size;
                        int srcY = (l / columns) * tile_size;

                        if (flipX || flipY)
                        {
                            using var flipped = FlipTile(tileset, srcX, srcY, flipX, flipY);
                            g.DrawImage(flipped, x, y);
                        }
                        else
                        {
                            g.DrawImage(tileset, x, y, new Rectangle(srcX, srcY, tile_size, tile_size), GraphicsUnit.Pixel);
                        }
                    }
                }

                x += tile_size;
                if (x >= chunk_size_pixels)
                {
                    x = 0;
                    y += tile_size;
                }
            }

            return chunk_image;
        }
        private static Bitmap FlipTile(Bitmap tileset, int srcX, int srcY, bool flipX, bool flipY)
        {
            var tile_image = new Bitmap(tile_size, tile_size);
            using var g = Graphics.FromImage(tile_image);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.DrawImage(tileset, 0, 0, new Rectangle(srcX, srcY, tile_size, tile_size), GraphicsUnit.Pixel);

            if (flipX && flipY)
            {
                tile_image.RotateFlip(RotateFlipType.RotateNoneFlipXY);
            }
            else if (flipX)
            {
                tile_image.RotateFlip(RotateFlipType.RotateNoneFlipX);
            }
            else if (flipY)
            {
                tile_image.RotateFlip(RotateFlipType.RotateNoneFlipY);
            }

            return tile_image;
        }

        /// <summary>
        /// Walks every BG layer's chunks the same way DrawLayer/DrawChunk do, but only
        /// records where each tile ID appears (world/composite pixel coordinates) instead
        /// of decoding pixels -- lets the editor highlight every occurrence of a tile the
        /// user clicked in the tile list. Kept separate from DrawLayer/DrawChunk (some
        /// duplicated parsing) rather than threading an output dictionary through their
        /// cached, pixel-decoding path, since that path has already been a source of
        /// subtle bugs and this is cheap enough to just re-walk on its own.
        /// </summary>
        public static Dictionary<int, List<Point>> BuildTileUsageIndex(ROM rom, int mapOffset)
        {
            var index = new Dictionary<int, List<Point>>();

            rom.PushPosition(mapOffset + 0x14);
            for (int i = 0; i < 4; i++)
            {
                int address = rom.ReadPointer();
                IndexLayerTiles(rom, address, index);
            }
            rom.PopPosition();

            return index;
        }

        private static void IndexLayerTiles(ROM rom, int address, Dictionary<int, List<Point>> index)
        {
            if (address == 0x0) return;

            rom.PushPosition(address);

            int layerType = rom.ReadPointer();
            if (layerType != TiledLayerType)
            {
                rom.PopPosition();
                return;
            }

            rom.Skip(0x9);
            int offX = rom.ReadShort() / 4;
            rom.Skip(0x2);
            int offYRaw = rom.ReadShort();
            int offY = offYRaw / 4;
            rom.Skip(0x1);
            int numCols = rom.ReadByte();
            int numRows = rom.ReadByte();

            if (offX >= 0x3F00) { offX -= 0x3F00; offX *= -1; }
            if (offY >= 0x3F00) { offY -= 0x3F00; offY *= -1; }

            rom.Skip(0x2);

            for (int r = 0; r < numRows; r++)
            {
                for (int c = 0; c < numCols; c++)
                {
                    int chunkPtr = rom.ReadPointer();
                    IndexChunkTiles(rom, chunkPtr, (chunk_size_pixels * c) - offX, (chunk_size_pixels * r) - offY, index);
                }
            }

            rom.PopPosition();
        }

        private static void IndexChunkTiles(ROM rom, int address, int originX, int originY, Dictionary<int, List<Point>> index)
        {
            if (address == 0x0) return;

            byte[] bytes;
            try
            {
                bytes = JCALG1.Decompress(rom, address);
            }
            catch
            {
                return;
            }

            int x = 0;
            int y = 0;

            for (int i = 0; i < bytes.Length; i += 2)
            {
                byte lsb = bytes[i];
                byte msb = bytes[i + 1];
                int entry = (msb << 8) + lsb;
                int tileId = entry & 0x3FF;

                if (tileId != 0)
                {
                    if (!index.TryGetValue(tileId, out var list))
                    {
                        list = [];
                        index[tileId] = list;
                    }
                    list.Add(new Point(originX + x, originY + y));
                }

                x += tile_size;
                if (x >= chunk_size_pixels)
                {
                    x = 0;
                    y += tile_size;
                }
            }
        }

        private static Dictionary<int, Bitmap> GetTilesetCache(Game game)
        {
            if (!tilesetCaches.TryGetValue(game, out var cache))
            {
                cache = [];
                tilesetCaches[game] = cache;
            }
            return cache;
        }

        private static Dictionary<int, (Bitmap bitmap, int priority, HashSet<Range> usedTilesets, bool unsupported)> GetLayerCache(Game game)
        {
            if (!layerCaches.TryGetValue(game, out var cache))
            {
                cache = [];
                layerCaches[game] = cache;
            }
            return cache;
        }


    }

    public readonly struct MapRenderOptions
    {
        public bool ShowBG0 { get; init; } = true;
        public bool ShowBG1 { get; init; } = true;
        public bool ShowBG2 { get; init; } = true;
        public bool ShowBG3 { get; init; } = true;
        public bool ShowCollision { get; init; } = false;
        public MapRenderOptions() { }
    }
}