using DrGero.IO;

namespace DrGero.Engine
{
    // Roster / ability / map-table access for the US ROM (ALFE), plus a few verified engine patches.
    // Every offset here is a FILE offset and comes from IDA (see Roster-and-Abilities.md and Engine-Notes.md).
    // Anything that patches code first checks the ORIGINAL bytes so it can never corrupt a different ROM.

    public sealed class PartySlot
    {
        public ushort CurrentHp, MaxHp, CurrentEp, MaxEp;
        public byte DisplayIndex, AbilityIndex;
        public ushort Str, Pow, End;
        public uint Exp, Flags;
        public byte[] Abilities = new byte[4];

        public const int Size = 28;

        public static PartySlot FromBytes(byte[] b, int o = 0) => new()
        {
            CurrentHp = BitConverter.ToUInt16(b, o), MaxHp = BitConverter.ToUInt16(b, o + 2),
            CurrentEp = BitConverter.ToUInt16(b, o + 4), MaxEp = BitConverter.ToUInt16(b, o + 6),
            DisplayIndex = b[o + 8], AbilityIndex = b[o + 9],
            Str = BitConverter.ToUInt16(b, o + 10), Pow = BitConverter.ToUInt16(b, o + 12), End = BitConverter.ToUInt16(b, o + 14),
            Exp = BitConverter.ToUInt32(b, o + 16), Flags = BitConverter.ToUInt32(b, o + 20),
            Abilities = b[(o + 24)..(o + 28)],
        };

        public byte[] ToBytes()
        {
            var b = new byte[Size];
            BitConverter.GetBytes(CurrentHp).CopyTo(b, 0); BitConverter.GetBytes(MaxHp).CopyTo(b, 2);
            BitConverter.GetBytes(CurrentEp).CopyTo(b, 4); BitConverter.GetBytes(MaxEp).CopyTo(b, 6);
            b[8] = DisplayIndex; b[9] = AbilityIndex;
            BitConverter.GetBytes(Str).CopyTo(b, 10); BitConverter.GetBytes(Pow).CopyTo(b, 12); BitConverter.GetBytes(End).CopyTo(b, 14);
            BitConverter.GetBytes(Exp).CopyTo(b, 16); BitConverter.GetBytes(Flags).CopyTo(b, 20);
            Abilities.CopyTo(b, 24);
            return b;
        }
    }

    public sealed class DisplayRow
    {
        public byte Flags, SpriteId, PortraitIndex, Unk3, TransformedIndex, DetransformedIndex, MinFrames, MaxFrames, Transformed2, Unk9;
        public ushort UnkA;
        public uint UnkC, VTable;

        public const int Size = 20;

        public static DisplayRow FromBytes(byte[] b, int o = 0) => new()
        {
            Flags = b[o], SpriteId = b[o + 1], PortraitIndex = b[o + 2], Unk3 = b[o + 3],
            TransformedIndex = b[o + 4], DetransformedIndex = b[o + 5], MinFrames = b[o + 6], MaxFrames = b[o + 7],
            Transformed2 = b[o + 8], Unk9 = b[o + 9], UnkA = BitConverter.ToUInt16(b, o + 10),
            UnkC = BitConverter.ToUInt32(b, o + 12), VTable = BitConverter.ToUInt32(b, o + 16),
        };

        public byte[] ToBytes()
        {
            var b = new byte[Size];
            b[0] = Flags; b[1] = SpriteId; b[2] = PortraitIndex; b[3] = Unk3; b[4] = TransformedIndex; b[5] = DetransformedIndex;
            b[6] = MinFrames; b[7] = MaxFrames; b[8] = Transformed2; b[9] = Unk9;
            BitConverter.GetBytes(UnkA).CopyTo(b, 10); BitConverter.GetBytes(UnkC).CopyTo(b, 12); BitConverter.GetBytes(VTable).CopyTo(b, 16);
            return b;
        }
    }

    public sealed class AbilityEntry
    {
        public uint ReadyIcon, CoolingIcon, IsReady, OnUse;
        public const int Size = 16;

        public static AbilityEntry FromBytes(byte[] b, int o = 0) => new()
        {
            ReadyIcon = BitConverter.ToUInt32(b, o), CoolingIcon = BitConverter.ToUInt32(b, o + 4),
            IsReady = BitConverter.ToUInt32(b, o + 8), OnUse = BitConverter.ToUInt32(b, o + 12),
        };

        public byte[] ToBytes()
        {
            var b = new byte[Size];
            BitConverter.GetBytes(ReadyIcon).CopyTo(b, 0); BitConverter.GetBytes(CoolingIcon).CopyTo(b, 4);
            BitConverter.GetBytes(IsReady).CopyTo(b, 8); BitConverter.GetBytes(OnUse).CopyTo(b, 12);
            return b;
        }
    }

    public static class RosterTables
    {
        public const int PartySize = 6; // hard limit: baked into PartyState and the save format (see Roster-and-Abilities.md)

        // Original table locations (file offsets).
        public const int OriginalDefaultStats = 0x1D9548, OriginalDisplay = 0x1D967C, OriginalAbilities = 0x1D9900;
        public const int OriginalDisplayRows = 22, OriginalAbilityCount = 14;

        // Code literals holding each table's ROM address (IDA + a ROM-wide search confirmed there are no interior pointers).
        public static readonly int[] DisplayLiterals =
            [0x1954, 0x1B3C, 0x3B30, 0x414C, 0x42F0, 0x854C, 0x886C, 0x8B04, 0xA77C, 0xAA04, 0xB050, 0xC804, 0x10F18, 0x112BC, 0x13574, 0x14FF4, 0x26458, 0x26474];
        public static readonly int[] AbilityLiterals = [0x8024, 0x8E8C];
        public const int DefaultStatsLiteral = 0x3EA0;
        public const int DisplayRowLimitImmediate = 0x42D6; // Thumb `CMP R2,#imm8` in CharacterData_FindBySprite: bytes [imm, 0x2A]

        public const int MaxDisplayRows = 255, AbilityCapacity = 64;

        public static readonly string[] OriginalAbilityNames =
            ["Big Bang Attack", "Burning Attack", "Ki Blast", "Kamehameha", "Masenkoball", "(none)", "Hercules",
             "Energy Punch", "Scatter Shot", "Special Beam Cannon", "Spirit Bomb", "Transformation", "Transformation (B)", "Sword Blast"];

        private static uint U32(ROM r, int o) => BitConverter.ToUInt32(r.ReadBytesAt(o, 4));
        private static void W32(ROM r, int o, uint v) => r.WriteBytesAt(o, BitConverter.GetBytes(v));
        private static int FileOffset(uint romAddress) => (int)(romAddress & 0x00FFFFFF);

        /// <summary>True when this looks like the US ROM these offsets were taken from.</summary>
        public static bool IsSupportedRom(ROM rom) =>
            rom.Length >= 0x800000 && FileOffset(U32(rom, DefaultStatsLiteral)) is >= 0x100000 && rom.ReadBytesAt(DisplayRowLimitImmediate + 1, 1)[0] == 0x2A;

        // ---- default party ------------------------------------------------------------------------------------
        public static int DefaultStatsAddress(ROM rom) => FileOffset(U32(rom, DefaultStatsLiteral));

        public static List<PartySlot> ReadDefaultParty(ROM rom)
        {
            int a = DefaultStatsAddress(rom);
            return Enumerable.Range(0, PartySize).Select(i => PartySlot.FromBytes(rom.ReadBytesAt(a + i * PartySlot.Size, PartySlot.Size))).ToList();
        }

        public static void WriteDefaultSlot(ROM rom, int slot, PartySlot s)
        {
            if (slot < 0 || slot >= PartySize) throw new ArgumentOutOfRangeException(nameof(slot));
            rom.WriteBytesAt(DefaultStatsAddress(rom) + slot * PartySlot.Size, s.ToBytes());
        }

        // ---- display rows -------------------------------------------------------------------------------------
        public static int DisplayTableAddress(ROM rom) => FileOffset(U32(rom, DisplayLiterals[0]));
        public static bool DisplayRelocated(ROM rom) => DisplayTableAddress(rom) != OriginalDisplay;
        public static int DisplayRowCount(ROM rom) => rom.ReadBytesAt(DisplayRowLimitImmediate, 1)[0];

        public static List<DisplayRow> ReadDisplayRows(ROM rom)
        {
            int a = DisplayTableAddress(rom), n = DisplayRowCount(rom);
            return Enumerable.Range(0, n).Select(i => DisplayRow.FromBytes(rom.ReadBytesAt(a + i * DisplayRow.Size, DisplayRow.Size))).ToList();
        }

        public static void WriteDisplayRow(ROM rom, int index, DisplayRow row)
        {
            if (index < 0 || index >= DisplayRowCount(rom)) throw new ArgumentOutOfRangeException(nameof(index));
            rom.WriteBytesAt(DisplayTableAddress(rom) + index * DisplayRow.Size, row.ToBytes());
        }

        /// <summary>
        /// Moves the display table to free space with room for <see cref="MaxDisplayRows"/> rows and repoints all 18 code
        /// literals. The original table is left in place, untouched. Idempotent. Returns the table's file offset.
        /// </summary>
        public static int RelocateDisplayTable(ROM rom)
        {
            if (DisplayRelocated(rom)) return DisplayTableAddress(rom);
            int count = DisplayRowCount(rom);
            var block = new byte[MaxDisplayRows * DisplayRow.Size];
            rom.ReadBytesAt(DisplayTableAddress(rom), count * DisplayRow.Size).CopyTo(block, 0);
            int addr = rom.AllocateFreeSpace(block.Length);
            rom.WriteBytesAt(addr, block);
            foreach (int lit in DisplayLiterals) W32(rom, lit, 0x08000000u | (uint)addr);
            return addr;
        }

        /// <summary>Appends a row (relocating on first use) and raises the row limit. Returns the new row's index.</summary>
        public static int AddDisplayRow(ROM rom, DisplayRow row)
        {
            RelocateDisplayTable(rom);
            int n = DisplayRowCount(rom);
            if (n >= MaxDisplayRows) throw new InvalidOperationException($"The display table is limited to {MaxDisplayRows} rows.");
            rom.WriteBytesAt(DisplayTableAddress(rom) + n * DisplayRow.Size, row.ToBytes());
            rom.WriteBytesAt(DisplayRowLimitImmediate, [(byte)(n + 1), 0x2A]);
            return n;
        }

        // ---- abilities ----------------------------------------------------------------------------------------
        public static int AbilityTableAddress(ROM rom) => FileOffset(U32(rom, AbilityLiterals[0]));
        public static bool AbilitiesRelocated(ROM rom) => AbilityTableAddress(rom) != OriginalAbilities;

        private static bool LooksLikeAbility(ROM rom, int a)
        {
            if (a + AbilityEntry.Size > rom.Length) return false;
            uint isReady = U32(rom, a + 8);
            return isReady is >= 0x08000000 and < 0x0A000000 && FileOffset(isReady) < rom.Length;
        }

        public static int AbilityCount(ROM rom)
        {
            int a = AbilityTableAddress(rom), n = 0;
            while (n < AbilityCapacity && LooksLikeAbility(rom, a + n * AbilityEntry.Size)) n++;
            return n;
        }

        public static List<AbilityEntry> ReadAbilities(ROM rom)
        {
            int a = AbilityTableAddress(rom), n = AbilityCount(rom);
            return Enumerable.Range(0, n).Select(i => AbilityEntry.FromBytes(rom.ReadBytesAt(a + i * AbilityEntry.Size, AbilityEntry.Size))).ToList();
        }

        public static void WriteAbility(ROM rom, int index, AbilityEntry e)
        {
            if (index < 0 || index >= AbilityCount(rom)) throw new ArgumentOutOfRangeException(nameof(index));
            rom.WriteBytesAt(AbilityTableAddress(rom) + index * AbilityEntry.Size, e.ToBytes());
        }

        /// <summary>Moves the ability table to free space with room for <see cref="AbilityCapacity"/> entries (2 code literals).</summary>
        public static int RelocateAbilityTable(ROM rom)
        {
            if (AbilitiesRelocated(rom)) return AbilityTableAddress(rom);
            int count = AbilityCount(rom);
            var block = new byte[AbilityCapacity * AbilityEntry.Size];
            rom.ReadBytesAt(AbilityTableAddress(rom), count * AbilityEntry.Size).CopyTo(block, 0);
            int addr = rom.AllocateFreeSpace(block.Length);
            rom.WriteBytesAt(addr, block);
            foreach (int lit in AbilityLiterals) W32(rom, lit, 0x08000000u | (uint)addr);
            return addr;
        }

        /// <summary>Appends an ability (relocating on first use). Its index is what a slot's abilityList stores.</summary>
        public static int AddAbility(ROM rom, AbilityEntry e)
        {
            RelocateAbilityTable(rom);
            int n = AbilityCount(rom);
            if (n >= AbilityCapacity) throw new InvalidOperationException($"The ability table is limited to {AbilityCapacity} entries.");
            rom.WriteBytesAt(AbilityTableAddress(rom) + n * AbilityEntry.Size, e.ToBytes());
            return n;
        }
    }

    /// <summary>
    /// Which sprite-record block each stock ability's animation reads. CERTAINTY: HIGH for the offsets (decompiled each
    /// animation's init: it does `entity+0x4C = spriteRecord + K`); MEDIUM for "missing block => crash/garbage" (not run in an emulator).
    /// Block K is 4 consecutive frame-descriptor pointers at record+K. Ability 7 (Energy Punch, anim 43) is unknown -> no requirement listed.
    /// </summary>
    public static class AbilityRequirements
    {
        public sealed record Need(int Animation, int EpCost, int BlockOffset);

        public static readonly IReadOnlyDictionary<int, Need> Stock = new Dictionary<int, Need>
        {
            [0] = new(46, 10, 156), [1] = new(47, 6, 156), [2] = new(34, 2, 156), [3] = new(48, 2, 364),
            [4] = new(45, 4, 156), [8] = new(44, 12, 156), [9] = new(49, 2, 364), [10] = new(51, 10, 380), [13] = new(50, 5, 364),
        };

        private const int CharacterSpriteIndex = 0x3B4E74;

        private static bool IsRomPtr(uint v, ROM r) => v is >= 0x08000000 and < 0x0A000000 && (int)(v & 0x00FFFFFF) + 12 <= r.Length;

        /// <summary>True when the sprite's record has 4 valid frame pointers at <paramref name="blockOffset"/>.</summary>
        public static bool SpriteHasBlock(ROM rom, int spriteId, int blockOffset)
        {
            uint rec = BitConverter.ToUInt32(rom.ReadBytesAt(FrameImport.IndexAddress(rom) + spriteId * 4, 4));
            if (!IsRomPtr(rec, rom)) return false;
            int r = (int)(rec & 0x00FFFFFF);
            for (int i = 0; i < 4; i++)
            {
                uint fp = BitConverter.ToUInt32(rom.ReadBytesAt(r + blockOffset + i * 4, 4));
                if (!IsRomPtr(fp, rom)) return false;
                var d = rom.ReadBytesAt((int)(fp & 0x00FFFFFF), 12);
                if (d[2] == 0 || d[3] == 0 || d[2] % 8 != 0 || d[3] % 8 != 0) return false;
            }
            return true;
        }

        /// <summary>Human-readable problems for a slot: every form reachable from its display row, times each assigned ability.</summary>
        public static List<string> Check(ROM rom, PartySlot slot, int slotIndex, IReadOnlyList<DisplayRow> rows)
        {
            var problems = new List<string>();
            var forms = new HashSet<int>();
            var queue = new Queue<int>([slot.DisplayIndex]);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                if (i < 0 || i >= rows.Count || !forms.Add(i)) continue;
                queue.Enqueue(rows[i].TransformedIndex); queue.Enqueue(rows[i].DetransformedIndex);
            }
            var table = RosterTables.ReadAbilities(rom);
            foreach (int a in slot.Abilities.Distinct())
            {
                int block = -1;
                if (a < table.Count && AbilityAnimations.Get(rom, table[a]) is int anim) block = AbilityAnimations.BlockOf(anim);
                else if (Stock.TryGetValue(a, out var need)) block = need.BlockOffset;
                if (block < 0) continue;
                string name = a < RosterTables.OriginalAbilityNames.Length ? RosterTables.OriginalAbilityNames[a] : $"Custom ability {a}";
                foreach (int f in forms.Order())
                    if (!SpriteHasBlock(rom, rows[f].SpriteId, block))
                        problems.Add($"Slot {slotIndex}: '{name}' needs frames at sprite-record +{block} but form row {f} (sprite {rows[f].SpriteId}) has none -- likely crash/garbled when used. (The Sprite editor tab imports PNGs into that block.)");
            }
            return problems;
        }
    }

    /// <summary>One verified, reversible byte patch.</summary>
    public sealed record BytePatch(string Name, string Description, int Offset, byte[] Original, byte[] Patched)
    {
        public bool IsApplied(ROM rom) => rom.ReadBytesAt(Offset, Patched.Length).AsSpan().SequenceEqual(Patched);
        public bool IsOriginal(ROM rom) => rom.ReadBytesAt(Offset, Original.Length).AsSpan().SequenceEqual(Original);

        public void Apply(ROM rom)
        {
            if (IsApplied(rom)) return;
            if (!IsOriginal(rom)) throw new InvalidOperationException($"'{Name}': the bytes at 0x{Offset:X} aren't what this patch expects (a different ROM version?).");
            rom.WriteBytesAt(Offset, Patched);
        }

        public void Revert(ROM rom)
        {
            if (IsOriginal(rom)) return;
            if (!IsApplied(rom)) throw new InvalidOperationException($"'{Name}': the bytes at 0x{Offset:X} are neither the original nor the patched form.");
            rom.WriteBytesAt(Offset, Original);
        }
    }

    public static class EnginePatches
    {
        /// <summary>
        /// Cell's defeat dialogue (Z14 A3, enemy stat 25) ends with "Hercule is now an available character!" then a script:
        /// PushByte 4; Step 91 (ClearCharacterInParty) -- which removes party slot 4 (Goku) for the rest of the game.
        /// Changing the step to 79 (SetCharacterInParty) keeps him. Verified against the ROM; not run in an emulator.
        /// </summary>
        public static readonly BytePatch KeepGokuAfterCell = new(
            "Keep Goku after Cell",
            "Cell's defeat script clears party slot 4 (Goku). This turns that Step 91 (ClearCharacterInParty) into Step 79 (SetCharacterInParty).",
            0x3EC738, [0x5B], [0x4F]);

        /// <summary>
        /// The debug menu (Music/Sample/Map Test) is unreachable in the shipped game. Idling 30 s at the title normally
        /// starts the attract video (BL GameIntroVideo_Create); this points that call at DebugMenu_Create instead.
        /// </summary>
        public static readonly BytePatch EnableDebugMenu = new(
            "Debug menu on title idle",
            "After 30 seconds idle at the title screen, open the built-in Music/Sample/Map Test menu instead of the attract video.",
            0x574, [0x1E, 0xF0, 0xE2, 0xFB], [0x05, 0xF0, 0x9C, 0xF8]);

        public static IReadOnlyList<BytePatch> All => [KeepGokuAfterCell, EnableDebugMenu];
    }

    /// <summary>
    /// The map table (g_MapEntries). Map_FindEntry scans it linearly up to Map_GetCount() (a constant in code), and the
    /// table's address lives in four code literals. Adding maps = relocate with spare room, patch those, bump the count.
    /// </summary>
    public static class MapTable
    {
        public const int OriginalAddress = 0x409368, EntrySize = 0x38, OriginalCount = 327, MaxCount = 510;
        public const int CountCode = 0xE0AC;                 // FF 20 xx 30 : MOVS R0,#0xFF ; ADDS R0,#imm (327 = 0xFF + 0x48)
        public static readonly int[] BaseLiterals = [0x5ADC, 0x5D5C, 0xEBD8];
        public const int NewGameEntryLiteral = 0x195C;        // &g_MapEntries[3]: where a new game starts
        public const int NewGameEntryIndex = 3;

        private static uint U32(ROM r, int o) => BitConverter.ToUInt32(r.ReadBytesAt(o, 4));
        private static void W32(ROM r, int o, uint v) => r.WriteBytesAt(o, BitConverter.GetBytes(v));

        public static int Address(ROM rom) => (int)(U32(rom, BaseLiterals[0]) & 0x00FFFFFF);
        public static bool Relocated(ROM rom) => Address(rom) != OriginalAddress;

        public static int Count(ROM rom)
        {
            var c = rom.ReadBytesAt(CountCode, 4);
            if (c[1] != 0x20 || c[3] != 0x30) throw new InvalidOperationException("Map_GetCount doesn't have the expected encoding at 0xE0AC.");
            return c[0] + c[2];
        }

        private static void SetCount(ROM rom, int n)
        {
            if (n is < 1 or > MaxCount) throw new ArgumentOutOfRangeException(nameof(n));
            int a = Math.Min(255, n), b = n - a;
            rom.WriteBytesAt(CountCode, [(byte)a, 0x20, (byte)b, 0x30]);
        }

        /// <summary>Moves the table to free space with room for <see cref="MaxCount"/> entries and repoints all four code literals.</summary>
        public static int Relocate(ROM rom)
        {
            if (Relocated(rom)) return Address(rom);
            int count = Count(rom);
            var block = new byte[MaxCount * EntrySize];
            Array.Fill(block, (byte)0xFF);                                   // unused entries never match a real (zone, area)
            rom.ReadBytesAt(Address(rom), count * EntrySize).CopyTo(block, 0);
            int addr = rom.AllocateFreeSpace(block.Length);
            rom.WriteBytesAt(addr, block);
            foreach (int lit in BaseLiterals) W32(rom, lit, 0x08000000u | (uint)addr);
            W32(rom, NewGameEntryLiteral, 0x08000000u | (uint)(addr + NewGameEntryIndex * EntrySize));
            return addr;
        }

        /// <summary>
        /// Appends a new map entry as a copy of <paramref name="sourceIndex"/> with its own (zone, area, variation).
        /// It SHARES the source's triggers/scripts/items/objects/variation data until you edit them, so change what
        /// you need in Dragon Radar afterwards. Returns the new entry's index.
        /// </summary>
        public static int AddEntryFromCopy(ROM rom, int sourceIndex, byte zone, byte area, byte variation)
        {
            int count = Count(rom);
            if (sourceIndex < 0 || sourceIndex >= count) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            if (count >= MaxCount) throw new InvalidOperationException($"The map table is limited to {MaxCount} entries.");
            Relocate(rom);
            int addr = Address(rom);
            var entry = rom.ReadBytesAt(addr + sourceIndex * EntrySize, EntrySize);
            entry[0] = zone; entry[1] = area; entry[2] = variation;
            rom.WriteBytesAt(addr + count * EntrySize, entry);
            SetCount(rom, count + 1);
            return count;
        }
    }
}
