using DrGero.IO;
using DrGero.Types;

namespace DrGero
{
    /// <summary>
    /// Enemy spawn records: the mapScripts[] handler at 0x0800E77F (MapScript_CreateEnemy),
    /// 666 records across 167 maps in the US ROM -- the bulk of what walks around a map.
    ///
    /// CONFIRMED via IDA 2026-09-19 (MapScript_CreateEnemy -> CombatEntity_Create, verified
    /// against real records). Record layout (MapScriptEnemyRecord, IDA type declared):
    ///   +0x00 handler       0x0800E77F
    ///   +0x04 flags         spawn condition, same as every mapScripts[] record (0 = always)
    ///   +0x08 statIndex     index into g_ScouterStatDatabase (24-byte DBZ_ScouterStatEntry:
    ///                       hp/str/pow/end...) -- WHICH enemy this is
    ///   +0x0C x, +0x0E y    int16 pixels
    ///   +0x10 spriteId      Character_GetSpriteId, same id space as NPCs (>=7)
    ///   +0x14 actionObj*    {func, payload}: func 0x0801069B runs `payload` as VM bytecode
    ///                       (EnemyAction_RunScript), 0x080106B3 opens `payload` as a dialog
    ///                       sequence[] (EnemyAction_RunDialog), 0x08010699 does nothing.
    ///                       WHEN the game fires it is not yet traced.
    ///   +0x18 count, +0x1C ptr[count]  the entity's behavior list, cycled forever by
    ///                       EntityBehaviorList_Tick. Each entry begins with a create-fn:
    ///                       0x0800D625 = waypoint move {fn, u16 x, u16 y, u32 param(0x80)},
    ///                       0x0800CB0F = idle random walk {fn, u32 flags}. count is always >= 1.
    ///   record size = 0x1C + 4*count.
    /// Waypoint records are never shared between enemies (checked across all 1554).
    ///
    /// The same behavior list also drives ordinary NPCs (handler 0x0800D6DF), where it sits
    /// INLINE at record+0x10 ({count, ptr[]}) -- so a 32-byte NPC record's last 8 bytes
    /// (IDA calls them next/spawnFunc) are really its one inline behavior object.
    /// </summary>
    public static class EnemyRecords
    {
        public const int SpawnHandler = 0x0800E77F;
        public const int RunScriptFunc = 0x0801069B;
        public const int RunDialogFunc = 0x080106B3;
        public const int WaypointHandler = 0x0800D625;
        public const int IdleHandler = 0x0800CB0F;          // NpcBehavior_FaceAndWait_Create: face a direction, wait
        public const int StandWaitHandler = 0x08010099;     // NpcBehavior_StandWait_Create: animation 0, wait N ticks
        public const int WanderSolidHandler = 0x0800CFDD;   // NpcBehavior_WanderSolid_Create: wander, blocked by walls and the player

        public const int StatIndexOffset = 0x08;
        public const int PositionOffset = 0x0C;
        public const int SpriteOffset = 0x10;
        public const int ActionObjOffset = 0x14;
        public const int BehaviorCountOffset = 0x18;
        public const int BehaviorListOffset = 0x1C;

        public readonly record struct Template(int StatIndex, int SpriteId, int Count, string ExampleMap);

        private static int U32(ROM rom, int address) => BitConverter.ToInt32(rom.ReadBytesAt(address, 4));

        /// <summary>Every enemy record address in the given maps (file offsets).</summary>
        public static IEnumerable<(MapEntry Map, int Record)> EnumerateRecords(ROM rom, IEnumerable<MapEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.MapScripts == 0 || entry.ScriptCount == 0) continue;
                for (int i = 0; i < entry.ScriptCount; i++)
                {
                    int record = U32(rom, entry.MapScripts + (i * 4)) & 0x00FFFFFF;
                    if (record == 0 || record + 0x20 > rom.Length) continue;
                    if (U32(rom, record) == SpawnHandler)
                        yield return (entry, record);
                }
            }
        }

        /// <summary>
        /// Distinct (statIndex, spriteId) pairs the game itself uses, most common first --
        /// the palette for placing enemies. A pair the game already ships is guaranteed to
        /// be a real, renderable enemy; inventing an untested stat/sprite combination isn't.
        /// </summary>
        public static List<Template> FindTemplates(ROM rom, IReadOnlyList<MapEntry> entries)
        {
            var counts = new Dictionary<(int stat, int sprite), (int n, string map)>();
            foreach (var (map, record) in EnumerateRecords(rom, entries))
            {
                var key = (U32(rom, record + StatIndexOffset), U32(rom, record + SpriteOffset));
                counts[key] = counts.TryGetValue(key, out var v) ? (v.n + 1, v.map) : (1, $"Z{map.Zone} A{map.Area}");
            }
            return counts.OrderByDescending(kv => kv.Value.n)
                .Select(kv => new Template(kv.Key.stat, kv.Key.sprite, kv.Value.n, kv.Value.map)).ToList();
        }

        /// <summary>
        /// The (func, payload) action pair shared by the most enemy records -- what a newly
        /// placed enemy gets, so it behaves like the game's own typical enemy.
        /// </summary>
        private static (int Func, int Payload) MostCommonAction(ROM rom, IReadOnlyList<MapEntry> entries)
        {
            var counts = new Dictionary<(int, int), int>();
            foreach (var (_, record) in EnumerateRecords(rom, entries))
            {
                int obj = U32(rom, record + ActionObjOffset) & 0x00FFFFFF;
                if (obj == 0 || obj + 8 > rom.Length) continue;
                var key = (U32(rom, obj), U32(rom, obj + 4));
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
            return counts.Count == 0 ? (RunScriptFunc, 0) : counts.OrderByDescending(kv => kv.Value).First().Key;
        }

        /// <summary>
        /// The behaviour a stationary enemy gets: FaceAndWait {fn, u32 param} with the param the game's own one-entry lists use most
        /// (0x7D00 in the US ROM). 311 real enemies have a one-entry list, and NONE of them is a waypoint -- they are FaceAndWait,
        /// StandWait or a wander. A lone waypoint at the enemy's own spawn finishes the moment it starts, so the endlessly cycled
        /// behaviour list never yields and the game hangs (reported: placing "Enemy 9" froze the game, script or not).
        /// </summary>
        private static byte[] StationaryBehavior(ROM rom, IReadOnlyList<MapEntry> entries)
        {
            var counts = new Dictionary<int, int>();
            foreach (var (_, record) in EnumerateRecords(rom, entries))
            {
                if (U32(rom, record + BehaviorCountOffset) != 1) continue;
                int b = U32(rom, record + BehaviorListOffset) & 0x00FFFFFF;
                if (b == 0 || b + 8 > rom.Length || U32(rom, b) != IdleHandler) continue;
                int param = U32(rom, b + 4);
                counts[param] = counts.GetValueOrDefault(param) + 1;
            }
            int best = counts.Count == 0 ? 0x7D00 : counts.OrderByDescending(kv => kv.Value).First().Key;
            return [.. BitConverter.GetBytes(IdleHandler), .. BitConverter.GetBytes(best)];
        }

        /// <summary>
        /// Repairs enemies placed by older versions of this tool on <paramref name="entry"/>: a behaviour list that is one waypoint
        /// (the game never ships one -- see <see cref="StationaryBehavior"/>) gets the stationary behaviour instead. Returns how many.
        /// </summary>
        public static int RepairLoneWaypointEnemies(ROM rom, IReadOnlyList<MapEntry> allEntries, MapEntry entry)
        {
            byte[]? stationary = null;
            int fixedCount = 0;
            foreach (var (_, record) in EnumerateRecords(rom, [entry]))
            {
                if (U32(rom, record + BehaviorCountOffset) != 1) continue;
                int b = U32(rom, record + BehaviorListOffset) & 0x00FFFFFF;
                if (b == 0 || b + 4 > rom.Length || U32(rom, b) != WaypointHandler) continue;
                stationary ??= StationaryBehavior(rom, allEntries);
                int at = rom.AllocateFreeSpace(stationary.Length);
                rom.WriteBytesAt(at, stationary);
                rom.PatchInt32(record + BehaviorListOffset, 0x08000000 | at);
                fixedCount++;
            }
            return fixedCount;
        }

        /// <summary>
        /// Appends new enemy records (Entity.Kind == Enemy, SourceAddress == 0) to the map's
        /// mapScripts[]. Each gets its own action object cloned from the game's most common
        /// one and the stationary FaceAndWait behaviour (<see cref="StationaryBehavior"/>), all
        /// freshly allocated so nothing is shared. Enemies an older version placed with a lone
        /// waypoint are repaired on the way (<see cref="RepairLoneWaypointEnemies"/>).
        /// </summary>
        public static int PersistNewEnemies(ROM rom, IReadOnlyList<MapEntry> allEntries, MapEntry entry, int mapEntryAddress, List<MapEntry.Entity> entities)
        {
            RepairLoneWaypointEnemies(rom, allEntries, entry);
            var fresh = entities.Where(e => e.Kind == MapEntry.EntityKind.Enemy && e.SourceAddress == 0).ToList();
            if (fresh.Count == 0) return 0;

            var (actionFunc, actionPayload) = MostCommonAction(rom, allEntries);
            byte[] stationary = StationaryBehavior(rom, allEntries);

            var existing = new List<byte>();
            if (entry.MapScripts != 0 && entry.ScriptCount > 0)
                existing.AddRange(rom.ReadBytesAt(entry.MapScripts, entry.ScriptCount * 4));

            var newPointers = new List<byte>();
            foreach (var enemy in fresh)
            {
                // Sub-objects first so the record can point at them.
                int behaviorAddr = rom.AllocateFreeSpace(stationary.Length);
                rom.WriteBytesAt(behaviorAddr, stationary);

                var action = new List<byte>();
                action.AddRange(BitConverter.GetBytes(actionFunc));
                action.AddRange(BitConverter.GetBytes(actionPayload));
                int actionAddr = rom.AllocateFreeSpace(action.Count);
                rom.WriteBytesAt(actionAddr, action.ToArray());

                var record = new List<byte>();
                record.AddRange(BitConverter.GetBytes(SpawnHandler));
                record.AddRange(BitConverter.GetBytes(0));                       // flags: always spawns
                record.AddRange(BitConverter.GetBytes(enemy.StatIndex));
                record.AddRange(BitConverter.GetBytes((short)enemy.X));
                record.AddRange(BitConverter.GetBytes((short)enemy.Y));
                record.AddRange(BitConverter.GetBytes(enemy.TypeId));            // spriteId
                record.AddRange(BitConverter.GetBytes(0x08000000 | actionAddr));
                record.AddRange(BitConverter.GetBytes(1));                       // behavior count
                record.AddRange(BitConverter.GetBytes(0x08000000 | behaviorAddr));
                int recordAddr = rom.AllocateFreeSpace(record.Count);
                rom.WriteBytesAt(recordAddr, record.ToArray());

                newPointers.AddRange(BitConverter.GetBytes(0x08000000 | recordAddr));
            }

            var array = existing.Concat(newPointers).ToArray();
            int arrayAddr = rom.AllocateFreeSpace(array.Length);
            rom.WriteBytesAt(arrayAddr, array);

            rom.PatchInt32(mapEntryAddress + 0x14, 0x08000000 | arrayAddr);
            rom.PatchByte(mapEntryAddress + 0x04, (byte)Math.Min(255, entry.ScriptCount + fresh.Count));
            return fresh.Count;
        }

        /// <summary>
        /// Moves an enemy: rewrites its x/y and shifts every waypoint by the same delta, so
        /// its patrol keeps its shape around the new spot (a stationary enemy would otherwise
        /// walk back to where it started). Non-waypoint behavior entries are left alone.
        /// </summary>
        public static void Move(ROM rom, int record, int newX, int newY)
        {
            var old = rom.ReadBytesAt(record + PositionOffset, 4);
            int dx = newX - BitConverter.ToInt16(old, 0);
            int dy = newY - BitConverter.ToInt16(old, 2);

            rom.PatchInt16(record + PositionOffset, (short)newX);
            rom.PatchInt16(record + PositionOffset + 2, (short)newY);
            if (dx == 0 && dy == 0) return;

            int count = U32(rom, record + BehaviorCountOffset);
            if (count < 0 || count > 64) return; // not a sane list -- leave the waypoints alone
            for (int i = 0; i < count; i++)
            {
                int wp = U32(rom, record + BehaviorListOffset + (i * 4)) & 0x00FFFFFF;
                if (wp == 0 || wp + 8 > rom.Length || U32(rom, wp) != WaypointHandler) continue;
                var xy = rom.ReadBytesAt(wp + 4, 4);
                rom.PatchInt16(wp + 4, (short)(BitConverter.ToUInt16(xy, 0) + dx));
                rom.PatchInt16(wp + 6, (short)(BitConverter.ToUInt16(xy, 2) + dy));
            }
        }

        /// <summary>The address of an enemy's action object (0 if none).</summary>
        public static int ActionObject(ROM rom, int record) => U32(rom, record + ActionObjOffset) & 0x00FFFFFF;

        // ---- exact ("absolute") EXP ----
        // The game scales a stat entry's EXP base to the player's level (sub_800E4D6). The enemy's action object, though, is queued
        // to run when it dies (CombatEntity_TakeDamage -> defeat handler sub_801A74C -> sub_800E39C), and VM action 139 AddEXP gives
        // its amount to the active character unscaled (BytecodeVM_AddEXP -> CharStats_AddEXP). So exact EXP for one enemy = EXP base
        // 0 in its stat entry + "AddEXP(N);" at the start of its own action script.
        public const int AddExpStep = 139;
        public const int DoNothingFunc = 0x08010699;

        /// <summary>The N of an "AddEXP(N);" this enemy's action script starts with, or null.</summary>
        public static int? ReadExactExp(ROM rom, int record)
        {
            int obj = ActionObject(rom, record);
            if (obj <= 0 || obj + 8 > rom.Length || U32(rom, obj) != RunScriptFunc) return null;
            int script = U32(rom, obj + 4) & 0x00FFFFFF;
            if (script <= 0 || script + 4 > rom.Length) return null;
            var b = rom.ReadBytesAt(script, Math.Min(12, rom.Length - script));
            int len = ExactExpPrefixLength(b);
            if (len == 0) return null;
            int i = 1, raw = 0;
            while (true) { byte c = b[i++]; raw = (raw << 7) | (c & 0x7F); if ((c & 0x80) == 0) break; }
            return (raw >> 1) ^ -(raw & 1);
        }

        // Length of a leading "PushVarint n; Step AddEXP" (01 <varint> 02 8B), or 0.
        private static int ExactExpPrefixLength(byte[] b)
        {
            if (b.Length < 4 || b[0] != 0x01) return 0;
            int i = 1;
            while (i < b.Length && (b[i] & 0x80) != 0) i++;
            i++; // the varint's last byte
            return i + 1 < b.Length && b[i] == 0x02 && b[i + 1] == AddExpStep ? i + 2 : 0;
        }

        private static byte[] ExactExpPrefix(int amount)
        {
            uint zig = (uint)((amount << 1) ^ (amount >> 31));
            var groups = new List<byte>();
            do { groups.Insert(0, (byte)(zig & 0x7F)); zig >>= 7; } while (zig != 0);
            for (int i = 0; i < groups.Count - 1; i++) groups[i] |= 0x80;
            return [0x01, .. groups, 0x02, (byte)AddExpStep];
        }

        /// <summary>
        /// Gives this one enemy exact EXP (<paramref name="amount"/>), or removes it (null): writes a copy of its action script with
        /// "AddEXP(amount);" in front (or without it) and a new action object for this record only -- the scripts are shared by
        /// hundreds of enemies, so nothing existing is changed. Returns null when done, else why it can't be.
        /// </summary>
        public static string? SetExactExp(ROM rom, int record, int? amount)
        {
            int obj = ActionObject(rom, record);
            int func = obj > 0 && obj + 8 <= rom.Length ? U32(rom, obj) : DoNothingFunc;
            if (func == RunDialogFunc)
                return "This enemy's defeat action is a conversation, not a script, so exact EXP can't be added to it.";

            byte[] rest = [0x11]; // END: nothing else to run
            if (func == RunScriptFunc)
            {
                int script = U32(rom, obj + 4) & 0x00FFFFFF;
                if (script > 0 && script < rom.Length)
                {
                    var bytes = rom.ReadBytesAt(script, Math.Min(1024, rom.Length - script));
                    var decoded = Quests.InstructionDecoder.Decode(bytes);
                    if (decoded.Count == 0 || decoded[^1].Opcode != 0x11)
                        return "This enemy's defeat script couldn't be read to its end, so it wasn't changed.";
                    rest = bytes[..(decoded[^1].Offset + 1)];
                    int prefix = ExactExpPrefixLength(rest);
                    if (prefix > 0) rest = rest[prefix..]; // replace an existing exact amount
                }
            }
            else if (amount == null) return null; // no script and nothing to add

            byte[] newScript = amount is int n and > 0 ? [.. ExactExpPrefix(n), .. rest] : rest;
            int scriptAt = rom.AllocateFreeSpace(newScript.Length);
            rom.WriteBytesAt(scriptAt, newScript);
            int objAt = rom.AllocateFreeSpace(8);
            rom.WriteBytesAt(objAt, [.. BitConverter.GetBytes(RunScriptFunc), .. BitConverter.GetBytes(0x08000000 | scriptAt)]);
            rom.PatchInt32(record + ActionObjOffset, 0x08000000 | objAt);
            return null;
        }
    }
}
