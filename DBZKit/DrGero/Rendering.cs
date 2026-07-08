using DrGero.Config;
using DrGero.IO;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DrGero.Rendering
{
    /// <summary>
    /// Renders map tile/layer data to bitmaps. Ported from the original
    /// LOGExtractor/MapViewer MapRenderer, but with all ROM offsets sourced
    /// from a <see cref="Game"/> config instead of hardcoded constants, so
    /// it works across any game definition rather than just one ROM.
    /// </summary>
    public static class MapRenderer
    {
        private const int tile_size = 8;
        private const int chunk_size_tiles = 32;
        private const int chunk_size_pixels = tile_size * chunk_size_tiles;
        private const int tile_per_image = 16 * 16;

        // MapEntry struct layout (0x38 bytes):
        //   +0x00  u8  zone
        //   +0x01  u8  area
        //   +0x02  u8  variation       (runtime flag, NOT an array index)
        //   +0x03  u8  triggerCount
        //   +0x04  u8  scriptCount
        //   +0x05  u8  itemCount
        //   +0x06  u8  objectCount
        //   +0x07  u8  npcCount
        //   +0x08  u32 flags
        //   +0x0C  u32 mapNameIndex
        //   +0x10  ptr mapTriggers
        //   +0x14  ptr mapScripts
        //   +0x18  ptr mapItems
        //   +0x1C  ptr mapObjects
        //   +0x20  ptr npcArray
        //   +0x24  u32 musicId
        //   +0x28  ptr variationScript
        //   +0x2C  ptr variationArray   <-- Map_VariationEntry*
        //   +0x30  ptr entryScript
        //   +0x34  ptr exitScript
        //
        // Map_VariationEntry struct layout (0x50 bytes):
        //   +0x00  ptr MapGraphicObjects  <-- this is the map struct offset

        // Per-game caches. Keyed by Game so switching ROMs doesn't serve stale tiles.
        private static readonly Dictionary<Game, Dictionary<int, Bitmap>> tilesetCaches = [];
        private static readonly Dictionary<Game, Dictionary<int, (Bitmap bitmap, int priority, HashSet<Range> usedTilesets)>> layerCaches = [];

        public static void ResetCache(Game game)
        {
            tilesetCaches.Remove(game);
            layerCaches.Remove(game);
        }

        public static (Bitmap bitmap, Dictionary<Range, Bitmap> tilesets, HashSet<Range> usedTilesets) RenderMap(ROM rom, Game game, int mapOffset, MapRenderOptions options)
        {
            var tilesets = DrawTileset(rom, game, mapOffset);
            var usedTilesets = new HashSet<Range>();

            rom.PushPosition(mapOffset + 0x14);
            var layer0 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            var layer1 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            var layer2 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            var layer3 = DrawLayer(rom, game, rom.ReadPointer(), tilesets, usedTilesets);
            rom.PopPosition();

            rom.PushPosition(mapOffset + 0x8);
            int width = rom.ReadShort() + 240;
            int height = rom.ReadShort() + 160;
            rom.PopPosition();

            var composite = new Bitmap(Math.Max(1, width), Math.Max(1, height));
            using var g = Graphics.FromImage(composite);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            if (options.ShowBG3) g.DrawImage(layer3.bitmap, 0, 0);
            if (options.ShowBG2) g.DrawImage(layer2.bitmap, 0, 0);
            if (options.ShowBG1) g.DrawImage(layer1.bitmap, 0, 0);
            if (options.ShowBG0) g.DrawImage(layer0.bitmap, 0, 0);

            return (composite, tilesets, usedTilesets);
        }

        public static Dictionary<Range, Bitmap> DrawTileset(ROM rom, Game game, int mapOffset)
        {
            var pal = ReadBGPalette(rom, game);

            // animated tile sequences
            rom.PushPosition(mapOffset + 0xC);
            int numberOfSequences = rom.ReadInt();
            int sequencePtr = rom.ReadPointer();

            var tilesets = new Dictionary<Range, Bitmap>();

            if (numberOfSequences > 0 && sequencePtr != 0x0)
            {
                rom.Seek(sequencePtr);
                for (int i = 0; i < numberOfSequences; i++)
                {
                    int sequenceStructPtr = rom.ReadPointer();
                    rom.PushPosition(sequenceStructPtr);

                    int numberOfStrips = rom.ReadByte();
                    int numberOfFrames = rom.ReadByte();
                    int vramOffset = rom.ReadShort();

                    if (numberOfFrames <= 0 || numberOfStrips <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Bad animated-sequence header at 0x{sequenceStructPtr:X} (sequence {i}/{numberOfSequences}): " +
                            $"numberOfStrips={numberOfStrips}, numberOfFrames={numberOfFrames}, vramOffset={vramOffset}");
                    }

                    var sequenceImage = new Bitmap(numberOfFrames * 8, numberOfStrips * 8);

                    for (int j = 0; j < numberOfStrips; j++)
                    {
                        var imgData = JCALG1.DecompressUnknownHeader(rom, rom.ReadPointer());

                        for (int src = 0; src < imgData.Length; src += 64)
                        {
                            int dx = (src / 64) * 8;
                            int dy = j * 8;

                            for (int x = 0; x < 8; x++)
                            {
                                for (int y = 0; y < 8; y++)
                                {
                                    int index = (y * 8) + x;
                                    sequenceImage.SetPixel(dx + x, dy + y, pal[imgData[src + index]]);
                                }
                            }
                        }
                    }

                    tilesets.Add(new Range(vramOffset, vramOffset + numberOfFrames - 1), sequenceImage);
                    rom.PopPosition();
                }
            }
            rom.PopPosition();

            // static tileset
            // Each entry in tilesetBytes is a 2-byte delta: the tile index advances by
            // that delta before placing the next tile. The destination slot in the tileset
            // bitmap is simply the entry's sequential position (i/2), independent of the
            // accumulated tile index used to look up which atlas tile to draw.
            rom.PushPosition(mapOffset + 0x48);
            byte[] tilesetBytes = JCALG1.Decompress(rom, rom.ReadPointer());
            int tileCount = tilesetBytes.Length / 2;
            int tsColumns = 16;
            int imageWidth = tsColumns * 8;
            int imageHeight = (int)Math.Ceiling((float)tileCount / tsColumns) * 8;

            if (tilesetBytes.Length == 0 || imageHeight <= 0)
            {
                throw new InvalidOperationException(
                    $"Static tileset decompressed to {tilesetBytes.Length} bytes at mapOffset 0x{mapOffset:X} (+0x48) " +
                    $"-> imageWidth={imageWidth}, imageHeight={imageHeight}");
            }

            var tileset = new Bitmap(imageWidth, Math.Max(8, imageHeight));
            using var ts = Graphics.FromImage(tileset);
            ts.InterpolationMode = InterpolationMode.NearestNeighbor;
            ts.PixelOffsetMode = PixelOffsetMode.Half;

            int currentTileIndex = 0;
            for (int i = 0; i < tilesetBytes.Length; i += 2)
            {
                // Delta-decode: advance the atlas tile index by the stored offset.
                int delta = tilesetBytes[i] | (tilesetBytes[i + 1] << 8);
                currentTileIndex += delta;

                // Source: which tile in the atlas to pull from.
                var src = GetTilesetImage(rom, game, currentTileIndex, pal);
                int atlasLocal = currentTileIndex % 256;
                int srcX = (atlasLocal % 16) * tile_size;
                int srcY = (atlasLocal / 16) * tile_size;

                // Destination: sequential slot in the output tileset bitmap.
                int slot = i / 2;
                int destX = (slot % tsColumns) * tile_size;
                int destY = (slot / tsColumns) * tile_size;

                ts.DrawImage(src, destX, destY, new Rectangle(srcX, srcY, tile_size, tile_size), GraphicsUnit.Pixel);

                // Advance past this tile so the next delta is relative to the tile
                // after the one we just placed.
                currentTileIndex++;
            }

            // The static tileset covers slots 0..(tileCount-1).
            tilesets.Add(new Range(0, tileCount - 1), tileset);
            rom.PopPosition();

            return tilesets;
        }

        public static Bitmap GetTilesetImage(ROM rom, Game game, int tileId, Color[] palette)
        {
            int tilesetId = tileId / tile_per_image;
            var cache = GetTilesetCache(game);

            if (cache.TryGetValue(tilesetId, out var bitmap))
            {
                return bitmap;
            }

            bitmap = new Bitmap(128, 128);
            rom.PushPosition(game.TileAtlasOffset + (tilesetId * 4));
            var imgData = JCALG1.Decompress(rom, rom.ReadPointer());

            for (int src = 0; src < imgData.Length; src += 64)
            {
                int dx = (src % 1024) / 8;
                int dy = (src / 1024) * 8;

                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        int index = (y * 8) + x;
                        bitmap.SetPixel(dx + x, dy + y, palette[imgData[src + index]]);
                    }
                }
            }

            rom.PopPosition();
            cache.Add(tilesetId, bitmap);
            return bitmap;
        }

        private static Color[] ReadBGPalette(ROM rom, Game game)
        {
            rom.PushPosition(game.BGPaletteOffset);
            var colors = new Color[256];

            for (int i = 0; i < colors.Length; i++)
            {
                int c = rom.ReadShort();
                int r = ((c & 0x1F) * 0x21) >> 2;
                int g = (((c >> 5) & 0x1F) * 0x21) >> 2;
                int b = (((c >> 10) & 0x1F) * 0x21) >> 2;
                colors[i] = Color.FromArgb(0xFF, r, g, b);
            }

            colors[0] = Color.FromArgb(0, 0, 0, 0);
            rom.PopPosition();
            return colors;
        }

        // Layer-type vtable pointers are compiled-in function addresses, so they
        // differ per ROM build even when the underlying format is identical.
        // These are this ROM's values (confirmed against real layer structs):
        //   tiled-grid layer -> 0x0056A1   (original LOGExtractor ROM: 0x8087)
        private const int TiledLayerType = 0x0056A1;

        public static (Bitmap bitmap, int priority) DrawLayer(ROM rom, Game game, int address, Dictionary<Range, Bitmap> tilesets, HashSet<Range> usedTilesets)
        {
            var cache = GetLayerCache(game);

            if (cache.TryGetValue(address, out var cached))
            {
                // Cached layers don't re-walk their chunks, so make sure the
                // ranges they used originally still get reported this time.
                foreach (var range in cached.usedTilesets)
                {
                    usedTilesets.Add(range);
                }
                return (cached.bitmap, cached.priority);
            }

            if (address == 0x0)
            {
                return (new Bitmap(1, 1), 0);
            }

            rom.PushPosition(address);

            int layerType = rom.ReadPointer();
            if (layerType != TiledLayerType)
            {
                rom.PopPosition();
                return (new Bitmap(1, 1), 0);
            }

            rom.Skip(0x9);
            int offX = rom.ReadShort() / 4;
            rom.Skip(0x2);

            // +0x11 = scroll Y low byte, +0x12 = BG priority
            int offYRaw = rom.ReadShort();
            int offY = (offYRaw & 0xFF) / 4;
            int priority = (offYRaw >> 8) & 0xFF;

            rom.Skip(0x1);
            int numCols = rom.ReadByte();
            int numRows = rom.ReadByte();

            if (offX >= 0x3F00)
            {
                offX -= 0x3F00;
                offX *= -1;
            }

            if (offY >= 0x3F00)
            {
                offY -= 0x3F00;
                offY *= -1;
            }

            var layerImage = new Bitmap(Math.Max(1, (numCols * chunk_size_pixels) - offX), Math.Max(1, (numRows * chunk_size_pixels) - offY));
            using var g = Graphics.FromImage(layerImage);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            rom.Skip(0x2);

            var layerUsedTilesets = new HashSet<Range>();

            for (int r = 0; r < numRows; r++)
            {
                for (int c = 0; c < numCols; c++)
                {
                    var chunk = DrawChunk(rom, rom.ReadPointer(), tilesets, layerUsedTilesets);
                    g.DrawImage(chunk, (chunk_size_pixels * c) - offX, (chunk_size_pixels * r) - offY);
                }
            }

            foreach (var range in layerUsedTilesets)
            {
                usedTilesets.Add(range);
            }

            var result = (layerImage, priority, layerUsedTilesets);
            cache.Add(address, result);
            rom.PopPosition();

            return (result.layerImage, result.priority);
        }

        private static Bitmap DrawChunk(ROM rom, int address, Dictionary<Range, Bitmap> tilesets, HashSet<Range> usedTilesets)
        {
            var chunk_image = new Bitmap(chunk_size_pixels, chunk_size_pixels);

            if (address == 0x0)
            {
                return chunk_image;
            }

            using var g = Graphics.FromImage(chunk_image);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            byte[] bytes = JCALG1.Decompress(rom, address);

            int x = 0;
            int y = 0;

            for (int i = 0; i < bytes.Length; i += 2)
            {
                byte lsb = bytes[i];
                byte msb = bytes[i + 1];
                int entry = (msb << 8) + lsb;

                int tileId = entry & 0x3FF;
                bool flipX = (entry & (1 << 10)) != 0;
                bool flipY = (entry & (1 << 11)) != 0;

                // Tile ID 0 is the GBA transparent/blank tile — skip drawing but still
                // advance position so subsequent tiles land in the right slot.
                if (tileId != 0)
                {
                    bool found = false;
                    Range range = default;

                    foreach (var k in tilesets.Keys)
                    {
                        if (tileId >= k.Start.Value && tileId <= k.End.Value)
                        {
                            range = k;
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        usedTilesets.Add(range);

                        var tileset = tilesets[range];
                        int columns = tileset.Width / tile_size;
                        int l = tileId - range.Start.Value;
                        int srcX = (l % columns) * tile_size;
                        int srcY = (l / columns) * tile_size;

                        if (flipX || flipY)
                        {
                            g.DrawImage(FlipTile(tileset, srcX, srcY, flipX, flipY), x, y);
                        }
                        else
                        {
                            g.DrawImage(tileset, x, y, new Rectangle(srcX, srcY, tile_size, tile_size), GraphicsUnit.Pixel);
                        }
                    }
                }

                x += tile_size;
                if (x >= chunk_size_pixels)
                {
                    x = 0;
                    y += tile_size;
                }
            }

            return chunk_image;
        }

        private static Bitmap FlipTile(Bitmap tileset, int srcX, int srcY, bool flipX, bool flipY)
        {
            var tile_image = new Bitmap(tile_size, tile_size);
            using var g = Graphics.FromImage(tile_image);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.DrawImage(tileset, 0, 0, new Rectangle(srcX, srcY, tile_size, tile_size), GraphicsUnit.Pixel);

            if (flipX && flipY)
            {
                tile_image.RotateFlip(RotateFlipType.RotateNoneFlipXY);
            }
            else if (flipX)
            {
                tile_image.RotateFlip(RotateFlipType.RotateNoneFlipX);
            }
            else if (flipY)
            {
                tile_image.RotateFlip(RotateFlipType.RotateNoneFlipY);
            }

            return tile_image;
        }

        private static Dictionary<int, Bitmap> GetTilesetCache(Game game)
        {
            if (!tilesetCaches.TryGetValue(game, out var cache))
            {
                cache = [];
                tilesetCaches[game] = cache;
            }
            return cache;
        }

        private static Dictionary<int, (Bitmap bitmap, int priority, HashSet<Range> usedTilesets)> GetLayerCache(Game game)
        {
            if (!layerCaches.TryGetValue(game, out var cache))
            {
                cache = [];
                layerCaches[game] = cache;
            }
            return cache;
        }
    }

    public readonly struct MapRenderOptions
    {
        public bool ShowBG0 { get; init; } = true;
        public bool ShowBG1 { get; init; } = true;
        public bool ShowBG2 { get; init; } = true;
        public bool ShowBG3 { get; init; } = true;
        public MapRenderOptions() { }
    }
}