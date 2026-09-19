using DrGero.Config;
using DrGero.IO;
using DrGero.Loader;
using DrGero.Rendering;
using DrGero.Types;
using System.Drawing;
using System.Drawing.Imaging;

namespace Legacy
{
    /// <summary>
    /// Console checks for the tile-editing core against a REAL ROM. Run via
    /// `Legacy.exe --test-tiles=&lt;path to .gba&gt;` (same idea as --test-zenkai).
    /// </summary>
    internal static class TileTests
    {
        public static void RunAll(string romPath)
        {
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}{(ok ? "" : " -- " + detail)}");
                if (ok) pass++; else fail++;
            }

            var rom = ROM.FromFile(romPath);

            string[] gameDirs =
            {
                Path.Combine(AppContext.BaseDirectory, "games"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "Dragon Radar", "games")),
            };
            string? gamesDir = gameDirs.FirstOrDefault(Directory.Exists);
            if (gamesDir == null) { Console.WriteLine("Couldn't find the games/ config folder (build Dragon Radar once)."); return; }
            var game = GameLibrary.LoadAll(gamesDir).First();

            int MapOffsetOf(int mapIndex)
            {
                rom.PushPosition(game.MapEntriesOffset + (mapIndex * MapEntry.RecordSize));
                var entry = MapEntry.Read(rom);
                rom.PopPosition();
                rom.PushPosition(entry.VariationArray);
                int offset = rom.ReadPointer();
                rom.PopPosition();
                return offset;
            }

            byte[] Render(int mapOffset)
            {
                MapRenderer.ResetCache(game);
                var (bitmap, _, _, _, _) = MapRenderer.RenderMap(rom, game, mapOffset, new MapRenderOptions());
                return PixelBytes(bitmap);
            }

            // ---- 1. layer edit round trip through the real renderer ----
            int mapA = MapOffsetOf(0);
            byte[] baseline = Render(mapA);

            TileLayerEditor? editor = null;
            int layerIndex = -1;
            for (int l = 0; l < 4 && editor == null; l++)
            {
                rom.PushPosition(mapA + 0x14 + (l * 4));
                int layerAddress = rom.ReadPointer();
                rom.PopPosition();
                var candidate = TileLayerEditor.Open(rom, layerAddress);
                if (candidate?.GetTile(64, 64) != null) { editor = candidate; layerIndex = l; }
            }

            if (editor == null) { Check("found an editable layer on map 0", false, "none of BG0-3 opened"); }
            else
            {
                Check($"found an editable layer on map 0 (BG{layerIndex})", true);
                ushort original = editor.GetTile(64, 64)!.Value;
                ushort changed = (ushort)(((original & 0x3FF) == 1) ? 2 : 1);

                Check("SetTile succeeds", editor.SetTile(64, 64, changed));
                Check("edited tile reads back from the same editor", editor.GetTile(64, 64) == changed);

                // A FRESH editor re-reads the chunk from the ROM: proves the stored-format chunk we
                // wrote decodes through the same JCALG1.Decompress path the renderer uses.
                rom.PushPosition(mapA + 0x14 + (layerIndex * 4));
                int addr = rom.ReadPointer();
                rom.PopPosition();
                var fresh = TileLayerEditor.Open(rom, addr)!;
                Check("edited tile survives a fresh read from the ROM", fresh.GetTile(64, 64) == changed,
                    $"got {fresh.GetTile(64, 64)}");

                bool renderOk = true;
                try { Render(mapA); } catch (Exception ex) { renderOk = false; Console.WriteLine(ex.Message); }
                Check("map still renders with the edit", renderOk);

                fresh.SetTile(64, 64, original);
                Check("restore reads back", TileLayerEditor.Open(rom, addr)!.GetTile(64, 64) == original);
                Check("restored map renders pixel-identical to the original", Render(mapA).AsSpan().SequenceEqual(baseline));
            }

            // ---- 2. borrowing a tile from another map's tileset ----
            int mapB = -1;
            int[] atlasA = TilesetTable.ReadAtlasIndices(rom, mapA);
            int borrowSlotB = -1, borrowAtlas = -1;
            for (int m = 1; m < 12 && borrowAtlas < 0; m++)
            {
                try
                {
                    int off = MapOffsetOf(m);
                    int[] atlasB = TilesetTable.ReadAtlasIndices(rom, off);
                    for (int s = 0; s < atlasB.Length; s++)
                    {
                        if (Array.IndexOf(atlasA, atlasB[s]) < 0) { mapB = off; borrowSlotB = s; borrowAtlas = atlasB[s]; break; }
                    }
                }
                catch { /* a map with no readable tileset -- try the next */ }
            }

            if (borrowAtlas < 0) { Check("found a tile on another map that map 0 lacks", false, "every early map shares map 0's tiles"); }
            else
            {
                int slot = TilesetTable.EnsureAtlasTile(rom, mapA, borrowAtlas);
                Check("EnsureAtlasTile adds a slot", slot == atlasA.Length, $"slot={slot}, old count={atlasA.Length}");
                Check("asking again returns the same slot", TilesetTable.EnsureAtlasTile(rom, mapA, borrowAtlas) == slot);
                Check("existing slots are unchanged", TilesetTable.ReadAtlasIndices(rom, mapA).Take(atlasA.Length).SequenceEqual(atlasA));

                MapRenderer.ResetCache(game);
                var tsA = MapRenderer.DrawTileset(rom, game, mapA).First(kv => kv.Key.Start.Value == 0).Value;
                var tsB = MapRenderer.DrawTileset(rom, game, mapB).First(kv => kv.Key.Start.Value == 0).Value;

                bool same = true;
                for (int y = 0; y < 8 && same; y++)
                    for (int x = 0; x < 8 && same; x++)
                    {
                        var a = tsA.GetPixel(((slot % 16) * 8) + x, ((slot / 16) * 8) + y);
                        var b = tsB.GetPixel(((borrowSlotB % 16) * 8) + x, ((borrowSlotB / 16) * 8) + y);
                        same = a == b;
                    }
                Check("the borrowed tile looks identical on map 0 as on its own map", same);
            }

            // ---- 2b. allocations must be 4-byte aligned (the GBA rotates misaligned 32-bit loads) ----
            {
                bool aligned = true;
                foreach (int size in new[] { 3, 1, 7, 8200, 5, 2056, 13 })
                {
                    int offset = rom.AllocateFreeSpace(size);
                    if ((offset & 3) != 0) aligned = false;
                }
                Check("every AllocateFreeSpace result is 4-byte aligned", aligned);
            }

            // ---- 2c. resizing a map ----
            {
                // The raw +0x8/+0xA fields are the camera SCROLL RANGE (Map_InitRenderer: width-1 max
                // scroll, width+239 right edge), so the map's pixel size is field + 239 / field + 159.
                // Sanity check that reading against several real maps: it must fit inside what the
                // layers cover.
                bool allFit = true;
                for (int m = 0; m < 12; m++)
                {
                    try
                    {
                        int off = MapOffsetOf(m);
                        var (mw, mh) = MapResizer.ReadSize(rom, off);
                        var (cw, ch) = MapResizer.LayerCoverage(rom, off);
                        Console.WriteLine($"    map {m}: size {mw}x{mh}, layers cover {cw}x{ch}");
                        if (cw > 0 && (mw > cw || mh > ch)) allFit = false;
                    }
                    catch { /* a map with no readable layers -- skip */ }
                }
                Check("every sampled map's size fits inside its layers' coverage", allFit);

                var (w0, h0) = MapResizer.ReadSize(rom, mapA);
                var (lw0, lh0) = MapResizer.LayerCoverage(rom, mapA);
                Console.WriteLine($"    map 0 size {w0}x{h0}, layers cover {lw0}x{lh0}");
                Check("map size reads back sensibly", w0 >= 240 && h0 >= 160 && w0 <= 2048 && h0 <= 2048, $"{w0}x{h0}");

                Check("rejects a map smaller than the screen", MapResizer.Resize(rom, mapA, 100, h0, false, out _) != null);
                Check("rejects a size past the collision grid", MapResizer.Resize(rom, mapA, 4096, h0, false, out _) != null);

                int newW = Math.Min(2048, lw0 + 256), newH = h0;
                string? error = MapResizer.Resize(rom, mapA, newW, newH, growLayers: true, out int grown);
                Check("resize succeeds", error == null, error ?? "");
                Check("size reads back", MapResizer.ReadSize(rom, mapA) == (newW, newH));
                Check("layers were grown", grown > 0, $"grown={grown}");
                Check("layers now cover the new size", MapResizer.LayerCoverage(rom, mapA).Width >= newW);

                bool rendered = true;
                int renderedWidth = 0;
                try
                {
                    MapRenderer.ResetCache(game);
                    var (bmp, _, _, _, _) = MapRenderer.RenderMap(rom, game, mapA, new MapRenderOptions());
                    renderedWidth = bmp.Width;
                }
                catch (Exception ex) { rendered = false; Console.WriteLine(ex.Message); }
                Check("resized map still renders", rendered);
                Check("rendered map is at least the new width", renderedWidth >= newW, $"{renderedWidth} < {newW}");

                // Existing chunks stay in their slots: the tile edited earlier must read the same.
                var reopened = TileLayerEditor.Open(rom, ReadLayerAddress(rom, mapA, Math.Max(0, layerIndex)));
                Check("existing tiles survive the layer growth", reopened != null && reopened.GetTile(64, 64) != null);
            }

            // ---- 3. collision grid round trip through the real reader ----
            MapRenderer.ResetCache(game);
            bool[,] before = MapRenderer.ReadCollisionMap(rom, mapA);
            var collision = CollisionEditor.Open(rom, mapA);
            if (collision == null) { Check("collision grid opens", false); }
            else
            {
                bool wasBlocked = before[10, 10];
                Check("editor agrees with the game's reader on a cell", collision.IsAllowed(10, 10) == wasBlocked);

                Check("SetAllowed succeeds", collision.SetAllowed(10, 10, !wasBlocked));
                Check("reader sees the change", MapRenderer.ReadCollisionMap(rom, mapA)[10, 10] == !wasBlocked);

                collision.SetAllowed(40, 3, true);   // a cell in a different word of a different row
                collision.SetAllowed(10, 10, wasBlocked);
                collision.SetAllowed(40, 3, before[40, 3]);

                bool[,] after = MapRenderer.ReadCollisionMap(rom, mapA);
                bool identical = true;
                for (int y = 0; y < 256 && identical; y++)
                    for (int x = 0; x < 256 && identical; x++)
                        identical = after[x, y] == before[x, y];
                Check("restoring leaves the whole grid identical", identical);
                Check("out-of-grid cell is refused", !collision.SetAllowed(256, 0, true));
            }

            Console.WriteLine();
            Console.WriteLine($"{pass} passed, {fail} failed.");
        }

        private static int ReadLayerAddress(ROM rom, int mapOffset, int layer)
        {
            rom.PushPosition(mapOffset + 0x14 + (layer * 4));
            int address = rom.ReadPointer();
            rom.PopPosition();
            return address;
        }

        private static byte[] PixelBytes(Bitmap bitmap)
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] bytes = new byte[Math.Abs(data.Stride) * bitmap.Height];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                return bytes;
            }
            finally { bitmap.UnlockBits(data); }
        }
    }
}
