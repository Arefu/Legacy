using DrGero.IO;

namespace DrGero.Types
{
    public class MapEntry
    {
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