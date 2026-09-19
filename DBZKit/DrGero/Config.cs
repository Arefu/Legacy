using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrGero.Config
{
    public class Game
    {
        public string GAME_CODE { get; set; } = "";

        [JsonConverter(typeof(HexIntConverter))]
        public int MapEntriesOffset { get; set; }

        public int MapEntryCount { get; set; }

        [JsonConverter(typeof(HexIntConverter))]
        public int MapNameTableOffset { get; set; }

        [JsonConverter(typeof(HexIntConverter))]
        public int BGPaletteOffset { get; set; }

        [JsonConverter(typeof(HexIntConverter))]
        public int OBJPaletteOffset { get; set; }

        // g_CharacterSpriteIndex -- CONFIRMED via IDA 2026-09 (Character_GetSpriteId
        // @0x8009324): for spriteId>=7, a direct array of per-character record pointers,
        // indexed by spriteId. See CharacterIconReader for the (partly experimental) rest
        // of the chain from a record pointer to real decoded pixels.
        [JsonConverter(typeof(HexIntConverter))]
        public int CharacterSpriteIndexOffset { get; set; }

        [JsonConverter(typeof(HexIntConverter))]
        public int TileAtlasOffset { get; set; }

        [JsonConverter(typeof(HexIntConverter))]
        public int DefaultCharStats { get; set; }

        [JsonConverter(typeof(HexIntConverter))]
        public int DefaultExpThresholds { get; set; }

        // g_QuestEntries -- CONFIRMED via IDA 2026-09 (QuestLog_Build @0x80049FC): a fixed
        // array of QuestCount 16-byte QuestEntry records (Priority, isAvailableScript,
        // isCompleteScript, EntryName). See DrGero.Quests.QuestReader for the rest of the
        // chain (decoding the tiny condition scripts into readable flag checks).
        [JsonConverter(typeof(HexIntConverter))]
        public int QuestTableOffset { get; set; }

        public int QuestCount { get; set; }

        public class HexIntConverter : JsonConverter<int>
        {
            public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var str = reader.GetString();
                    if (str == null)
                        throw new JsonException("Expected a non-null string for int conversion.");

                    if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        str = str[2..];

                    return Convert.ToInt32(str, 16);
                }

                return reader.GetInt32();
            }

            public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            {
                writer.WriteStringValue($"0x{value:X}");
            }
        }
    }
}