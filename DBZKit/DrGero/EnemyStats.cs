using DrGero.IO;
using DrGero.Types;
using Game = DrGero.Config.Game;

namespace DrGero
{
    /// <summary>
    /// The combat stat table (IDA: g_ScouterStatDatabase), 24 bytes per entry. Stats belong to an entry, not to an enemy: an enemy
    /// record only holds the entry's index (MapScriptEnemyRecord +0x08), so every enemy with the same index shares them.
    ///
    /// Entry layout:
    ///   +0x00 s32  EXP base. CONFIRMED (sub_800E4D6, run when an enemy is defeated): 0 = no EXP; 1-5 = given as is; otherwise
    ///              nothing at level 50, else clamped to [need/128, need/2] where need = the active character's EXP from their level
    ///              to the next (EXP table via sub_800426A). So the number shown in game depends on who is fighting.
    ///   +0x04 s32  max HP (CombatEntity_Create copies it as the starting HP)
    ///   +0x08 s32  passed on by CombatEntity_Create -> ScouterTarget_Register, but NOT an index into g_ScouterDatabase (it matches
    ///              the scouter table's statIndex link for 1 of 122 entries). Names come from that table instead (ScouterNames).
    ///   +0x0C u8   STR, +0x0D u8 POW, +0x0E u8 END (the scouter's numbers)
    ///   +0x0F..+0x17  not mapped (0x80, 0x80/0x60, a ROM pointer)
    ///
    /// The table is 109 entries in the US ROM (other data starts right after it), and code reaches it through three literals
    /// (0x0800E79C MapScript_CreateEnemy, 0x08017A38, 0x0801912C GetScouterIndexEntry). <see cref="MakeUnique"/> moves it to free
    /// space with room to grow, behind an 8-byte header {"DRST", capacity}, and repoints those literals.
    /// </summary>
    public static class EnemyStats
    {
        public const int EntrySize = 24;
        private const int OriginalCount = 109;      // US ROM: 0x086D56AC .. 0x086D60E4, where the next table starts
        private const int Spare = 256;              // room added on a move, so most "make unique" calls never move it again
        private static readonly byte[] Magic = "DRST"u8.ToArray();

        public readonly record struct Entry(int Index, int Exp, int Hp, int ScouterIndex, int Str, int Pow, int End);

        private static int U32(ROM rom, int a) => BitConverter.ToInt32(rom.ReadBytesAt(a, 4));

        private static bool IsMoved(ROM rom, int table) =>
            table >= 8 && rom.ReadBytesAt(table - 8, 4).AsSpan().SequenceEqual(Magic);

        /// <summary>The code literals that hold the table's address (aligned words in the code area pointing at it).</summary>
        private static List<int> Literals(ROM rom, Game game)
        {
            var found = new List<int>();
            if (game.ScouterStatsOffset == 0) return found;
            uint original = 0x08000000u | (uint)game.ScouterStatsOffset;
            var b = rom.RawBytes;
            int codeEnd = Math.Min(b.Length, 0x40000); // thumb code lives in the first 256 KB
            for (int o = 0; o + 4 <= codeEnd; o += 4)
            {
                uint v = BitConverter.ToUInt32(b, o);
                if (v == original) { found.Add(o); continue; }
                int at = (int)(v & 0x01FFFFFF);
                if (v is >= 0x08000000 and < 0x0A000000 && at >= 8 && at + 4 <= b.Length && IsMoved(rom, at)) found.Add(o);
            }
            return found;
        }

        /// <summary>File offset of the table: where the code points (it may have been moved), else the game profile's address.</summary>
        public static int TableAddress(ROM rom, Game game)
        {
            foreach (int lit in Literals(rom, game))
            {
                uint v = (uint)U32(rom, lit);
                return (int)(v & 0x01FFFFFF);
            }
            return game.ScouterStatsOffset;
        }

        private static int Capacity(ROM rom, int table) => IsMoved(rom, table) ? U32(rom, table - 4) : OriginalCount;

        public static Entry? Read(ROM rom, Game game, int index)
        {
            int table = TableAddress(rom, game);
            if (table <= 0 || index < 0 || index >= Capacity(rom, table) || table + (index + 1) * EntrySize > rom.Length) return null;
            var b = rom.ReadBytesAt(table + index * EntrySize, EntrySize);
            return new Entry(index, BitConverter.ToInt32(b, 0), BitConverter.ToInt32(b, 4), BitConverter.ToInt32(b, 8), b[12], b[13], b[14]);
        }

        public static void Write(ROM rom, Game game, Entry e)
        {
            int at = TableAddress(rom, game) + e.Index * EntrySize;
            rom.PatchInt32(at, e.Exp);
            rom.PatchInt32(at + 4, e.Hp);
            rom.PatchByte(at + 12, (byte)Math.Clamp(e.Str, 0, 255));
            rom.PatchByte(at + 13, (byte)Math.Clamp(e.Pow, 0, 255));
            rom.PatchByte(at + 14, (byte)Math.Clamp(e.End, 0, 255));
        }

        public const int MaxLevel = 50;

        /// <summary>
        /// Total EXP to reach each level (index 0 = level 1), from the shared EXP table every character uses
        /// (game profile DefaultExpThresholds, 0x081D9834 in the US ROM; read by sub_800426A). Null if the profile doesn't map it.
        /// </summary>
        public static int[]? ExpTable(ROM rom, Game game)
        {
            int t = game.DefaultExpThresholds;
            if (t <= 0 || t + MaxLevel * 4 > rom.Length) return null;
            return [.. Enumerable.Range(0, MaxLevel).Select(i => U32(rom, t + i * 4))];
        }

        /// <summary>
        /// The EXP the game actually gives for defeating an enemy with EXP base <paramref name="expBase"/> while the active character
        /// is at <paramref name="level"/> -- sub_800E4D6 exactly: 0 gives nothing, 1-5 are given as is, nothing at level 50, otherwise
        /// clamped to [need/128, need/2] where need is the EXP from this level to the next.
        /// </summary>
        public static int Reward(int expBase, int level, int[] expTable)
        {
            if (expBase <= 0) return 0;
            if (expBase <= 5) return expBase;
            if (level >= MaxLevel || level < 1) return 0;
            int need = expTable[level] - expTable[level - 1];
            int least = need >> 7, most = need >> 1;
            return expBase < least ? least : expBase > most ? most : expBase;
        }

        /// <summary>
        /// The EXP base to store so a level-<paramref name="level"/> player receives <paramref name="wanted"/> EXP, the inverse of
        /// <see cref="Reward"/>. When the game can't give that much (or that little) at this level, the base for the nearest amount it
        /// can is returned and <c>Note</c> says why. <c>Gets</c> is what that player will actually receive.
        /// </summary>
        public static (int Base, int Gets, string? Note) BaseFor(int wanted, int level, int[] expTable)
        {
            if (wanted <= 0) return (0, 0, null);
            if (wanted <= 5) return (wanted, wanted, null); // 1-5 are never scaled
            if (level >= MaxLevel) return (0, 0, $"A level {MaxLevel} player never gets EXP above 5.");
            int need = expTable[level] - expTable[level - 1];
            int least = Math.Max(6, need >> 7), most = need >> 1;
            if (most < 6) return (5, 5, $"At level {level} the most the game gives is 5 EXP.");
            if (wanted > most) return (most, most, $"At level {level} the most the game gives is {most:N0} EXP (half of the {need:N0} needed for the next level).");
            if (wanted < least) return (least, least, $"At level {level} the least the game gives is {least:N0} EXP (1/128 of a level), unless you use 1-5.");
            return (wanted, wanted, null);
        }

        /// <summary>The name the scouter shows for an entry (its +0x08 scouter entry's name), or null.</summary>
        public static string? Name(ROM rom, Game game, Entry e, int spriteId = -1) => ScouterNames.ForEnemy(rom, game, e.Index, spriteId);

        /// <summary>How many enemy records in the game use stat entry <paramref name="index"/>.</summary>
        public static int CountUsers(ROM rom, IEnumerable<MapEntry> entries, int index) =>
            EnemyRecords.EnumerateRecords(rom, entries).Count(r => U32(rom, r.Record + EnemyRecords.StatIndexOffset) == index);

        /// <summary>
        /// Gives one enemy record its own copy of its stat entry, so editing it changes only that enemy. Returns the new index.
        /// The table is moved (once) to free space with room for <see cref="Spare"/> more entries if it has no free slot.
        /// </summary>
        public static int MakeUnique(ROM rom, Game game, IEnumerable<MapEntry> entries, int enemyRecord)
        {
            int current = U32(rom, enemyRecord + EnemyRecords.StatIndexOffset);
            int slot = AddEntry(rom, game, entries, current);
            rom.PatchInt32(enemyRecord + EnemyRecords.StatIndexOffset, slot);
            return slot;
        }

        /// <summary>
        /// Adds a new stat entry, a copy of <paramref name="copyFrom"/>, that nothing uses yet; returns its index. For an enemy type
        /// whose new stats should only go to enemies placed from now on. Moves the table (once) if it has no free slot.
        /// </summary>
        public static int AddEntry(ROM rom, Game game, IEnumerable<MapEntry> entries, int copyFrom)
        {
            var literals = Literals(rom, game);
            if (literals.Count == 0) throw new InvalidOperationException("Couldn't find the code that points at the stat table, so it can't grow.");
            int table = TableAddress(rom, game), capacity = Capacity(rom, table);

            // A free slot is past every index anything uses (enemies, and the scouter list's own stat column) and still all zero --
            // an entry added for future placements has no users yet, but it isn't free.
            var all = entries.ToList();
            int used = EnemyRecords.EnumerateRecords(rom, all).Select(r => U32(rom, r.Record + EnemyRecords.StatIndexOffset)).DefaultIfEmpty(0).Max();
            if (game.ScouterTableOffset != 0)
                for (int i = 0; i < game.ScouterCount && game.ScouterTableOffset + i * 20 + 20 <= rom.Length; i++)
                    used = Math.Max(used, U32(rom, game.ScouterTableOffset + i * 20 + 16));
            int slot = Math.Max(OriginalCount, used + 1);
            while (slot < capacity && rom.ReadBytesAt(table + slot * EntrySize, EntrySize).Any(b => b != 0)) slot++;

            if (slot >= capacity)
            {
                int newCapacity = slot + Spare;
                var block = new byte[8 + newCapacity * EntrySize];
                Magic.CopyTo(block, 0);
                BitConverter.GetBytes(newCapacity).CopyTo(block, 4);
                rom.ReadBytesAt(table, capacity * EntrySize).CopyTo(block, 8);
                int at = rom.AllocateFreeSpace(block.Length);
                rom.WriteBytesAt(at, block);
                table = at + 8;
                foreach (int lit in literals) rom.PatchInt32(lit, 0x08000000 | table);
            }

            rom.WriteBytesAt(table + slot * EntrySize, rom.ReadBytesAt(table + copyFrom * EntrySize, EntrySize));
            return slot;
        }
    }
}
