using DrGero;
using DrGero.Config;
using DrGero.IO;
using System.Text;

namespace Bulma
{
    public static class GameFactory
    {
        public static IGame? Detect(ROM rom, IEnumerable<Game> configs)
        {
            // GBA game code is at 0xAC, 4 bytes
            rom.Seek(0xAC);
            string code = Encoding.ASCII.GetString([(byte)rom.ReadByte(), (byte)rom.ReadByte(), (byte)rom.ReadByte(), (byte)rom.ReadByte()]);

            var config = configs.FirstOrDefault(g => g.GAME_CODE.Equals(code, StringComparison.OrdinalIgnoreCase));

            if (config == null) return null;

            return code switch
            {
                "ALFE" => new LOG2.DBZLOG2(config),
                _ => null
            };
        }
    }
}