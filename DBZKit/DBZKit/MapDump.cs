using DrGero.Config;
using DrGero.IO;
using DrGero.Rendering;
using DrGero.Types;
using System.Drawing.Imaging;
using System.Text;

namespace DBZKit
{
    /// <summary>
    /// Dumps every map's graphics to PNG + raw BIN so they can be edited outside the tools.
    ///
    ///   Atlas\bank_NNN.png / .bin        every 256-tile bank of the shared tile atlas (the real
    ///                                    tile art: .bin is the raw 8bpp pixels, 64 bytes/tile, the
    ///                                    .png is the same drawn with the background palette)
    ///   Maps\Z{zone}A{area}v{var}_{idx}\ one folder per map:
    ///     composite.png                  the map as the game draws it (all layers)
    ///     tileset.png                    this map's tileset: slot i = the tile a tilemap entry with
    ///                                    id i shows (tileset_animated_N.png for animated ranges)
    ///     tileset_slots.txt              slot -> atlas tile index, and tileset_table.bin (raw)
    ///     layerN.png / layerN_tilemap.bin / layerN.txt
    ///                                    each background layer: the picture, the raw tilemap (u16
    ///                                    little-endian per tile, row-major: id | flipX&lt;&lt;10 |
    ///                                    flipY&lt;&lt;11) and its size/offsets
    ///     collision.png / collision.bin  where you can walk (white = allowed) and the raw 8 KB grid
    ///     map.txt                        zone/area/variation, pixel size
    /// A map that can't be read is noted in errors.txt and skipped, not fatal.
    /// </summary>
    internal static class MapDumper
    {
        // Confirmed offsets from Dragon Radar's games/ALFE.json (this ROM) -- only what the map
        // reader needs, like NpcGame in DBZKit.cs.
        private static readonly Game MapGame = new()
        {
            MapEntriesOffset = 0x409368,
            MapEntryCount = 327,
            BGPaletteOffset = 0x1DA4C8,
            TileAtlasOffset = 0x4DF574,
        };

        public static (int Maps, int Banks, int Errors) DumpAll(ROM rom, string folder, Action<string>? progress = null)
        {
            Directory.CreateDirectory(folder);
            var errors = new StringBuilder();
            MapRenderer.ResetCache(MapGame);

            int banks = DumpAtlas(rom, Path.Combine(folder, "Atlas"), errors, progress);

            int maps = 0;
            for (int index = 0; index < MapGame.MapEntryCount; index++)
            {
                progress?.Invoke($"Dumping maps {index + 1}/{MapGame.MapEntryCount}");
                try
                {
                    if (DumpMap(rom, folder, index, errors)) maps++;
                }
                catch (Exception ex)
                {
                    errors.AppendLine($"map {index}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (errors.Length > 0)
                File.WriteAllText(Path.Combine(folder, "errors.txt"), errors.ToString());
            return (maps, banks, errors.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        }

        private static int DumpAtlas(ROM rom, string dir, StringBuilder errors, Action<string>? progress)
        {
            Directory.CreateDirectory(dir);
            var palette = MapRenderer.ReadBGPalette(rom, MapGame);
            int count = 0;

            for (int bank = 0; bank < 256; bank++)
            {
                progress?.Invoke($"Dumping tile atlas {bank + 1}/256");

                rom.PushPosition(MapGame.TileAtlasOffset + (bank * 4));
                int raw = rom.ReadInt();
                rom.PopPosition();
                if ((raw & unchecked((int)0xFF000000)) != 0x08000000 || (raw & 0x00FFFFFF) >= rom.Length) break; // end of the atlas table

                try
                {
                    byte[] pixels = JCALG1.Decompress(rom, raw & 0x00FFFFFF);
                    File.WriteAllBytes(Path.Combine(dir, $"bank_{bank:D3}.bin"), pixels);
                    // GetTilesetImage returns a CACHED bitmap that later map renders draw from, so it
                    // must never be disposed here (doing so broke every map render that followed).
                    var image = MapRenderer.GetTilesetImage(rom, MapGame, bank * 256, palette);
                    using var copy = new Bitmap(image);
                    copy.Save(Path.Combine(dir, $"bank_{bank:D3}.png"), ImageFormat.Png);
                    count++;
                }
                catch (Exception ex)
                {
                    errors.AppendLine($"atlas bank {bank}: {ex.Message}");
                }
            }
            return count;
        }

        private static bool DumpMap(ROM rom, string folder, int index, StringBuilder errors)
        {
            rom.PushPosition(MapGame.MapEntriesOffset + (index * MapEntry.RecordSize));
            var entry = MapEntry.Read(rom);
            rom.PopPosition();
            if (entry.VariationArray <= 0) return false;

            rom.PushPosition(entry.VariationArray);
            int mapOffset = rom.ReadPointer();
            rom.PopPosition();
            if (mapOffset <= 0 || mapOffset >= rom.Length) return false;

            string dir = Path.Combine(folder, "Maps", $"Z{entry.Zone}A{entry.Area}v{entry.Variation}_{index:D3}");
            Directory.CreateDirectory(dir);

            var (width, height) = MapResizer.ReadSize(rom, mapOffset);
            File.WriteAllText(Path.Combine(dir, "map.txt"),
                $"index {index}\r\nzone {entry.Zone}\r\narea {entry.Area}\r\nvariation {entry.Variation}\r\npixel size {width} x {height}\r\n");

            // composite + tileset sheets (a map using the affine world-map mode has no tiled data here)
            Dictionary<Range, Bitmap> tilesets;
            try
            {
                var (composite, ts, _, _, _) = MapRenderer.RenderMap(rom, MapGame, mapOffset, new MapRenderOptions());
                tilesets = ts;
                using (composite) composite.Save(Path.Combine(dir, "composite.png"), ImageFormat.Png);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(dir, "render_error.txt"), ex.Message);
                errors.AppendLine($"map {index} (Z{entry.Zone}A{entry.Area}): couldn't render -- {ex.Message}");
                return true; // still counts as a map; the folder explains why there's no picture
            }

            int animated = 0;
            foreach (var (range, sheet) in tilesets.OrderBy(kv => kv.Key.Start.Value))
            {
                string name = range.Start.Value == 0 ? "tileset.png" : $"tileset_animated_{animated++}.png";
                using var copy = new Bitmap(sheet);
                copy.Save(Path.Combine(dir, name), ImageFormat.Png);
            }

            try
            {
                var slots = TilesetTable.ReadAtlasIndices(rom, mapOffset);
                var sb = new StringBuilder("slot -> atlas tile index (bank = index >> 8, tile = index & 0xFF)\r\n");
                for (int s = 0; s < slots.Length; s++)
                    sb.AppendLine($"{s}\t{slots[s]}\tbank {slots[s] >> 8} tile {slots[s] & 0xFF}");
                File.WriteAllText(Path.Combine(dir, "tileset_slots.txt"), sb.ToString());

                rom.PushPosition(mapOffset + 0x48);
                int tablePointer = rom.ReadPointer();
                rom.PopPosition();
                File.WriteAllBytes(Path.Combine(dir, "tileset_table.bin"), JCALG1.Decompress(rom, tablePointer));
            }
            catch { /* no static tileset table -- nothing to dump */ }

            // layers
            for (int layer = 0; layer < 4; layer++)
            {
                rom.PushPosition(mapOffset + 0x14 + (layer * 4));
                int layerAddress = rom.ReadPointer();
                rom.PopPosition();

                var editor = TileLayerEditor.Open(rom, layerAddress);
                if (editor == null) continue; // empty or an unsupported layer format

                File.WriteAllBytes(Path.Combine(dir, $"layer{layer}_tilemap.bin"), editor.ExportTilemap());
                File.WriteAllText(Path.Combine(dir, $"layer{layer}.txt"),
                    $"tiles wide {editor.NumCols * TileLayerEditor.ChunkTiles}\r\ntiles high {editor.NumRows * TileLayerEditor.ChunkTiles}\r\n" +
                    $"chunk columns {editor.NumCols}\r\nchunk rows {editor.NumRows}\r\n" +
                    $"scroll offset x {editor.OffX}\r\nscroll offset y {editor.OffY}\r\n" +
                    "tilemap.bin: u16 little-endian per tile, row-major; id = bits 0-9, flip X = bit 10, flip Y = bit 11\r\n");

                try
                {
                    var (bitmap, _, _) = MapRenderer.DrawLayer(rom, MapGame, layerAddress, tilesets, new HashSet<Range>());
                    using var copy = new Bitmap(bitmap);
                    copy.Save(Path.Combine(dir, $"layer{layer}.png"), ImageFormat.Png);
                }
                catch { /* the raw tilemap above is the important part */ }
            }

            // collision: white = allowed to walk, black = barred
            var collision = CollisionEditor.Open(rom, mapOffset);
            if (collision != null)
            {
                File.WriteAllBytes(Path.Combine(dir, "collision.bin"), collision.ExportRaw());

                int cw = Math.Clamp((width + 7) / 8, 1, CollisionEditor.GridSize), ch = Math.Clamp((height + 7) / 8, 1, CollisionEditor.GridSize);
                using var image = new Bitmap(cw, ch);
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                        image.SetPixel(x, y, collision.IsAllowed(x, y) ? Color.White : Color.Black);
                image.Save(Path.Combine(dir, "collision.png"), ImageFormat.Png); // one pixel per 8x8 tile
            }

            return true;
        }
    }
}
