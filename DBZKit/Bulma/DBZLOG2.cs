using DrGero;
using DrGero.Config;
using DrGero.IO;
using DrGero.Types;
using System.Text;

namespace Bulma.LOG2
{
    public class DBZLOG2 : IGame
    {
        public Game Config { get; }
        public IReadOnlyList<MapEntry> MapEntries => _mapEntries;
        private readonly List<MapEntry> _mapEntries = [];

        public DBZLOG2(Game config)
        {
            Config = config;
        }

        public void Load(ROM rom)
        {
            _mapEntries.Clear();

            rom.Seek(Config.MapEntriesOffset);
            for (int i = 0; i < Config.MapEntryCount; i++)
            {

                var entry = MapEntry.Read(rom);
                entry.Name = ReadMapName(rom, entry.MapNameIndex);
                _mapEntries.Add(entry);
            }


        }

        public void DumpMapEntries(string outputPath)
        {
            const uint GBA_BASE = 0x08000000;
            var sb = new StringBuilder();
            for (int i = 0; i < _mapEntries.Count; i++)
            {
                var e = _mapEntries[i];
                sb.AppendLine($"[{i:D3}] Z{e.Zone}A{e.Area}v{e.Variation} | {e.Name} | Triggers:{e.TriggerCount}...");
                sb.AppendLine($"Scripts:{e.ScriptCount} Items:{e.ItemCount} Objects:{e.ObjectCount} NPCs:{e.NpcCount}");
                sb.AppendLine($"       Flags:0x{e.Flags:X8} NameIdx:0x{e.MapNameIndex:X} Music:0x{e.MusicId:X}");
                sb.AppendLine($"       Triggers:0x{(e.MapTriggers > 0 ? e.MapTriggers | GBA_BASE : 0):X} Scripts:0x{(e.MapScripts > 0 ? e.MapScripts | GBA_BASE : 0):X} Items:0x{(e.MapItems > 0 ? e.MapItems | GBA_BASE : 0):X}");
                sb.AppendLine($"       Objects:0x{(e.MapObjects > 0 ? e.MapObjects | GBA_BASE : 0):X} NPCs:0x{(e.NpcArray > 0 ? e.NpcArray | GBA_BASE : 0):X}");
                sb.AppendLine($"       VariationArray:0x{(e.VariationArray > 0 ? e.VariationArray | GBA_BASE : 0):X} VariationScript:0x{(e.VariationScript > 0 ? e.VariationScript | GBA_BASE : 0):X}");
                sb.AppendLine($"       EntryScript:0x{(e.EntryScript > 0 ? e.EntryScript | GBA_BASE : 0):X} ExitScript:0x{(e.ExitScript > 0 ? e.ExitScript | GBA_BASE : 0):X}");
                sb.AppendLine();
            }
            File.WriteAllText(outputPath, sb.ToString());
        }

        private string ReadMapName(ROM rom, uint nameIndex)
        {
            if (nameIndex == 0) return string.Empty;
            if (nameIndex > 0x0000FFFF) return string.Empty; // likely a pointer, not an index

            rom.PushPosition(Config.MapNameTableOffset + (int)(nameIndex * 4));
            int ptr = rom.ReadPointer();
            if (ptr == 0)
            {
                rom.PopPosition();
                return string.Empty;
            }
            rom.Seek(ptr);
            string name = rom.ReadNullTerminatedString();
            rom.PopPosition();
            return name;
        }
    }
}