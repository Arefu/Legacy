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

        [JsonConverter(typeof(HexIntConverter))]
        public int TileAtlasOffset { get; set; }

        [JsonConverter(typeof(HexIntConverter))]
        public int DefaultCharStats { get; set; }

        [JsonConverter(typeof(HexIntConverter))]
        public int DefaultExpThresholds { get; set; }

        public class HexIntConverter : JsonConverter<int>
        {
            public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var str = reader.GetString();
                if (str != null && str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt32(str, 16);
                return reader.GetInt32();
            }

            public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            {
                writer.WriteStringValue($"0x{value:X}");
            }
        }
    }
}