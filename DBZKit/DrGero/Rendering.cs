using DrGero.Config;
using DrGero.IO;
using DrGero.Types;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private const int CharacterSpawnHandlerOffset = 0x00D6DF;
        private const int MapScriptRecordSize = 32; // MapScriptSpawnMultiConditional

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
                // Other handlers exist in mapScripts (enemies, effects, etc.)
                // but haven't been identified yet -- skipped rather than
                // guessing at their record layout/position.

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

        private static Bitmap RenderIndexed(byte[] data, int width, int height, Color[] palette)
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
        private static readonly Dictionary<Game, Dictionary<int, (Bitmap bitmap, int priority, HashSet<Range> usedTilesets)>> layerCaches = [];

        public static void ResetCache(Game game)
        {
            tilesetCaches.Remove(game);
            layerCaches.Remove(game);
        }

        public static (Bitmap bitmap, Dictionary<Range, Bitmap> tilesets, HashSet<Range> usedTilesets) RenderMap(ROM rom, Game game, int mapOffset, MapRenderOptions options)
        {
            var tilesets = DrawTileset(rom, game, mapOffset);
            var usedTilesets = new HashSet<Range>();

            rom.PushPosition(mapOffset + 0x14);
            var layer0 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            var layer1 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            var layer2 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            var layer3 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            rom.PopPosition();

            rom.PushPosition(mapOffset + 0x8);
            int width = rom.ReadShort() + 240;
            int height = rom.ReadShort() + 160;
            rom.PopPosition();

            var composite = new Bitmap(Math.Max(1, width), Math.Max(1, height));
            using var g = Graphics.FromImage(composite);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            if (options.ShowBG3) g.DrawImage(layer3.bitmap, 0, 0);
            if (options.ShowBG2) g.DrawImage(layer2.bitmap, 0, 0);
            if (options.ShowBG1) g.DrawImage(layer1.bitmap, 0, 0);
            if (options.ShowBG0) g.DrawImage(layer0.bitmap, 0, 0);

            return (composite, tilesets, usedTilesets);
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
        private const int TiledLayerType = 0x0056A1;

        public static (Bitmap bitmap, int priority) DrawLayer(ROM rom, Game game, int address, Dictionary<Range, Bitmap> tilesets, HashSet<Range> usedTilesets)
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
                return (cached.bitmap, cached.priority);
            }

            if (address == 0x0)
            {
                return (new Bitmap(1, 1), 0);
            }

            rom.PushPosition(address);

            int layerType = rom.ReadPointer();
            if (layerType != TiledLayerType)
            {
                rom.PopPosition();
                return (new Bitmap(1, 1), 0);
            }

            rom.Skip(0x9);
            int offX = rom.ReadShort() / 4;
            rom.Skip(0x2);

            // +0x11 = scroll Y low byte, +0x12 = BG priority
            int offYRaw = rom.ReadShort();
            int offY = (offYRaw & 0xFF) / 4;
            int priority = (offYRaw >> 8) & 0xFF;

            rom.Skip(0x1);
            int numCols = rom.ReadByte();
            int numRows = rom.ReadByte();

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
                    using var chunk = DrawChunk(rom, rom.ReadPointer(), tilesets, layerUsedTilesets);
                    g.DrawImage(chunk, (chunk_size_pixels * c) - offX, (chunk_size_pixels * r) - offY);
                }
            }

            foreach (var range in layerUsedTilesets)
            {
                usedTilesets.Add(range);
            }

            var result = (layerImage, priority, layerUsedTilesets);
            cache.Add(address, result);
            rom.PopPosition();

            return (result.layerImage, result.priority);
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

        private static Dictionary<int, Bitmap> GetTilesetCache(Game game)
        {
            if (!tilesetCaches.TryGetValue(game, out var cache))
            {
                cache = [];
                tilesetCaches[game] = cache;
            }
            return cache;
        }

        private static Dictionary<int, (Bitmap bitmap, int priority, HashSet<Range> usedTilesets)> GetLayerCache(Game game)
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
        public MapRenderOptions() { }
    }
}