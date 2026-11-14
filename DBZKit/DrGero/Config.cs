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

        // The 256-colour palette the title screen, save select, pause menu and character select load (IDA: g_MenuPalette). 0 = unknown.
        [JsonConverter(typeof(HexIntConverter))]
        public int MenuPaletteOffset { get; set; }

        public List<UiImage> UiImages { get; set; } = [];

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

        // Bytes per quest record: 16 in US/Japan ({priority, available, complete, name}); 32 in EU LoG2, where the name is
        // followed by the other four languages' names. The fields up to the first name are the same in all of them.
        public int QuestRecordSize { get; set; } = 16;

        // ---- Asset tables (ROM file offsets). 0 / empty = this game doesn't have it or it isn't known yet; the asset catalog
        // then leaves that category out instead of guessing. Same engine across LoG1, LoG2 and Buu's Fury, so most of these exist
        // in every game; only the addresses differ.

        // Portrait_Table: PortraitCount pointers to ResourceHeader blocks, each 64x64 8bpp linear (OBJ palette).
        [JsonConverter(typeof(HexIntConverter))]
        public int PortraitTableOffset { get; set; }
        public int PortraitCount { get; set; }

        // Scouter database (IDA: g_ScouterDatabase, DBZ_ScouterEntry): ScouterCount 20-byte entries {char* name; char* description;
        // s32 spriteId; s32 portraitIndex; s32 statIndex}. statIndex points into ScouterStatsOffset (g_ScouterStatDatabase, 24-byte
        // entries: s32 hp at +4, u8 str/pow/end at +0xC). 0 = not known for this game.
        [JsonConverter(typeof(HexIntConverter))]
        public int ScouterTableOffset { get; set; }

        public int ScouterCount { get; set; }

        [JsonConverter(typeof(HexIntConverter))]
        public int ScouterStatsOffset { get; set; }

        // g_ItemsInGame: ItemCount 0x38-byte records; sprite ResourceHeader pointers at +0x14/+0x24/+0x28/+0x2C.
        [JsonConverter(typeof(HexIntConverter))]
        public int ItemTableOffset { get; set; }
        public int ItemCount { get; set; }

        // Engine functions the ROM's DATA names by address (Thumb bit set, as file offsets): a tiled BG layer starts with its draw
        // function, and a map script record starts with its spawn handler. They move with every build, so each game lists its own.
        // The defaults are the US LoG2 values, so a Game made in code without a profile behaves as before.
        [JsonConverter(typeof(HexIntConverter))]
        public int TiledLayerHandler { get; set; } = 0x0056A1;      // the tiled-grid BG layer format

        // The other two layer formats. 0 = not known for this game, so such layers are reported as unsupported.
        [JsonConverter(typeof(HexIntConverter))]
        public int ScrollLayerHandler { get; set; }                 // ScrollLayer_Create: one repeating 32x32 tilemap that drifts (rain, clouds, water); 0x4FCD in US

        [JsonConverter(typeof(HexIntConverter))]
        public int WorldMapLayerHandler { get; set; }               // WorldMapLayer_Create: grid of 8-bit affine chunks (the overworld); 0x12705 in US

        [JsonConverter(typeof(HexIntConverter))]
        public int TwoFrameLayerHandler { get; set; }               // TwoFrameLayer_Init: one screen, two 32x32 tilemaps {handler, frame0, frame1}; 0x12841 in US

        // Pause-menu area map: address of {s32 count; Region** regions}. 0 = not known for this game.
        [JsonConverter(typeof(HexIntConverter))]
        public int AreaMapRegionsOffset { get; set; }

        [JsonConverter(typeof(HexIntConverter))]
        public int CharacterSpawnHandler { get; set; } = 0x00D6DF;  // MapScript_CreateCharacter

        [JsonConverter(typeof(HexIntConverter))]
        public int NpcSpawnHandler { get; set; } = 0x00B711;        // MapScript_CreateNpcSprite: talkable, solid town NPCs

        // HUD ability icons: raw 512-byte 32x16 8bpp tiled images (ready + cooling-down state).
        public List<AbilityIcon> AbilityIcons { get; set; } = [];

        // Languages the game's text exists in. A single-language game has one entry; a multi-language release lists each one
        // with its own string tables. MapNameTableOffset above stays the default (first) language for older callers.
        public List<GameLanguage> Languages { get; set; } = [];

        // Readers that still use hard-coded US (ALFE) addresses inside DrGero. A game lists a layout here only when those exact
        // addresses are right for it, so the catalog never runs an ALFE-only reader on another ROM. Remove an entry once its
        // reader takes its addresses from this profile.
        //   "ALFE-audio"   AudioTables (SFX/music/instrument tables)       "ALFE-sprites" FrameImport (character sprite index literal)
        //   "ALFE-boot"    DeveloperIntro / GameIntroVideo / TitleScreenBall / DeveloperCredits
        public List<string> Layouts { get; set; } = [];

        public bool HasLayout(string layout) => Layouts.Contains(layout, StringComparer.OrdinalIgnoreCase);

        // Numbered assets (sprites, portraits, items) are shown by number only. Names would need translating per language, so the
        // only names tools show are the ROM's own text (e.g. map names, per language).

        /// <summary>A copy for a second user of the same game (e.g. a background thread): DrGero's render caches are keyed per Game instance.</summary>
        public Game Clone()
        {
            var copy = (Game)MemberwiseClone();
            copy.AbilityIcons = [.. AbilityIcons];
            copy.Languages = [.. Languages];
            copy.Layouts = [.. Layouts];
            return copy;
        }

        /// <summary>
        /// A menu/front-end picture the game loads by address in code: a compressed resource that unpacks to a flat 8bpp bitmap
        /// of the given width (the game decompresses it into a canvas, draws text on it and converts it to tiles).
        /// </summary>
        public class UiImage
        {
            public string Name { get; set; } = "";

            [JsonConverter(typeof(HexIntConverter))]
            public int Address { get; set; }

            public int Width { get; set; }

            /// <summary>"menu" (MenuPaletteOffset), "bg" or "obj".</summary>
            public string Palette { get; set; } = "menu";

            /// <summary>
            /// How the bytes are laid out: "bitmap" (row by row, the default), "tiles" (8x8 8bpp tiles, Width/8 to a row; a tile set the
            /// game copies straight to VRAM) or "tilemap" (u16 BG map entries, Width entries to a row; shown as data).
            /// </summary>
            public string Layout { get; set; } = "bitmap";

            /// <summary>One line on what it is or where the game uses it.</summary>
            public string Note { get; set; } = "";
        }

        public class AbilityIcon
        {
            public string Name { get; set; } = "";

            [JsonConverter(typeof(HexIntConverter))]
            public int Ready { get; set; }

            [JsonConverter(typeof(HexIntConverter))]
            public int Cooling { get; set; }
        }

        public class GameLanguage
        {
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";

            // Address of this language's pointer for string 0. String i's pointer is at MapNameTableOffset + i * StringStride * 4.
            [JsonConverter(typeof(HexIntConverter))]
            public int MapNameTableOffset { get; set; }

            // Pointers per string in the table. 1 = one language per table (US, Japan). Multi-language releases interleave every
            // language's pointer for a string (EU LoG2: en, fr, de, it, es -> stride 5, and each language starts 4 bytes later).
            public int StringStride { get; set; } = 1;

            // "latin1": one byte per character (US/EU). "u16": 16-bit characters, values below 0x80 are ASCII and the rest are
            // font glyph numbers (Japan); the glyph table isn't mapped yet, so tools show those as {XXXX}.
            public string Encoding { get; set; } = "latin1";

            /// <summary>File offset of the pointer to string <paramref name="index"/> in this language.</summary>
            public int StringPointerAddress(uint index) => MapNameTableOffset + (int)index * Math.Max(1, StringStride) * 4;
        }

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