using DrGero.Config;
using DrGero.IO;
using DrGero.Types;

namespace DrGero
{
    public interface IGame
    {
        Game Config { get; }
        IReadOnlyList<MapEntry> MapEntries { get; }
        void Load(ROM rom);
        void DumpMapEntries(string outputPath);
    }
}