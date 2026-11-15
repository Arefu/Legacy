using System.Drawing;
using System.Globalization;
using System.Text;
using DrGero.Boot;
using DrGero.Config;
using DrGero.Engine;
using DrGero.IO;
using DrGero.Rendering;
using DrGero.Types;

namespace DrGero.Catalog
{
    // Every asset in a ROM, by kind, for browsers and dumpers. UI-free: DBZKit's ROM Browser shows it, other tools can reuse it.
    //
    // Two passes:
    //   Build  walks the game's own tables (from the game profile, games/<CODE>.json) and lists every asset. It also scans the whole
    //          ROM for ResourceHeader blocks ({u32 mode, u32 size, payload}; mode 1/2 = JCALG1), accepting a block only if it is
    //          pointed to and decompresses to exactly `size` bytes with a proper end code.
    //   Index  runs every asset's reader once with ROM.ResourceAccessed hooked, so each block learns which asset uses it.
    //          Blocks nobody claims are the "unclassified" ones still to be identified. Mode-0 (stored) blocks can't be told
    //          apart from random data by a scan, so they are only listed once a reader actually uses one.
    //
    // Readers that still use hard-coded US addresses only run when the game profile lists their layout (Game.Layouts).

    public enum AssetKind { Image, Audio, Text, Resource }

    public sealed record AudioClip(short[] Pcm, int Channels, int Rate);

    /// <summary>What an asset looks like, made on demand. Owns its bitmaps (copies, never a renderer's cached ones).</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    public sealed class AssetPreview : IDisposable
    {
        public List<(string Label, Bitmap Image)> Images { get; } = [];
        public byte[]? Raw { get; set; }
        public string RawExtension { get; set; } = "bin";
        public AudioClip? Audio { get; set; }
        public string? Text { get; set; }

        public AssetPreview Add(string label, Bitmap image, bool copy = false)
        {
            Images.Add((label, copy ? new Bitmap(image) : image));
            return this;
        }

        public void Dispose()
        {
            foreach (var (_, image) in Images) image.Dispose();
            Images.Clear();
        }
    }

    public sealed class AssetNode
    {
        public string Name { get; init; } = "";
        /// <summary>File offset of the asset's record or block, or -1 for a group.</summary>
        public int Address { get; init; } = -1;
        public AssetKind Kind { get; init; }
        public string Info { get; init; } = "";
        /// <summary>Labelled facts shown in the details panel ("Size" = "64 x 64"). Info is shown under them as a short note.</summary>
        public List<(string Label, string Value)> Details { get; init; } = [];
        /// <summary>The asset's number in its own table (sprite id, portrait index, ...), for actions that open an editor on it. -1 = none.</summary>
        public int Id { get; init; } = -1;
        public List<AssetNode> Children { get; } = [];
        /// <summary>
        /// Other versions of this same asset (a map's alternate layouts), picked from a Variation box instead of listed as rows.
        /// This node is variation 1; Variants[0] is variation 2, and so on. They are indexed and exported like any other node.
        /// </summary>
        public List<AssetNode> Variants { get; } = [];
        public Func<ROM, Game, AssetPreview>? Preview { get; init; }
        /// <summary>Run <see cref="Preview"/> while indexing to learn which blocks it decodes.</summary>
        public bool Index { get; init; } = true;
        /// <summary>
        /// Optional: decodes exactly the blocks <see cref="Preview"/> would, without drawing anything. Indexing uses it instead of the
        /// preview when set (drawing through GDI+ is most of the cost and doesn't parallelise).
        /// </summary>
        public Action<ROM, Game>? IndexWith { get; init; }
        /// <summary>ROM bytes the asset owns that aren't ResourceHeader blocks (PCM, raw icons, tables).</summary>
        public List<(int Start, int Length)> Ranges { get; } = [];
        public string Category { get; internal set; } = "";

        public IEnumerable<AssetNode> SelfAndDescendants()
        {
            yield return this;
            foreach (var variant in Variants)
                yield return variant;
            foreach (var child in Children)
                foreach (var node in child.SelfAndDescendants())
                    yield return node;
        }
    }

    public sealed class AssetCategory(string name)
    {
        public string Name { get; } = name;
        public List<AssetNode> Nodes { get; } = [];
        /// <summary>Shown when the category is empty, e.g. why it isn't available for this game.</summary>
        public string? Note { get; set; }
        public int Count => Nodes.Sum(n => n.SelfAndDescendants().Count(d => d.Preview != null));
    }

    public sealed class ResourceBlock
    {
        public int Address { get; init; }
        public int End { get; init; }
        public int Mode { get; init; }
        public int Size { get; init; }
        public List<int> Referrers { get; init; } = [];
        public AssetNode? Owner { get; set; }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    public sealed class AssetCatalog
    {
        public const string Unclassified = "Resources";

        public Game Game { get; }
        public List<AssetCategory> Categories { get; } = [];
        public List<ResourceBlock> Blocks { get; private set; } = [];
        public bool Indexed { get; private set; }

        private AssetCatalog(Game game) => Game = game;

        // ---- Build --------------------------------------------------------------------------------------------------------------

        public static AssetCatalog Build(ROM rom, Game game, CancellationToken cancel = default)
        {
            var catalog = new AssetCatalog(game);
            var entries = ReadMapEntries(rom, game);

            catalog.Add("Sound", AddSound, rom, game);
            catalog.Add("Characters", AddCharacters, rom, game);
            catalog.Add("Portraits", AddPortraits, rom, game);
            catalog.Add("Items", AddItems, rom, game);
            catalog.Add("Ability icons", AddAbilityIcons, rom, game);
            catalog.Add("Maps", (c, r, g) => AddMaps(c, r, g, entries), rom, game);
            catalog.Add("Objects", (c, r, g) => AddObjects(c, r, g, entries), rom, game);
            catalog.Add("Scouter", AddScouter, rom, game);
            catalog.Add("Area maps", AddAreaMaps, rom, game);
            catalog.Add("Menus", AddMenus, rom, game);
            catalog.Add("Tile atlas", AddAtlas, rom, game);
            catalog.Add("Boot screens", AddBootScreens, rom, game);
            catalog.Add("Text", (c, r, g) => AddText(c, r, g, entries), rom, game);
            catalog.Categories.Add(new AssetCategory(Unclassified) { Note = "Indexing..." });

            cancel.ThrowIfCancellationRequested();
            catalog.Blocks = ScanBlocks(rom.ToArray());
            return catalog;
        }

        private void Add(string name, Action<AssetCategory, ROM, Game> provider, ROM rom, Game game)
        {
            var category = new AssetCategory(name);
            try { provider(category, rom, game); }
            catch (Exception ex) { category.Note = $"Couldn't read {name.ToLowerInvariant()}: {ex.Message}"; }
            foreach (var node in category.Nodes.SelectMany(n => n.SelfAndDescendants())) node.Category = name;
            Categories.Add(category);
        }

        private static string NotInProfile(Game game, string what) =>
            $"{what} aren't in the game profile for {(game.GAME_CODE.Length > 0 ? game.GAME_CODE : "this ROM")} yet (games/{game.GAME_CODE}.json).";

        private static bool IsRomPointer(uint value, ROM rom) => value is >= 0x08000000 and < 0x0A000000 && (int)(value & 0x01FFFFFF) < rom.Length;
        private static uint U32(ROM rom, int address) => BitConverter.ToUInt32(rom.ReadBytesAt(address, 4));

        // ---- Sound ----

        private static void AddSound(AssetCategory category, ROM rom, Game game)
        {
            if (!game.HasLayout("ALFE-audio") || !AudioTables.IsSupported(rom)) { category.Note = NotInProfile(game, "Sound tables"); return; }

            var sfx = new AssetNode { Name = "Sound effects" };
            for (int i = 0; i < AudioTables.SfxCount; i++)
            {
                var s = AudioTables.ReadSfx(rom, i);
                int index = i;
                var node = new AssetNode
                {
                    Name = $"SFX {i:D2}", Address = s.Data, Kind = AssetKind.Audio, Index = false, Id = i,
                    Details = [("Length", Seconds(s.Length, s.Rate)), ("Sample rate", $"{s.Rate} Hz"), ("Loops", s.Loop ? "Yes" : "No")],
                    Preview = (r, _) => PcmPreview(AudioTables.ReadPcm(r, AudioTables.ReadSfx(r, index).Data, AudioTables.ReadSfx(r, index).Length), AudioTables.ReadSfx(r, index).Rate),
                };
                node.Ranges.Add((s.Data, s.Length));
                node.Ranges.Add((s.Address, 12));
                sfx.Children.Add(node);
            }

            var songs = new AssetNode { Name = "Songs" };
            for (int i = 0; i < AudioTables.MusicCount; i++)
            {
                var t = AudioTables.ReadTrack(rom, i);
                int index = i;
                var node = new AssetNode
                {
                    Name = $"Song {i:D2}", Address = t.Address, Kind = AssetKind.Audio, Index = false, Id = i,
                    Details = [("Tempo", t.Tempo.ToString(CultureInfo.InvariantCulture)), ("Speed", t.Speed.ToString(CultureInfo.InvariantCulture))],
                    Info = "The first play takes a moment while the song is rendered.",
                    Preview = (r, _) =>
                    {
                        var song = AudioTables.ReadSong(r, index);
                        var rendered = SongRenderer.Render(r, index, song);
                        return new AssetPreview
                        {
                            Audio = new AudioClip(rendered.Pcm, 2, rendered.Rate),
                            Raw = Encoding.UTF8.GetBytes(AudioTables.SongToJson(song)), RawExtension = "json",
                        };
                    },
                };
                node.Ranges.Add((t.Address, AudioTables.MusicEntrySize));
                songs.Children.Add(node);
            }

            var instruments = new AssetNode { Name = "Instruments" };
            int bank = AudioTables.BankAddress(rom);
            for (int i = 0, n = AudioTables.BankSize(rom, bank); i < n; i++)
            {
                var s = AudioTables.ReadInstrument(rom, bank, i);
                int index = i;
                var node = new AssetNode
                {
                    Name = $"Instrument {i:D3}", Address = s.Data, Kind = AssetKind.Audio, Index = false, Id = i,
                    Details = [("Length", Seconds(s.Length, s.Rate)), ("Sample rate", $"{s.Rate} Hz"),
                               ("Loop", s.Loops ? (s.PingPong ? "Ping-pong" : $"From sample {s.LoopStart}") : "No")],
                    Preview = (r, _) =>
                    {
                        var x = AudioTables.ReadInstrument(r, AudioTables.BankAddress(r), index);
                        return PcmPreview(AudioTables.ReadPcm(r, x.Data, x.Length), x.Rate);
                    },
                };
                node.Ranges.Add((s.Data, s.Length));
                node.Ranges.Add((s.Address, 12));
                instruments.Children.Add(node);
            }

            category.Nodes.AddRange([sfx, songs, instruments]);
        }

        private static string Seconds(int samples, int rate) => rate > 0 ? $"{(double)samples / rate:0.00} s" : $"{samples} samples";

        private static AssetPreview PcmPreview(byte[] pcm, int rate) => new()
        {
            Audio = new AudioClip(pcm.Select(b => (short)((sbyte)b << 8)).ToArray(), 1, rate),
            Raw = pcm, RawExtension = "pcm",
        };

        // ---- Characters ----

        private static void AddCharacters(AssetCategory category, ROM rom, Game game)
        {
            if (game.CharacterSpriteIndexOffset == 0) { category.Note = NotInProfile(game, "Character sprites"); return; }

            foreach (int id in SpriteDump.SpriteIds(rom, game))
            {
                int spriteId = id;
                category.Nodes.Add(new AssetNode
                {
                    Name = $"Sprite {id:D3}",
                    Address = (int)(U32(rom, FrameImport.IndexAddress(rom, game) + id * 4) & 0x01FFFFFF),
                    Kind = AssetKind.Image, Id = id,
                    Details = [("Sprite", id.ToString(CultureInfo.InvariantCulture)), ("Animation groups", FrameImport.GroupsIn(rom, id, game).ToString(CultureInfo.InvariantCulture)), ("Palette", "OBJ")],
                    Info = game.HasLayout("ALFE-sprites") ? "" : "Frames can only be edited in the US ROM so far.",
                    Preview = (r, g) =>
                    {
                        var p = new AssetPreview();
                        foreach (var (label, _, _, image) in SpriteDump.RenderFrames(r, spriteId, g)) p.Add(label, image);
                        return p;
                    },
                    IndexWith = (r, g) => SpriteDecode(r, g, spriteId),
                });
            }
        }

        /// <summary>The same walk as SpriteDump.RenderFrames (every group, direction and frame), decoding the tiles without drawing.</summary>
        private static void SpriteDecode(ROM rom, Game game, int spriteId)
        {
            uint ptr = U32(rom, FrameImport.IndexAddress(rom, game) + spriteId * 4);
            if (!FrameImport.IsRomPtr(ptr, rom)) return;
            int record = (int)(ptr & 0x00FFFFFF);
            for (int g = 0; g < FrameImport.GroupsIn(rom, spriteId, game); g++)
                for (int slot = 0; slot < 4; slot++)
                {
                    uint sp = U32(rom, record + FrameImport.FirstGroup + g * FrameImport.GroupSize + slot * 4);
                    foreach (var d in FrameImport.ReadStockArray(rom, record, sp))
                        try { JCALG1.Decompress(rom, (int)(BitConverter.ToUInt32(d, 8) & 0x00FFFFFF)); } catch { }
                }
        }

        // ---- Portraits / items / ability icons ----

        private static void AddPortraits(AssetCategory category, ROM rom, Game game)
        {
            if (game.PortraitTableOffset == 0) { category.Note = NotInProfile(game, "Portraits"); return; }
            for (int i = 0; i < game.PortraitCount; i++)
            {
                uint p = U32(rom, game.PortraitTableOffset + i * 4);
                if (!IsRomPointer(p, rom)) continue;
                int address = (int)(p & 0x01FFFFFF);
                category.Nodes.Add(new AssetNode
                {
                    Name = $"Portrait {i:D3}", Address = address, Kind = AssetKind.Image, Id = i,
                    Details = [("Portrait", i.ToString(CultureInfo.InvariantCulture)), ("Size", "64 x 64"), ("Palette", "OBJ")],
                    Preview = (r, g) => SquareImage(JCALG1.Decompress(r, address), ItemIconReader.ReadOBJPalette(r, g), tiled: false),
                });
            }
            category.Nodes.FirstOrDefault()?.Ranges.Add((game.PortraitTableOffset, game.PortraitCount * 4));
        }

        private static readonly int[] ItemSpriteFields = [0x14, 0x24, 0x28, 0x2C];

        private static void AddItems(AssetCategory category, ROM rom, Game game)
        {
            if (game.ItemTableOffset == 0) { category.Note = NotInProfile(game, "Items"); return; }
            for (int i = 0; i < game.ItemCount; i++)
            {
                int record = game.ItemTableOffset + i * 0x38;
                var sprites = ItemSpriteFields.Select(f => (Field: f, Pointer: U32(rom, record + f))).Where(s => IsRomPointer(s.Pointer, rom)).ToList();
                if (sprites.Count == 0) continue;
                var node = new AssetNode
                {
                    Name = $"Item {i:D2}", Address = record, Kind = AssetKind.Image, Id = i,
                    Details = [("Item", i.ToString(CultureInfo.InvariantCulture)), ("Pictures", sprites.Count.ToString(CultureInfo.InvariantCulture)), ("Palette", "OBJ")],
                    Preview = (r, g) =>
                    {
                        var palette = ItemIconReader.ReadOBJPalette(r, g);
                        var p = new AssetPreview();
                        foreach (var (field, pointer) in sprites)
                        {
                            byte[] data = JCALG1.Decompress(r, (int)(pointer & 0x01FFFFFF));
                            p.Raw ??= data;
                            using var one = SquareImage(data, palette, tiled: false);
                            foreach (var (_, image) in one.Images) p.Add($"+0x{field:X2}", image, copy: true);
                        }
                        return p;
                    },
                };
                node.Ranges.Add((record, 0x38));
                category.Nodes.Add(node);
            }
        }

        private static void AddAbilityIcons(AssetCategory category, ROM rom, Game game)
        {
            if (game.AbilityIcons.Count == 0) { category.Note = NotInProfile(game, "Ability icons"); return; }
            foreach (var icon in game.AbilityIcons)
            {
                var states = new[] { ("ready", icon.Ready), ("cooling", icon.Cooling) }.Where(s => s.Item2 > 0 && s.Item2 + 512 <= rom.Length).Distinct().ToList();
                if (states.Count == 0) continue;
                var node = new AssetNode
                {
                    Name = icon.Name, Address = icon.Ready, Kind = AssetKind.Image, Index = false,
                    Details = [("Size", "32 x 16"), ("States", states.Count == 2 ? "Ready, cooling down" : "One image"), ("Palette", "OBJ")],
                    Preview = (r, g) =>
                    {
                        var palette = ItemIconReader.ReadOBJPalette(r, g);
                        var p = new AssetPreview { Raw = r.ReadBytesAt(icon.Ready, 512) };
                        foreach (var (label, address) in states) p.Add(label, DeveloperIntro.RenderIndexedTiled(r.ReadBytesAt(address, 512), 4, 2, palette));
                        return p;
                    },
                };
                foreach (int address in states.Select(s => s.Item2).Distinct()) node.Ranges.Add((address, 512)); // "Disabled" uses one image for both
                category.Nodes.Add(node);
            }
        }

        /// <summary>Square 8bpp image (linear or tiled) from decoded pixels, padded if the data runs short.</summary>
        private static AssetPreview SquareImage(byte[] data, Color[] palette, bool tiled)
        {
            int side = Math.Max(8, (int)Math.Sqrt(data.Length));
            var p = new AssetPreview { Raw = data };
            if (tiled) p.Add($"{side}x{side}", DeveloperIntro.RenderIndexedTiled(data, side / 8, side / 8, palette));
            else p.Add($"{side}x{side}", DeveloperIntro.RenderIndexed(Pad(data, side * side), side, side, palette));
            return p;
        }

        private static byte[] Pad(byte[] data, int length) => data.Length >= length ? data : [.. data, .. new byte[length - data.Length]];

        // ---- Maps / objects ----

        /// <summary>One map layout. Alt is its position in the entry's variation array: 0 is the usual layout, 1+ are the alternate states.</summary>
        private sealed record MapRef(int Index, MapEntry Entry, int MapOffset, string Name, int Alt = 0)
        {
            public string Label => $"Z{Entry.Zone}A{Entry.Area}v{Entry.Variation}" + (Alt > 0 ? $" alt {Alt}" : "") + (Name.Length > 0 ? $" {Name}" : "");
        }

        private static List<MapRef> ReadMapEntries(ROM rom, Game game)
        {
            var maps = new List<MapRef>();
            if (game.MapEntriesOffset == 0) return maps;
            int table = game.MapEntriesOffset, count = game.MapEntryCount;
            try
            {
                // A relocated/extended map table is found through the game's own code, as Bulma's loader does.
                if (game.HasLayout("ALFE-sprites") && RosterTables.IsSupportedRom(rom)) { table = MapTable.Address(rom); count = MapTable.Count(rom); }
            }
            catch (InvalidOperationException) { }

            var names = Languages(game).FirstOrDefault();
            var seen = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                rom.PushPosition(table + i * MapEntry.RecordSize);
                var entry = MapEntry.Read(rom);
                rom.PopPosition();
                int mapOffset = 0;
                if (entry.VariationArray > 0 && entry.VariationArray + 4 <= rom.Length)
                {
                    uint p = U32(rom, entry.VariationArray);
                    if (IsRomPointer(p, rom)) mapOffset = (int)(p & 0x01FFFFFF);
                }
                string name = ReadString(rom, names, entry.MapNameIndex);
                maps.Add(new MapRef(i, entry, mapOffset, name));
                if (mapOffset > 0) seen.Add(mapOffset);

                // The variation array has no count: the map's scripts pick an index. Every layout ends with a pointer to the shared
                // tile atlas (+0x4C), so the array is followed for as long as its entries point at something that does.
                for (int alt = 1; alt < 16 && mapOffset > 0 && game.TileAtlasOffset != 0; alt++)
                {
                    int at = entry.VariationArray + alt * 4;
                    if (at + 4 > rom.Length) break;
                    uint p = U32(rom, at);
                    int layout = (int)(p & 0x01FFFFFF);
                    if (!IsRomPointer(p, rom) || layout + 0x50 > rom.Length || (U32(rom, layout + 0x4C) & 0x01FFFFFF) != game.TileAtlasOffset) break;
                    if (seen.Add(layout)) maps.Add(new MapRef(i, entry, layout, name, alt));
                }
            }
            return maps;
        }

        private static string ReadString(ROM rom, Game.GameLanguage? language, uint index)
        {
            if (language == null || language.MapNameTableOffset == 0 || index == 0 || index > 0xFFFF) return "";
            int at = language.StringPointerAddress(index);
            if (at + 4 > rom.Length) return "";
            uint p = U32(rom, at);
            if (!IsRomPointer(p, rom)) return "";
            int o = (int)(p & 0x01FFFFFF);
            var sb = new StringBuilder();
            if (language.Encoding.Equals("u16", StringComparison.OrdinalIgnoreCase))
            {
                for (; o + 2 <= rom.Length && sb.Length <= 256; o += 2)
                {
                    int c = BitConverter.ToUInt16(rom.ReadBytesAt(o, 2));
                    if (c == 0) break;
                    sb.Append(c is >= 0x20 and < 0x7F ? ((char)c).ToString() : $"{{{c:X}}}");
                }
                return sb.Length <= 256 ? sb.ToString() : "";
            }
            for (; o < rom.Length && sb.Length <= 64; o++)
            {
                int c = rom.ReadBytesAt(o, 1)[0];
                if (c == 0) break;
                if (c is < 0x20 or (>= 0x7F and < 0xA0)) return "";
                sb.Append((char)c); // Latin-1: EU text has accented letters
            }
            return sb.Length <= 64 ? sb.ToString() : "";
        }

        /// <summary>The profile's languages with a known string table; a profile without a Languages list falls back to MapNameTableOffset.</summary>
        private static List<Game.GameLanguage> Languages(Game game) =>
            (game.Languages.Count > 0 ? game.Languages
                : [new Game.GameLanguage { Code = "", Name = "Default", MapNameTableOffset = game.MapNameTableOffset }])
            .Where(l => l.MapNameTableOffset != 0).ToList();

        private static IEnumerable<IGrouping<byte, MapRef>> ByZone(IEnumerable<MapRef> maps) => maps.GroupBy(m => m.Entry.Zone).OrderBy(g => g.Key);

        private static void AddMaps(AssetCategory category, ROM rom, Game game, List<MapRef> maps)
        {
            if (maps.Count == 0) { category.Note = NotInProfile(game, "Map entries"); return; }
            foreach (var zone in ByZone(maps))
            {
                var group = new AssetNode { Name = $"Zone {zone.Key}" };
                // One row per map entry; its alternate layouts are variants of that row (the Variation box), not rows of their own.
                var rows = new Dictionary<int, AssetNode>();
                foreach (var map in zone.Where(m => m.MapOffset > 0))
                {
                    var m = map;
                    var node = new AssetNode
                    {
                        Name = m.Label, Address = m.MapOffset, Kind = AssetKind.Image, Id = m.Index,
                        Details =
                        [
                            ("Map entry", $"#{m.Index}"), ("Zone / area", $"{m.Entry.Zone} / {m.Entry.Area}"),
                            ("Layout", m.Alt == 0 ? "Normal" : $"Alternate {m.Alt}"),
                            ("Music", m.Entry.MusicId > 0xFFFF ? $"0x{m.Entry.MusicId:X8}" : m.Entry.MusicId.ToString(CultureInfo.InvariantCulture)),
                            ("Triggers", m.Entry.TriggerCount.ToString(CultureInfo.InvariantCulture)), ("Scripts", m.Entry.ScriptCount.ToString(CultureInfo.InvariantCulture)),
                            ("Items", m.Entry.ItemCount.ToString(CultureInfo.InvariantCulture)), ("Objects", m.Entry.ObjectCount.ToString(CultureInfo.InvariantCulture)),
                        ],
                        Info = "Edit maps in Dragon Radar.",
                        Preview = (r, g) => MapPreview(r, g, m.MapOffset),
                        IndexWith = (r, g) => MapDecode(r, g, m.MapOffset),
                    };
                    if (rows.TryGetValue(m.Index, out var row)) row.Variants.Add(node);
                    else { rows[m.Index] = node; group.Children.Add(node); }
                }
                if (group.Children.Count > 0) category.Nodes.Add(group);
            }
        }

        /// <summary>
        /// Decodes every block a map render decodes (collision, tileset table, layer chunks, animated tile strips), drawing nothing.
        /// Tile atlas banks and graphic objects are left to their own categories.
        /// </summary>
        private static void MapDecode(ROM rom, Game game, int mapOffset)
        {
            CollisionEditor.Open(rom, mapOffset);
            uint shots = U32(rom, mapOffset + 0x38);
            if (IsRomPointer(shots, rom)) JCALG1.Decompress(rom, (int)(shots & 0x01FFFFFF));
            try { TilesetTable.ReadAtlasIndices(rom, mapOffset); } catch { /* a map without a tileset table */ }
            for (int layer = 0; layer < 4; layer++)
            {
                // A tiled layer (as MapRenderer.DrawLayer reads it): u32 type = its draw function (game.TiledLayerHandler, 0x56A1 in US),
                // ..., u8 columns at +0x14, u8 rows at +0x15, then columns*rows pointers to 32x32-tile chunk resources from +0x18.
                uint pointer = U32(rom, mapOffset + 0x14 + layer * 4);
                if (!IsRomPointer(pointer, rom)) continue;
                int at = (int)(pointer & 0x01FFFFFF);
                int handler = (int)(U32(rom, at) & 0x01FFFFFF); // stored as 0x080056A1; DrawLayer compares it after ReadPointer's mask
                if (handler == game.ScrollLayerHandler && handler != 0)
                {
                    uint tilemap = U32(rom, at + 0x1C);
                    if (IsRomPointer(tilemap, rom)) JCALG1.Decompress(rom, (int)(tilemap & 0x01FFFFFF));
                    continue;
                }
                if (handler == game.TwoFrameLayerHandler && handler != 0)
                {
                    foreach (int field in new[] { 4, 8 })
                    {
                        uint frame = U32(rom, at + field);
                        if (IsRomPointer(frame, rom)) JCALG1.Decompress(rom, (int)(frame & 0x01FFFFFF));
                    }
                    continue;
                }
                if (handler == game.WorldMapLayerHandler && handler != 0)
                {
                    var grid = rom.ReadBytesAt(at + 4, 2);
                    for (int c = 0; c < grid[0] * grid[1]; c++)
                    {
                        uint chunk = U32(rom, at + 8 + c * 4);
                        if (IsRomPointer(chunk, rom)) JCALG1.Decompress(rom, (int)(chunk & 0x01FFFFFF));
                    }
                    continue;
                }
                if (handler != game.TiledLayerHandler) continue;
                var size = rom.ReadBytesAt(at + 0x14, 2);
                for (int c = 0; c < size[0] * size[1]; c++)
                {
                    uint chunk = U32(rom, at + 0x18 + c * 4);
                    if (IsRomPointer(chunk, rom)) JCALG1.Decompress(rom, (int)(chunk & 0x01FFFFFF));
                }
            }
            int sequences = BitConverter.ToInt32(rom.ReadBytesAt(mapOffset + 0xC, 4));
            uint list = U32(rom, mapOffset + 0x10);
            if (sequences <= 0 || sequences > 64 || !IsRomPointer(list, rom)) return;
            for (int i = 0; i < sequences; i++)
            {
                uint sequence = U32(rom, (int)(list & 0x01FFFFFF) + i * 4);
                if (!IsRomPointer(sequence, rom)) continue;
                int s = (int)(sequence & 0x01FFFFFF), strips = rom.ReadBytesAt(s, 1)[0];
                for (int j = 0; j < strips; j++)
                {
                    uint strip = U32(rom, s + 4 + j * 4);
                    if (IsRomPointer(strip, rom)) JCALG1.DecompressUnknownHeader(rom, (int)(strip & 0x01FFFFFF));
                }
            }
        }

        private static AssetPreview MapPreview(ROM rom, Game game, int mapOffset)
        {
            var p = new AssetPreview();
            var (composite, tilesets, _, unsupported, collision) = MapRenderer.RenderMap(rom, game, mapOffset, new MapRenderOptions());
            p.Add(unsupported ? "map (some layers not supported yet)" : "map", composite);

            for (int layer = 0; layer < 4; layer++)
            {
                uint pointer = U32(rom, mapOffset + 0x14 + layer * 4);
                if (!IsRomPointer(pointer, rom)) continue;
                try
                {
                    var (bitmap, priority, layerUnsupported) = MapRenderer.DrawLayer(rom, game, (int)(pointer & 0x01FFFFFF), tilesets, []);
                    p.Add($"BG layer {layer} (priority {priority}){(layerUnsupported ? ", unsupported format" : "")}", bitmap, copy: true);
                }
                catch { /* the composite already shows what could be drawn */ }
            }

            int sheet = 0;
            foreach (var (range, image) in tilesets.OrderBy(t => t.Key.Start.Value))
                p.Add(sheet++ == 0 ? "tileset" : $"animated tiles {range}", image, copy: true);

            var (width, height) = MapResizer.ReadSize(rom, mapOffset);
            int cw = Math.Clamp((width + 7) / 8, 1, collision.GetLength(0)), ch = Math.Clamp((height + 7) / 8, 1, collision.GetLength(1));
            var shotMask = MapRenderer.ReadProjectileMask(rom, mapOffset);
            var walk = new byte[cw * ch];
            var shots = new byte[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    walk[y * cw + x] = (byte)(collision[x, y] ? 1 : 0);
                    shots[y * cw + x] = (byte)(shotMask[x, y] ? (collision[x, y] ? 1 : 2) : 0);
                }
            Color[] colours = [Color.Black, Color.White, Color.Orange];
            p.Add("collision (1 px per 8x8 tile, white = walkable)", IndexedImage.FromIndices(walk, cw, ch, colours, transparentZero: false));
            p.Add("projectile mask (white = walkable, orange = only shots can cross)", IndexedImage.FromIndices(shots, cw, ch, colours, transparentZero: false));
            p.Text = $"{width} x {height} px";
            return p;
        }

        private static void AddObjects(AssetCategory category, ROM rom, Game game, List<MapRef> maps)
        {
            if (maps.Count == 0) { category.Note = NotInProfile(game, "Map entries"); return; }
            foreach (var zone in ByZone(maps))
            {
                var group = new AssetNode { Name = $"Zone {zone.Key}" };
                foreach (var map in zone.Where(m => m.MapOffset > 0))
                {
                    var m = map;
                    int graphics = BitConverter.ToInt32(rom.ReadBytesAt(m.MapOffset + 0x24, 4));
                    int total = graphics + m.Entry.ItemCount + m.Entry.ObjectCount + m.Entry.TriggerCount + m.Entry.ScriptCount + m.Entry.NpcCount;
                    if (total <= 0) continue;
                    group.Children.Add(new AssetNode
                    {
                        Name = m.Label, Address = m.MapOffset, Kind = AssetKind.Image, Id = m.Index,
                        Details =
                        [
                            ("Map entry", $"#{m.Index}"), ("Graphic objects", Math.Max(0, graphics).ToString(CultureInfo.InvariantCulture)),
                            ("Items", m.Entry.ItemCount.ToString(CultureInfo.InvariantCulture)), ("Objects", m.Entry.ObjectCount.ToString(CultureInfo.InvariantCulture)),
                            ("Level gates", m.Entry.NpcCount.ToString(CultureInfo.InvariantCulture)), ("Triggers", m.Entry.TriggerCount.ToString(CultureInfo.InvariantCulture)),
                            ("Spawn scripts", m.Entry.ScriptCount.ToString(CultureInfo.InvariantCulture)),
                        ],
                        Info = "Edit objects in Dragon Radar.",
                        Preview = (r, g) => ObjectsPreview(r, g, m),
                    });
                }
                if (group.Children.Count > 0) category.Nodes.Add(group);
            }
        }

        private static AssetPreview ObjectsPreview(ROM rom, Game game, MapRef m)
        {
            var p = new AssetPreview();
            // RenderGraphicObjectSprites keys each sprite by its graphic-object record (file offset), matching the list below.
            foreach (var (key, image) in MapRenderer.RenderGraphicObjectSprites(rom, game, m.MapOffset).OrderBy(s => s.Key))
                p.Add(key > 0xFFFF ? $"graphic object 0x{0x08000000 | key:X8}" : $"graphic object {key}", image, copy: true);

            var text = new StringBuilder();
            void List(string what, Func<List<MapEntry.Entity>> read)
            {
                List<MapEntry.Entity> list;
                try { list = read(); } catch (Exception ex) { text.AppendLine($"{what}: couldn't read ({ex.Message})"); return; }
                text.AppendLine($"{what} ({list.Count})");
                foreach (var e in list)
                    text.AppendLine($"  {e.Kind,-11} type {e.TypeId,4}  at {e.X},{e.Y}{(e.Width > 0 ? $" size {e.Width}x{e.Height}" : "")}  record 0x{e.SourceAddress | 0x08000000:X8}");
                text.AppendLine();
            }
            List("Graphic objects", () => EntityReader.ReadGraphicObjects(rom, m.MapOffset));
            List("Items", () => EntityReader.ReadMapItems(rom, m.Entry, m.MapOffset));
            List("Objects", () => EntityReader.ReadObjectArray(rom, m.Entry));
            List("Level gates", () => EntityReader.ReadLevelGates(rom, m.Entry));
            List("Triggers", () => EntityReader.ReadMapTriggers(rom, m.Entry));
            List("Spawn scripts / characters", () => EntityReader.ReadMapScripts(rom, m.Entry, game));
            p.Text = text.ToString();
            return p;
        }

        // ---- Tile atlas ----

        private static void AddAtlas(AssetCategory category, ROM rom, Game game)
        {
            if (game.TileAtlasOffset == 0) { category.Note = NotInProfile(game, "The tile atlas"); return; }
            for (int bank = 0; bank < 256; bank++)
            {
                uint p = U32(rom, game.TileAtlasOffset + bank * 4);
                if (!IsRomPointer(p, rom)) break;
                int address = (int)(p & 0x01FFFFFF);
                category.Nodes.Add(new AssetNode
                {
                    Name = $"Bank {bank:D3}", Address = address, Kind = AssetKind.Image, Id = bank,
                    Details = [("Bank", bank.ToString(CultureInfo.InvariantCulture)), ("Tiles", "Up to 256, 8 x 8"), ("Palette", "BG")],
                    Info = "Every map draws its tiles from these banks.",
                    Preview = (r, g) =>
                    {
                        byte[] pixels = JCALG1.Decompress(r, address);
                        int tiles = Math.Max(1, pixels.Length / 64);
                        return new AssetPreview { Raw = pixels }.Add($"{tiles} tiles", DeveloperIntro.RenderIndexedTiled(pixels, 16, (tiles + 15) / 16, MapRenderer.ReadBGPalette(r, g)));
                    },
                });
            }
            category.Nodes.FirstOrDefault()?.Ranges.Add((game.TileAtlasOffset, category.Nodes.Count * 4));
        }

        // ---- Scouter ----

        /// <summary>
        /// The Scouter database (IDA: g_ScouterDatabase): what a scan shows for each enemy or NPC. Names and descriptions are the
        /// ROM's own text. The game shows the entry's sprite, its portrait (index 0 = "No Portrait Data"; 1-7 are party members,
        /// whose portrait follows their current form) and HP/STR/POW/END from the stat table (stat index 0 = "???").
        /// </summary>
        private static void AddScouter(AssetCategory category, ROM rom, Game game)
        {
            if (game.ScouterTableOffset == 0 || game.ScouterCount <= 0) { category.Note = NotInProfile(game, "Scouter entries"); return; }
            for (int i = 0; i < game.ScouterCount; i++)
            {
                int at = game.ScouterTableOffset + i * 20;
                if (at + 20 > rom.Length) break;
                uint namePtr = U32(rom, at), descPtr = U32(rom, at + 4);
                if (!IsRomPointer(namePtr, rom)) break;
                string name = CString(rom, (int)(namePtr & 0x01FFFFFF));
                int sprite = (int)U32(rom, at + 8), portrait = (int)U32(rom, at + 12), stat = (int)U32(rom, at + 16);
                int index = i;
                // Several entries share a name (seven "Cell, Perfect"s, one per fight); number the repeats so the list tells them apart.
                int same = category.Nodes.Count(n => n.Name == name || n.Name.StartsWith(name + " (", StringComparison.Ordinal));
                var stats = ScouterStats(rom, game, stat);
                var node = new AssetNode
                {
                    Name = same == 0 ? name : $"{name} ({same + 1})", Address = at, Kind = AssetKind.Image, Index = false, Id = index,
                    Details =
                    [
                        ("Entry", index.ToString(CultureInfo.InvariantCulture)),
                        ("HP", stats?.Hp.ToString(CultureInfo.InvariantCulture) ?? "???"),
                        ("STR / POW / END", stats is { } s ? $"{s.Str} / {s.Pow} / {s.End}" : "???"),
                        ("Sprite", sprite.ToString(CultureInfo.InvariantCulture)),
                        ("Portrait", portrait == 0 ? "None" : portrait <= 7 ? $"{portrait} (party member)" : portrait.ToString(CultureInfo.InvariantCulture)),
                        ("Scan flag", (102 * 8 + index).ToString(CultureInfo.InvariantCulture)),
                    ],
                    Preview = (r, g) => ScouterPreview(r, g, index, sprite, portrait, stat, descPtr),
                };
                node.Ranges.Add((at, 20));
                category.Nodes.Add(node);
            }
        }

        /// <summary>HP and STR/POW/END from the Scouter stat table; null for stat index 0 (the game shows "???").</summary>
        private static (int Hp, int Str, int Pow, int End)? ScouterStats(ROM rom, Game game, int stat)
        {
            if (stat == 0 || EnemyStats.Read(rom, game, stat) is not { } e) return null; // follows the table if it was moved
            return (e.Hp, e.Str, e.Pow, e.End);
        }

        private static AssetPreview ScouterPreview(ROM rom, Game game, int index, int sprite, int portrait, int stat, uint descPtr)
        {
            var p = new AssetPreview();
            if (IsRomPointer(descPtr, rom)) p.Text = CString(rom, (int)(descPtr & 0x01FFFFFF));

            if (portrait >= 8 && game.PortraitTableOffset != 0 && portrait < game.PortraitCount)
            {
                uint pp = U32(rom, game.PortraitTableOffset + portrait * 4);
                if (IsRomPointer(pp, rom))
                {
                    using var square = SquareImage(JCALG1.Decompress(rom, (int)(pp & 0x01FFFFFF)), ItemIconReader.ReadOBJPalette(rom, game), tiled: false);
                    foreach (var (_, image) in square.Images) p.Add($"portrait {portrait}", image, copy: true);
                }
            }
            // The sprite standing still, one frame per direction (group 0 = idle).
            try
            {
                foreach (var (label, _, _, image) in SpriteDump.RenderFrames(rom, sprite, game))
                {
                    if (label.StartsWith("g00_", StringComparison.Ordinal) && label.EndsWith("_f0", StringComparison.Ordinal)) p.Add($"sprite {sprite} {label[4..^3]}", image);
                    else image.Dispose();
                }
            }
            catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or InvalidOperationException) { }
            return p;
        }

        private static string CString(ROM rom, int at)
        {
            var sb = new StringBuilder();
            for (int i = at; i < rom.Length && i < at + 4096; i++)
            {
                byte c = rom.ReadBytesAt(i, 1)[0];
                if (c == 0) break;
                sb.Append(c == '\n' ? "\n" : c < 0x20 ? "" : ((char)c).ToString());
            }
            return sb.ToString();
        }

        // ---- Menus ----

        /// <summary>A 256-colour GBA palette at a file offset.</summary>
        public static Color[] ReadPalette(ROM rom, int offset)
        {
            var raw = rom.ReadBytesAt(offset, 512);
            var colors = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                int c = raw[i * 2] | (raw[i * 2 + 1] << 8);
                colors[i] = Color.FromArgb(255, ((c & 0x1F) * 0x21) >> 2, (((c >> 5) & 0x1F) * 0x21) >> 2, (((c >> 10) & 0x1F) * 0x21) >> 2);
            }
            return colors;
        }

        private static Color[] UiPalette(ROM rom, Game game, string which) =>
            which == "obj" && game.OBJPaletteOffset != 0 ? ItemIconReader.ReadOBJPalette(rom, game)
            : which == "bg" || game.MenuPaletteOffset == 0 ? MapRenderer.ReadBGPalette(rom, game)
            : ReadPalette(rom, game.MenuPaletteOffset);

        /// <summary>"PauseMenu_TabLeft1" -> "Pause menu: tab left 1" (profile names are IDA names; the list shows them as words).</summary>
        private static string Friendly(string name)
        {
            static string Words(string part)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < part.Length; i++)
                {
                    char c = part[i];
                    bool boundary = i > 0 && ((char.IsUpper(c) && !char.IsUpper(part[i - 1])) || (char.IsDigit(c) && !char.IsDigit(part[i - 1])));
                    if (boundary) sb.Append(' ');
                    sb.Append(sb.Length == 0 ? c : char.ToLowerInvariant(c));
                }
                return sb.ToString().Replace("u i ", "UI ").Replace(" u i", " UI");
            }
            var parts = name.Split('_', 2);
            if (parts[0] == "UI") return parts.Length > 1 ? Words(parts[1]) : name;
            return parts.Length == 2 ? $"{Words(parts[0])}: {Words(parts[1]).ToLowerInvariant()}" : Words(name);
        }

        /// <summary>The front-end and menu pictures listed in the profile (UiImages): flat 8bpp bitmaps the game loads by address.</summary>
        private static void AddMenus(AssetCategory category, ROM rom, Game game)
        {
            if (game.UiImages.Count == 0) { category.Note = NotInProfile(game, "Menu images"); return; }
            foreach (var ui in game.UiImages)
            {
                var image = ui;
                if (image.Address <= 0 || image.Address + 8 > rom.Length || image.Width <= 0) continue;
                int bytes = BitConverter.ToInt32(rom.ReadBytesAt(image.Address + 4, 4)); // ResourceHeader.size: the unpacked size
                bool tilemap = image.Layout == "tilemap", tiles = image.Layout == "tiles";
                int perPixel = tilemap ? 2 : 1;
                category.Nodes.Add(new AssetNode
                {
                    Name = Friendly(image.Name), Address = image.Address, Kind = tilemap ? AssetKind.Text : AssetKind.Image,
                    Details =
                    [
                        (tilemap ? "Entries" : "Size", bytes > 0 ? $"{image.Width} x {(bytes / perPixel + image.Width - 1) / image.Width}" : $"{image.Width} wide"),
                        ("Layout", tilemap ? "BG tilemap" : tiles ? "8 x 8 tiles" : "Bitmap"),
                        ("Palette", image.Palette switch { "obj" => "OBJ", "bg" => "BG", _ => "Menu" }),
                        ("IDA name", "g_" + image.Name),
                    ],
                    Info = image.Note,
                    Preview = (r, g) =>
                    {
                        byte[] pixels = JCALG1.Decompress(r, image.Address);
                        var p = new AssetPreview { Raw = pixels };
                        if (tilemap)
                        {
                            // Tile number of each entry, a row per line: the tiles themselves are put in VRAM by other code.
                            var text = new StringBuilder();
                            for (int i = 0; i + 1 < pixels.Length; i += 2)
                            {
                                int entry = pixels[i] | (pixels[i + 1] << 8);
                                text.Append(CultureInfo.InvariantCulture, $"{entry & 0x3FF,4}");
                                if ((i / 2 + 1) % image.Width == 0) text.Append('\n');
                            }
                            p.Text = text.ToString();
                            return p;
                        }
                        var palette = UiPalette(r, g, image.Palette);
                        if (tiles)
                        {
                            int across = Math.Max(1, image.Width / 8), count = Math.Max(1, pixels.Length / 64), down = (count + across - 1) / across;
                            return p.Add($"{count} tiles", DeveloperIntro.RenderIndexedTiled(Pad(pixels, across * down * 64), across, down, palette));
                        }
                        int rows = Math.Max(1, (pixels.Length + image.Width - 1) / image.Width);
                        return p.Add($"{image.Width}x{rows}", IndexedImage.FromIndices(pixels, image.Width, rows, palette));
                    },
                });
            }
        }

        // ---- Area maps (pause menu) ----

        /// <summary>
        /// The pause-menu map (IDA: AreaMapScreen_Init). The profile points at {s32 count; Region** regions}. A region is
        /// {ResourceHeader* tiles (8bpp, BG palette); script; s32 markerCount; markers; ResourceHeader* markerTiles (OBJ); s32 roomCount; Room* rooms[]}.
        /// A room is {u8 zone; u8 area; u16 visitedFlag; Rect unvisited; Rect visited; u16 tilemap[unvisited area]; u16 tilemap[visited area]},
        /// rectangles in 8x8 tiles on a 128x128 canvas. The game draws a room's visited tilemap once its flag is set.
        /// </summary>
        private static void AddAreaMaps(AssetCategory category, ROM rom, Game game)
        {
            if (game.AreaMapRegionsOffset == 0) { category.Note = NotInProfile(game, "Area maps"); return; }
            int count = (int)U32(rom, game.AreaMapRegionsOffset);
            uint table = U32(rom, game.AreaMapRegionsOffset + 4);
            if (count is <= 0 or > 512 || !IsRomPointer(table, rom)) { category.Note = "The area map table in the game profile doesn't look right."; return; }

            for (int i = 0; i < count; i++)
            {
                uint p = U32(rom, (int)(table & 0x01FFFFFF) + i * 4);
                if (!IsRomPointer(p, rom)) continue;
                int region = (int)(p & 0x01FFFFFF);
                int rooms = (int)U32(rom, region + 0x14);
                if (rooms is < 0 or > 512) continue;
                var covers = new List<string>();
                for (int r = 0; r < rooms; r++)
                {
                    uint room = U32(rom, region + 0x18 + r * 4);
                    if (!IsRomPointer(room, rom)) continue;
                    var id = rom.ReadBytesAt((int)(room & 0x01FFFFFF), 2);
                    covers.Add($"Z{id[0]}A{id[1]}");
                }
                var node = new AssetNode
                {
                    Name = covers.Count > 0 ? $"Region {i:D2} ({covers[0]}{(covers.Count > 1 ? $" + {covers.Count - 1} more" : "")})" : $"Region {i:D2}",
                    Address = region, Kind = AssetKind.Image, Id = i,
                    Details = [("Region", i.ToString(CultureInfo.InvariantCulture)), ("Rooms", covers.Count.ToString(CultureInfo.InvariantCulture)), ("Palette", "BG")],
                    Info = "The pause-menu map of these rooms: " + string.Join(", ", covers.Take(16)) + (covers.Count > 16 ? ", ..." : "") + ".",
                    Preview = (r, g) => AreaMapPreview(r, g, region),
                    IndexWith = (r, _) =>
                    {
                        foreach (int field in new[] { 0x00, 0x10 })
                        {
                            uint block = U32(r, region + field);
                            if (IsRomPointer(block, r)) JCALG1.Decompress(r, (int)(block & 0x01FFFFFF));
                        }
                    },
                };
                node.Ranges.Add((region, 0x18 + rooms * 4));
                category.Nodes.Add(node);
            }
            category.Nodes.FirstOrDefault()?.Ranges.Add(((int)(table & 0x01FFFFFF), count * 4));
        }

        private static AssetPreview AreaMapPreview(ROM rom, Game game, int region)
        {
            const int side = 128;
            var preview = new AssetPreview();
            uint tilesAt = U32(rom, region);
            if (!IsRomPointer(tilesAt, rom)) return preview;
            byte[] tiles = JCALG1.Decompress(rom, (int)(tilesAt & 0x01FFFFFF));
            var palette = MapRenderer.ReadBGPalette(rom, game);
            int rooms = (int)U32(rom, region + 0x14);

            foreach (bool visited in new[] { true, false })
            {
                var canvas = new ushort[side * side];
                int left = side, top = side, right = 0, bottom = 0;
                for (int r = 0; r < rooms; r++)
                {
                    uint p = U32(rom, region + 0x18 + r * 4);
                    if (!IsRomPointer(p, rom)) continue;
                    int room = (int)(p & 0x01FFFFFF);
                    int[] a = Rect(rom, room + 4), b = Rect(rom, room + 0x14);
                    int[] use = visited ? b : a;
                    int data = room + 0x24 + (visited ? 2 * Math.Max(0, a[2] - a[0]) * Math.Max(0, a[3] - a[1]) : 0);
                    if (use[0] < 0 || use[1] < 0 || use[2] > side || use[3] > side || use[2] <= use[0] || use[3] <= use[1]) continue;
                    if (data + 2 * (use[2] - use[0]) * (use[3] - use[1]) > rom.Length) continue;
                    var map = rom.ReadBytesAt(data, 2 * (use[2] - use[0]) * (use[3] - use[1]));
                    int at = 0;
                    for (int y = use[1]; y < use[3]; y++)
                        for (int x = use[0]; x < use[2]; x++, at += 2)
                        {
                            ushort entry = BitConverter.ToUInt16(map, at);
                            if (entry != 0) canvas[y * side + x] = entry;
                        }
                    left = Math.Min(left, use[0]); top = Math.Min(top, use[1]); right = Math.Max(right, use[2]); bottom = Math.Max(bottom, use[3]);
                }
                if (right <= left || bottom <= top) continue;

                int w = (right - left) * 8, h = (bottom - top) * 8;
                var indices = new byte[w * h];
                for (int ty = top; ty < bottom; ty++)
                    for (int tx = left; tx < right; tx++)
                    {
                        ushort entry = canvas[ty * side + tx];
                        int tile = entry & 0x3FF;
                        if (entry == 0 || tile * 64 + 64 > tiles.Length) continue;
                        bool flipX = (entry & 0x400) != 0, flipY = (entry & 0x800) != 0;
                        for (int y = 0; y < 8; y++)
                            for (int x = 0; x < 8; x++)
                                indices[((ty - top) * 8 + y) * w + (tx - left) * 8 + x] = tiles[tile * 64 + (flipY ? 7 - y : y) * 8 + (flipX ? 7 - x : x)];
                    }
                preview.Add(visited ? "every room visited" : "no room visited", IndexedImage.FromIndices(indices, w, h, palette));
            }

            preview.Add("tiles", DeveloperIntro.RenderIndexedTiled(Pad(tiles, ((tiles.Length / 64 + 15) / 16) * 16 * 64), 16, Math.Max(1, (tiles.Length / 64 + 15) / 16), palette));
            return preview;

            static int[] Rect(ROM r, int at) => [.. Enumerable.Range(0, 4).Select(k => BitConverter.ToInt32(r.ReadBytesAt(at + k * 4, 4)))];
        }

        // ---- Boot screens ----

        private static void AddBootScreens(AssetCategory category, ROM rom, Game game)
        {
            if (!game.HasLayout("ALFE-boot")) { category.Note = NotInProfile(game, "Boot screens"); return; }

            var intro = new AssetNode { Name = "Developer intro" };
            foreach (var slide in DeveloperIntro.Slides)
            {
                var s = slide;
                intro.Children.Add(new AssetNode
                {
                    Name = s.Name, Address = s.DataAddress & 0x01FFFFFF, Kind = AssetKind.Image, Details = [("Size", "240 x 160")],
                    Preview = (r, _) =>
                    {
                        byte[] raw = DeveloperIntro.GetRawIndexedBytes(r, s);
                        return new AssetPreview { Raw = raw }.Add(s.Name, DeveloperIntro.RenderIndexed(Pad(raw, DeveloperIntro.FrameWidth * DeveloperIntro.FrameHeight),
                            DeveloperIntro.FrameWidth, DeveloperIntro.FrameHeight, DeveloperIntro.GetPalette(r)));
                    },
                });
            }

            var video = new AssetNode { Name = "Attract-mode video" };
            int frame = 0;
            foreach (int address in GameIntroVideo.GetFrameResourceAddresses(rom))
            {
                int a = address & 0x01FFFFFF, n = frame++;
                if (a == 0) continue;
                video.Children.Add(new AssetNode
                {
                    Name = $"Frame {n:D3}", Address = a, Kind = AssetKind.Image, Id = n, Details = [("Frame", n.ToString(CultureInfo.InvariantCulture)), ("Size", "240 x 160")],
                    Info = "Frames that only store changes are shown on their own, not on top of the previous frame.",
                    Preview = (r, _) =>
                    {
                        byte[] raw = GameIntroVideo.GetRawFrameBytes(r, a);
                        return new AssetPreview { Raw = raw }.Add($"frame {n}", DeveloperIntro.RenderIndexedTiled(raw, GameIntroVideo.FrameWidth / 8, GameIntroVideo.FrameHeight / 8, GameIntroVideo.GetPalette(r)));
                    },
                });
            }

            var ball = new AssetNode { Name = "Title screen ball" };
            frame = 0;
            foreach (int address in TitleScreenBall.GetFrameResourceAddresses(rom))
            {
                int a = address & 0x01FFFFFF, n = frame++;
                if (a == 0) continue;
                ball.Children.Add(new AssetNode
                {
                    Name = $"Frame {n}", Address = a, Kind = AssetKind.Image, Id = n, Details = [("Frame", n.ToString(CultureInfo.InvariantCulture))],
                    Preview = (r, _) =>
                    {
                        byte[] raw = TitleScreenBall.GetRawFrameBytes(r, a);
                        return new AssetPreview { Raw = raw }.Add($"frame {n}", TitleScreenBall.RenderFrame(raw, TitleScreenBall.GetPalette(r)));
                    },
                });
            }

            category.Nodes.AddRange(new[] { intro, video, ball }.Where(n => n.Children.Count > 0));
        }

        // ---- Text ----

        private static void AddText(AssetCategory category, ROM rom, Game game, List<MapRef> maps)
        {
            if (game.HasLayout("ALFE-boot"))
                category.Nodes.Add(new AssetNode
                {
                    Name = "Credits", Address = DeveloperCredits.ChainAddress & 0x01FFFFFF, Kind = AssetKind.Text, Index = false,
                    Info = "The developer credits roll.",
                    Preview = (r, _) =>
                    {
                        var text = string.Join(Environment.NewLine, DeveloperCredits.GetEntries(r).Select(e => $"{e.Title}{Environment.NewLine}  {e.Subtitle}"));
                        return new AssetPreview { Text = text, Raw = Encoding.UTF8.GetBytes(text), RawExtension = "txt" };
                    },
                });

            foreach (var language in Languages(game))
            {
                var lang = language;
                var used = maps.Select(m => m.Entry.MapNameIndex).Where(i => i is > 0 and <= 0xFFFF).Distinct().OrderBy(i => i).ToList();
                category.Nodes.Add(new AssetNode
                {
                    Name = $"Map names ({lang.Name}{(lang.Code.Length > 0 ? $", {lang.Code}" : "")})", Address = lang.MapNameTableOffset, Kind = AssetKind.Text, Index = false,
                    Info = "Every name a map uses, by name number. Dialogue and scripts are edited in Legacy.",
                    Preview = (r, _) =>
                    {
                        var text = string.Join(Environment.NewLine, used.Select(i => $"{i,4}  {ReadString(r, lang, i)}"));
                        return new AssetPreview { Text = text, Raw = Encoding.UTF8.GetBytes(text), RawExtension = "txt" };
                    },
                });
            }
            if (category.Nodes.Count == 0) category.Note = NotInProfile(game, "Text tables");
        }

        // ---- ResourceHeader scan ----

        private static List<ResourceBlock> ScanBlocks(byte[] rom)
        {
            var referrers = new Dictionary<int, List<int>>();
            for (int o = 0; o + 4 <= rom.Length; o += 4)
            {
                uint v = BitConverter.ToUInt32(rom, o);
                if (v < 0x08000000 || v >= 0x08000000u + (uint)rom.Length || (v & 3) != 0) continue;
                int target = (int)(v - 0x08000000);
                if (!referrers.TryGetValue(target, out var list)) referrers[target] = list = [];
                list.Add(o);
            }

            var blocks = new List<ResourceBlock>();
            int lastEnd = 0;
            foreach (int target in referrers.Keys.Order())
            {
                if (target < lastEnd) continue;
                if (!JCALG1.TryValidate(rom, target, out int size, out int end)) continue;
                blocks.Add(new ResourceBlock { Address = target, End = end, Mode = BitConverter.ToInt32(rom, target), Size = size, Referrers = referrers[target] });
                lastEnd = end;
            }
            return blocks;
        }

        // ---- Index ----

        /// <summary>
        /// Every asset with a preview, in the order that decides ownership: a block belongs to the first asset that decodes it, so the
        /// specific assets come first and the composite ones last (a map render also decodes its tile atlas banks and object graphics,
        /// which should stay "Tile atlas" / "Objects"). The order only depends on the ROM and the profile, so a saved index maps back.
        /// </summary>
        private List<AssetNode> IndexOrder()
        {
            string[] last = ["Objects", "Maps"];
            return [.. Categories.Where(c => c.Name != Unclassified).OrderBy(c => Array.IndexOf(last, c.Name))
                .SelectMany(c => c.Nodes).SelectMany(n => n.SelfAndDescendants()).Where(n => n.Preview != null)];
        }

        /// <summary>
        /// Runs every asset's reader once and records which blocks it decodes, on several threads. Each worker gets its own ROM copy
        /// and Game clone (renderers keep per-Game caches, ROM keeps a read position), so the UI's own copies are never touched.
        /// Owners are assigned afterwards in <see cref="IndexOrder"/>, so the result doesn't depend on thread timing.
        /// </summary>
        public void Index(byte[] romBytes, Game game, IProgress<(int Done, int Total, string What)>? progress = null, CancellationToken cancel = default, int workers = 0)
        {
            var nodes = IndexOrder();
            var hits = new List<int>?[nodes.Count];
            var work = Enumerable.Range(0, nodes.Count).Where(i => nodes[i].Index).ToList();
            // Each worker holds one map's render caches at a time; 6 keeps a 32-bit process well inside its address space.
            if (workers <= 0) workers = Math.Clamp(Environment.ProcessorCount - 1, 1, 6);
            int done = 0;

            Parallel.For(0, workers, new ParallelOptions { CancellationToken = cancel, MaxDegreeOfParallelism = workers }, w =>
            {
                var rom = ROM.FromBytes((byte[])romBytes.Clone());
                var g = game.Clone();
                List<int>? sink = null;
                rom.ResourceAccessed += address => sink?.Add(address);
                try
                {
                    // Interleaved, so the slow maps are spread over every worker.
                    for (int k = w; k < work.Count; k += workers)
                    {
                        cancel.ThrowIfCancellationRequested();
                        int i = work[k];
                        sink = hits[i] = [];
                        try
                        {
                            if (nodes[i].IndexWith is { } decode) decode(rom, g);
                            else nodes[i].Preview!(rom, g).Dispose();
                        }
                        catch (OperationCanceledException) { throw; }
                        catch { /* an asset that can't be drawn still lists */ }
                        sink = null;
                        if (nodes[i].Category is "Maps" or "Objects") MapRenderer.ResetCache(g); // keep memory flat over hundreds of maps
                        int n = Interlocked.Increment(ref done);
                        if (n % 16 == 0) progress?.Report((n, work.Count, $"{nodes[i].Category}: {nodes[i].Name}"));
                    }
                }
                finally { MapRenderer.ResetCache(g); }
            });

            var owners = new Dictionary<int, int>();   // block address -> node index
            var stored = new List<(int Address, int Size)>();
            var starts = Blocks.Select(b => b.Address).ToList();
            for (int i = 0; i < nodes.Count; i++)
            {
                foreach (var (start, length) in nodes[i].Ranges)
                    foreach (var b in Blocks.Where(b => b.Address < start + length && start < b.End)) owners.TryAdd(b.Address, i);
                foreach (int address in hits[i] ?? [])
                {
                    int found = FindBlock(starts, address);
                    if (found >= 0) { owners.TryAdd(Blocks[found].Address, i); continue; }
                    // A stored (mode 0) block: only trusted because a reader really uses it.
                    if (address + 8 > romBytes.Length || BitConverter.ToInt32(romBytes, address) != 0) continue;
                    int size = BitConverter.ToInt32(romBytes, address + 4);
                    if (size <= 0 || address + 8 + size > romBytes.Length || stored.Any(s => s.Address == address)) continue;
                    stored.Add((address, size));
                    owners.TryAdd(address, i);
                }
            }
            Apply(nodes, owners, stored);
            progress?.Report((work.Count, work.Count, "done"));
        }

        private int FindBlock(List<int> starts, int address)
        {
            int i = starts.BinarySearch(address);
            if (i < 0) i = ~i - 1;
            return i >= 0 && address < Blocks[i].End ? i : -1;
        }

        private void Apply(List<AssetNode> nodes, Dictionary<int, int> owners, List<(int Address, int Size)> stored)
        {
            var starts = Blocks.Select(b => b.Address).ToList();
            var extra = stored.Where(s => FindBlock(starts, s.Address) < 0)
                .Select(s => new ResourceBlock { Address = s.Address, End = s.Address + 8 + s.Size, Mode = 0, Size = s.Size });
            Blocks = [.. Blocks.Concat(extra).OrderBy(b => b.Address)];
            foreach (var b in Blocks) b.Owner = owners.TryGetValue(b.Address, out int i) && i < nodes.Count ? nodes[i] : null;
            BuildUnclassified();
            Indexed = true;
        }

        // ---- Saved index ----

        private const int IndexFormat = 1;

        /// <summary>
        /// A name for this ROM + profile's index: SHA-1 of the ROM bytes and of the profile, so another version, an edited ROM or a
        /// changed profile never reuses a stale one.
        /// </summary>
        public static string IndexKey(byte[] romBytes, Game game)
        {
            string profile = System.Text.Json.JsonSerializer.Serialize(game);
            string rom = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(romBytes));
            string prof = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(profile)))[..12];
            return $"{game.GAME_CODE}_{rom}_{prof}_v{IndexFormat}";
        }

        private sealed class SavedIndex
        {
            public int Format { get; set; }
            public int Nodes { get; set; }
            public int Blocks { get; set; }
            public Dictionary<int, int> Owners { get; set; } = [];
            public List<int[]> Stored { get; set; } = [];
        }

        public void SaveIndex(string path)
        {
            var nodes = IndexOrder();
            var ids = nodes.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
            var saved = new SavedIndex
            {
                Format = IndexFormat, Nodes = nodes.Count, Blocks = Blocks.Count(b => b.Mode != 0),
                Owners = Blocks.Where(b => b.Owner != null).ToDictionary(b => b.Address, b => ids[b.Owner!]),
                Stored = [.. Blocks.Where(b => b.Mode == 0).Select(b => new[] { b.Address, b.Size })],
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(saved));
        }

        /// <summary>Applies a saved index if it matches this catalog (same asset list and block scan); false = index again.</summary>
        public bool TryLoadIndex(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var saved = System.Text.Json.JsonSerializer.Deserialize<SavedIndex>(File.ReadAllText(path));
                var nodes = IndexOrder();
                if (saved == null || saved.Format != IndexFormat || saved.Nodes != nodes.Count || saved.Blocks != Blocks.Count) return false;
                Apply(nodes, saved.Owners, [.. saved.Stored.Select(s => (s[0], s[1]))]);
                return true;
            }
            catch { return false; }
        }

        private void BuildUnclassified()
        {
            var category = Categories.First(c => c.Name == Unclassified);
            category.Nodes.Clear();
            category.Note = null;
            foreach (var region in Blocks.Where(b => b.Owner == null).GroupBy(b => b.Address & ~0x3FFFF))
            {
                var group = new AssetNode { Name = $"0x{0x08000000 | region.Key:X8} - 0x{0x08000000 | (region.Key + 0x3FFFF):X8}", Category = Unclassified };
                foreach (var block in region)
                {
                    var b = block;
                    group.Children.Add(new AssetNode
                    {
                        Name = $"res_{0x08000000 | b.Address:X8}", Address = b.Address, Kind = AssetKind.Resource, Index = false, Category = Unclassified,
                        Details =
                        [
                            ("Storage", b.Mode == 0 ? "Uncompressed" : "Compressed (JCALG1)"),
                            ("Size in ROM", $"{b.End - b.Address:N0} bytes"), ("Unpacked", $"{b.Size:N0} bytes"),
                            ("Used from", b.Referrers.Count == 0 ? "Nothing found" : string.Join(", ", b.Referrers.Take(3).Select(r => $"0x{0x08000000 | r:X8}")) + (b.Referrers.Count > 3 ? ", ..." : "")),
                        ],
                        Info = "Not identified yet. Try the format and palette options to see what it is.",
                        Preview = (r, _) => new AssetPreview { Raw = JCALG1.Decompress(r, b.Address) },
                    });
                }
                category.Nodes.Add(group);
            }
            if (category.Nodes.Count == 0) category.Note = "Every resource block in this ROM belongs to a known asset.";
        }

        // ---- ROM map ----

        /// <summary>
        /// One row per known ROM range: every resource block (with the asset that uses it) and every asset's other ranges.
        /// Addresses are GBA addresses. Feeds IDA naming and the decomp's ROM map.
        /// </summary>
        public string ToCsv()
        {
            var rows = new List<(int Start, int End, string Kind, string Category, string Name)>();
            foreach (var b in Blocks)
                rows.Add((b.Address, b.End, b.Mode == 0 ? "resource_stored" : "resource_jcalg1", b.Owner?.Category ?? Unclassified, b.Owner?.Name ?? $"res_{0x08000000 | b.Address:X8}"));
            foreach (var node in Categories.SelectMany(c => c.Nodes).SelectMany(n => n.SelfAndDescendants()))
                foreach (var (start, length) in node.Ranges)
                    rows.Add((start, start + length, "data", node.Category, node.Name));

            var csv = new StringBuilder("start,end,length,kind,category,name\n");
            foreach (var r in rows.OrderBy(r => r.Start).ThenBy(r => r.End))
                csv.Append(CultureInfo.InvariantCulture, $"0x{0x08000000 | r.Start:X8},0x{0x08000000 | r.End:X8},{r.End - r.Start},{r.Kind},{Quote(r.Category)},{Quote(r.Name)}\n");
            return csv.ToString();
        }

        private static string Quote(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}
