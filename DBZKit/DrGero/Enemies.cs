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
        /// Appends new enemy records (Entity.Kind == Enemy, SourceAddress == 0) to the map's
        /// mapScripts[]. Each gets its own action object cloned from the game's most common
        /// one and a single waypoint at its own spawn point (a stationary enemy -- 311 real
        /// ones use a one-entry list), all freshly allocated so nothing is shared.
        /// </summary>
        public static int PersistNewEnemies(ROM rom, IReadOnlyList<MapEntry> allEntries, MapEntry entry, int mapEntryAddress, List<MapEntry.Entity> entities)
        {
            var fresh = entities.Where(e => e.Kind == MapEntry.EntityKind.Enemy && e.SourceAddress == 0).ToList();
            if (fresh.Count == 0) return 0;

            var (actionFunc, actionPayload) = MostCommonAction(rom, allEntries);

            var existing = new List<byte>();
            if (entry.MapScripts != 0 && entry.ScriptCount > 0)
                existing.AddRange(rom.ReadBytesAt(entry.MapScripts, entry.ScriptCount * 4));

            var newPointers = new List<byte>();
            foreach (var enemy in fresh)
            {
                // Sub-objects first so the record can point at them.
                var waypoint = new List<byte>();
                waypoint.AddRange(BitConverter.GetBytes(WaypointHandler));
                waypoint.AddRange(BitConverter.GetBytes((ushort)enemy.X));
                waypoint.AddRange(BitConverter.GetBytes((ushort)enemy.Y));
                waypoint.AddRange(BitConverter.GetBytes(0x80));
                int waypointAddr = rom.AllocateFreeSpace(waypoint.Count);
                rom.WriteBytesAt(waypointAddr, waypoint.ToArray());

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
                record.AddRange(BitConverter.GetBytes(0x08000000 | waypointAddr));
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
    }
}
