using DrGero.IO;
using Game = DrGero.Config.Game;

namespace DrGero
{
    /// <summary>
    /// Names for enemies and NPCs, read live from the ROM's Scouter database (IDA: g_ScouterDatabase, 20-byte entries
    /// {char* name; char* description; s32 spriteId; s32 portraitIndex; s32 statIndex}). Nothing is hard-coded or translated:
    /// rename a scouter entry and every tool shows the new name. Read on every call (no cache), so an edit shows at once.
    /// </summary>
    public static class ScouterNames
    {
        private const int EntrySize = 20;

        /// <summary>The name of scouter entry <paramref name="index"/>, or null when it's out of range or has no name.</summary>
        public static string? Entry(ROM rom, Game game, int index)
        {
            if (game.ScouterTableOffset == 0 || index < 0 || index >= game.ScouterCount) return null;
            int at = game.ScouterTableOffset + index * EntrySize;
            if (at + EntrySize > rom.Length) return null;
            return CString(rom, U32(rom, at));
        }

        /// <summary>The name of the first scouter entry drawn with sprite <paramref name="spriteId"/> (how the game ties an NPC
        /// to a scan), or null when no entry uses that sprite.</summary>
        public static string? ForSprite(ROM rom, Game game, int spriteId)
        {
            if (game.ScouterTableOffset == 0) return null;
            for (int i = 0; i < game.ScouterCount; i++)
            {
                int at = game.ScouterTableOffset + i * EntrySize;
                if (at + EntrySize > rom.Length) break;
                if ((int)U32(rom, at + 8) != spriteId) continue;
                if (CString(rom, U32(rom, at)) is { Length: > 0 } name) return name;
            }
            return null;
        }

        /// <summary>The name for an enemy with stat entry <paramref name="statIndex"/> and sprite <paramref name="spriteId"/>: the
        /// scouter entry whose own statIndex (+0x10) and spriteId (+0x08) both match, else one matching the stat, else the sprite.
        /// (The stat table's +0x08 word is NOT a scouter index: it agrees with these links for 1 of 122 entries, and named
        /// first-form Cell "Ninja".) Pass spriteId -1 when there's no sprite to go by.</summary>
        public static string? ForEnemy(ROM rom, Game game, int statIndex, int spriteId)
        {
            if (game.ScouterTableOffset == 0 || statIndex <= 0) return spriteId >= 0 ? ForSprite(rom, game, spriteId) : null;
            string? statOnly = null;
            for (int i = 0; i < game.ScouterCount; i++)
            {
                int at = game.ScouterTableOffset + i * EntrySize;
                if (at + EntrySize > rom.Length) break;
                if ((int)U32(rom, at + 16) != statIndex) continue;
                if (CString(rom, U32(rom, at)) is not { Length: > 0 } name) continue;
                if ((int)U32(rom, at + 8) == spriteId) return name;
                statOnly ??= name;
            }
            return statOnly ?? (spriteId >= 0 ? ForSprite(rom, game, spriteId) : null);
        }

        private static uint U32(ROM rom, int a) => BitConverter.ToUInt32(rom.ReadBytesAt(a, 4));

        private static string? CString(ROM rom, uint pointer)
        {
            if (pointer is < 0x08000000 or >= 0x0A000000) return null;
            int s = (int)(pointer & 0x01FFFFFF);
            if (s >= rom.Length) return null;
            var raw = rom.RawBytes;
            int end = s;
            while (end < rom.Length && end - s < 64 && raw[end] != 0) end++;
            var chars = new char[end - s];
            int n = 0;
            for (int i = s; i < end; i++) if (raw[i] >= 0x20) chars[n++] = (char)raw[i]; // Latin-1, like the Scouter tab
            string name = new string(chars, 0, n).Trim();
            return name.Length > 0 ? name : null;
        }
    }
}
