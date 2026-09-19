using DrGero.IO;

namespace DrGero.Types
{
    public class MapEntry
    {
        public const int RecordSize = 0x38;

        public byte Zone { get; set; }
        public byte Area { get; set; }
        public byte Variation { get; set; }
        public byte TriggerCount { get; set; }
        public byte ScriptCount { get; set; }
        public byte ItemCount { get; set; }
        public byte ObjectCount { get; set; }
        public byte NpcCount { get; set; }
        public uint Flags { get; set; }
        public uint MapNameIndex { get; set; }
        public int MapTriggers { get; set; }
        public int MapScripts { get; set; }
        public int MapItems { get; set; }
        public int MapObjects { get; set; }
        public int NpcArray { get; set; }
        public uint MusicId { get; set; }
        public int VariationScript { get; set; }
        public int VariationArray { get; set; }
        public int EntryScript { get; set; }
        public int ExitScript { get; set; }

        public string Name { get; set; } = string.Empty;

        // Renamed from "Npc" — confirmed via IDA (LevelGate_Create, formerly
        // misnamed NPCObject_Create by an earlier session) that MapEntry's
        // npcArray does NOT hold walking characters. Its constructed entity
        // only ever renders a floating number ('?' or a level requirement) via
        // TextRenderer_DrawFmt, matching the game's field barriers that require
        // a minimum character level to break. Real walking/talking NPCs are
        // spawned from MapScripts instead (see EntityKind.Character).
        public enum EntityKind
        {
            LevelGate,
            Object,
            SpawnScript,
            Trigger,
            Character,
            Item,
            Decoration,
            Enemy
        }

        // Width/Height default to 0 for point-like entities (level gates, objects,
        // spawn scripts, characters). Triggers populate them so callers can draw
        // the actual zone rectangle instead of a single dot. X/Y remain the
        // top-left corner for anything with a nonzero Width/Height, and the
        // exact position for point-like entities.
        //
        // SourceAddress identifies the entity's *record* (used for labeling/
        // debugging and as the default write-back target). PositionAddress is
        // where X/Y actually live in ROM and is what an editor should patch when
        // moving an entity -- for most kinds these are the same address, but
        // triggers store their identity in the mapTriggers vtable+dataPtr slot
        // while their x1/y1/x2/y2 coordinates live inside the dataPtr target.
        public readonly record struct Entity(EntityKind Kind, int X, int Y, int TypeId, int SourceAddress, int Width = 0, int Height = 0, int PositionAddress = 0, int OnPickup = 0, int CollectionMsg = 0, int StatIndex = 0)
        {
            public int PositionAddress { get; init; } = PositionAddress == 0 ? SourceAddress : PositionAddress;
        }

        /// <summary>
        /// Re-reads this entry's ROM record IN PLACE (same object, so the map tree's Tags and
        /// any held references stay valid). Needed after anything that patches a map entry's
        /// bytes -- e.g. EntityWriter.PersistNewObjects rewrites mapObjects/objectCount -- since
        /// the parsed copy never sees those writes on its own: a reload through the stale
        /// copy reads the OLD array and the new object appears to have vanished.
        /// </summary>
        public void RefreshFrom(ROM rom, int recordAddress)
        {
            rom.PushPosition(recordAddress);
            var fresh = Read(rom);
            rom.PopPosition();

            Zone = fresh.Zone; Area = fresh.Area; Variation = fresh.Variation;
            TriggerCount = fresh.TriggerCount; ScriptCount = fresh.ScriptCount;
            ItemCount = fresh.ItemCount; ObjectCount = fresh.ObjectCount; NpcCount = fresh.NpcCount;
            Flags = fresh.Flags; MapNameIndex = fresh.MapNameIndex;
            MapTriggers = fresh.MapTriggers; MapScripts = fresh.MapScripts; MapItems = fresh.MapItems;
            MapObjects = fresh.MapObjects; NpcArray = fresh.NpcArray; MusicId = fresh.MusicId;
            VariationScript = fresh.VariationScript; VariationArray = fresh.VariationArray;
            EntryScript = fresh.EntryScript; ExitScript = fresh.ExitScript;
            // Name is looked up elsewhere from MapNameIndex, not stored in the record -- untouched.
        }

        public static MapEntry Read(ROM rom)
        {
            return new MapEntry
            {
                Zone = (byte)rom.ReadByte(),
                Area = (byte)rom.ReadByte(),
                Variation = (byte)rom.ReadByte(),
                TriggerCount = (byte)rom.ReadByte(),
                ScriptCount = (byte)rom.ReadByte(),
                ItemCount = (byte)rom.ReadByte(),
                ObjectCount = (byte)rom.ReadByte(),
                NpcCount = (byte)rom.ReadByte(),
                Flags = (uint)rom.ReadInt(),
                MapNameIndex = (uint)rom.ReadInt(),
                MapTriggers = rom.ReadPointer(),
                MapScripts = rom.ReadPointer(),
                MapItems = rom.ReadPointer(),
                MapObjects = rom.ReadPointer(),
                NpcArray = rom.ReadPointer(),
                MusicId = (uint)rom.ReadInt(),
                VariationScript = rom.ReadPointer(),
                VariationArray = rom.ReadPointer(),
                EntryScript = rom.ReadPointer(),
                ExitScript = rom.ReadPointer(),
            };
        }
    }
}