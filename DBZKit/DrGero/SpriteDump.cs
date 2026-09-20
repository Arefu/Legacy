using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using DrGero.IO;

namespace DrGero.Engine
{
    /// <summary>
    /// Dumps character sprites to a folder tree and puts them back.
    ///
    ///   &lt;root&gt;/sprite_072/manifest.json
    ///   &lt;root&gt;/sprite_072/g05_down_f0.png   indexed 8bpp PNG using the game's shared OBJ palette (index 0 = transparent), in the orientation
    ///                                        the game shows it (a stock "Right" frame is the Left image flipped, so it is exported flipped)
    ///   &lt;root&gt;/sprite_072/g05_down_f0.bin   the raw 8x8-tile bytes (64 per tile, row-major tile order) the game decompresses
    ///
    /// A sprite record holds 20 groups (Down/Up/Left/Right) and every slot points at an array of frames (see FrameImport.WriteFrameArray),
    /// so a file is named by group (g05), direction and frame (f0, f1, ...). The manifest keeps each frame's x/y offsets so an unedited dump
    /// imports back to the same picture. Import writes every frame in the folder as its own image at the end of the ROM.
    /// </summary>
    public static class SpriteDump
    {
        public sealed record FrameEntry(int Group, int Slot, int Index, int Width, int Height, int X, int Y, bool Flipped, string File);

        public sealed class Manifest
        {
            public int Sprite { get; set; }
            public List<FrameEntry> Frames { get; set; } = [];
        }

        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
        private const int ObjPalette = FrameImport.ObjPalette;

        public static string FolderName(int spriteId) => $"sprite_{spriteId:D3}";
        private static string FileStem(int group, int slot, int frame) => $"g{group:D2}_{FrameImport.SlotNames[slot].ToLowerInvariant()}_f{frame}";

        public static List<int> SpriteIds(ROM rom) => Enumerable.Range(0, FrameImport.IndexCapacity).Where(id => FrameImport.HasRecord(rom, id)).ToList();

        /// <summary>
        /// Every frame of a sprite as a bitmap, labelled like the dump's file names (g05_down_f0). What the Sprite Viewer shows, so the viewer and
        /// the dump always agree. Bitmaps are in the orientation the game shows them (flipped frames are flipped) with index 0 transparent.
        /// </summary>
        public static List<(string Label, int Width, int Height, Bitmap Image)> RenderFrames(ROM rom, int spriteId)
        {
            var result = new List<(string, int, int, Bitmap)>();
            uint ptr = BitConverter.ToUInt32(rom.ReadBytesAt(FrameImport.IndexAddress(rom) + spriteId * 4, 4));
            if (!FrameImport.IsRomPtr(ptr, rom)) return result;
            int rec = (int)(ptr & 0x00FFFFFF);
            var palette = ReadPalette(rom);
            for (int g = 0; g < FrameImport.GroupsIn(rom, spriteId); g++)
                for (int slot = 0; slot < 4; slot++)
                {
                    uint sp = BitConverter.ToUInt32(rom.ReadBytesAt(rec + FrameImport.FirstGroup + g * FrameImport.GroupSize + slot * 4, 4));
                    var frames = FrameImport.ReadStockArray(rom, rec, sp);
                    for (int f = 0; f < frames.Count; f++)
                    {
                        var d = frames[f];
                        int w = d[2], h = d[3];
                        byte[] tiles;
                        try { tiles = Rendering.JCALG1.Decompress(rom, (int)(BitConverter.ToUInt32(d, 8) & 0x00FFFFFF)); }
                        catch { continue; }
                        if (tiles.Length < w * h) continue;
                        var idx = TilesToIndices(tiles, w, h);
                        if ((d[7] & 0x10) != 0) FlipX(idx, w, h);
                        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                byte i = idx[y * w + x];
                                if (i != 0 && i < palette.Length) bmp.SetPixel(x, y, palette[i]);
                            }
                        result.Add((FileStem(g, slot, f), w, h, bmp));
                    }
                }
            return result;
        }

        /// <summary>Writes one sprite's frames; returns how many frames were written.</summary>
        public static int DumpSprite(ROM rom, int spriteId, string root)
        {
            uint ptr = BitConverter.ToUInt32(rom.ReadBytesAt(FrameImport.IndexAddress(rom) + spriteId * 4, 4));
            if (!FrameImport.IsRomPtr(ptr, rom)) return 0;
            int rec = (int)(ptr & 0x00FFFFFF);
            string dir = Path.Combine(root, FolderName(spriteId));
            var palette = ReadPalette(rom);
            var manifest = new Manifest { Sprite = spriteId };

            for (int g = 0; g < FrameImport.GroupsIn(rom, spriteId); g++)
                for (int slot = 0; slot < 4; slot++)
                {
                    uint sp = BitConverter.ToUInt32(rom.ReadBytesAt(rec + FrameImport.FirstGroup + g * FrameImport.GroupSize + slot * 4, 4));
                    var frames = FrameImport.ReadStockArray(rom, rec, sp);
                    for (int f = 0; f < frames.Count; f++)
                    {
                        var d = frames[f];
                        int w = d[2], h = d[3];
                        byte[] tiles;
                        try { tiles = Rendering.JCALG1.Decompress(rom, (int)(BitConverter.ToUInt32(d, 8) & 0x00FFFFFF)); }
                        catch { continue; }
                        if (tiles.Length < w * h) continue;
                        Directory.CreateDirectory(dir);
                        string stem = FileStem(g, slot, f);
                        bool flipped = (d[7] & 0x10) != 0;
                        var indices = TilesToIndices(tiles, w, h);
                        if (flipped) FlipX(indices, w, h);
                        SaveIndexedPng(Path.Combine(dir, stem + ".png"), indices, w, h, palette);
                        File.WriteAllBytes(Path.Combine(dir, stem + ".bin"), tiles[..(w * h)]);
                        manifest.Frames.Add(new FrameEntry(g, slot, f, w, h, (sbyte)d[0], (sbyte)d[1], flipped, stem));
                    }
                }
            if (manifest.Frames.Count == 0) return 0;
            if (!File.Exists(Path.Combine(root, "obj_palette.gpl"))) ExportPalette(rom, root);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), JsonSerializer.Serialize(manifest, Json));
            return manifest.Frames.Count;
        }

        /// <summary>Puts a dumped sprite folder back over <paramref name="targetSprite"/> (which may be a different sprite: animations can be copied).</summary>
        public static string ImportSprite(ROM rom, string folder, int targetSprite, HashSet<int> relocated)
        {
            string manifestPath = Path.Combine(folder, "manifest.json");
            if (!File.Exists(manifestPath)) throw new InvalidOperationException($"{folder} has no manifest.json (it is not a sprite dump folder).");
            var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), Json) ?? throw new InvalidDataException("Bad manifest.");
            if (!FrameImport.HasRecord(rom, targetSprite)) throw new InvalidOperationException($"Sprite {targetSprite} has no record to write into.");

            if (relocated.Add(targetSprite)) FrameImport.RelocateRecord(rom, targetSprite);
            int slots = 0, frames = 0;
            var sizeChanges = new List<string>();
            foreach (var slotGroup in manifest.Frames.GroupBy(f => (f.Group, f.Slot)).OrderBy(g => g.Key))
            {
                var list = new List<(FrameImport.Converted, sbyte, sbyte)>();
                foreach (var f in slotGroup.OrderBy(f => f.Index))
                {
                    string png = Path.Combine(folder, f.File + ".png");
                    if (!File.Exists(png)) throw new FileNotFoundException($"Missing {f.File}.png listed in the manifest.");
                    var c = FrameImport.FromPng(png, rom);
                    if (c.Width != f.Width || c.Height != f.Height) sizeChanges.Add($"{f.File} {f.Width}x{f.Height} -> {c.Width}x{c.Height}");
                    list.Add((c, (sbyte)f.X, (sbyte)f.Y));
                }
                FrameImport.WriteFrameSequence(rom, targetSprite, FrameImport.FirstGroup + slotGroup.Key.Group * FrameImport.GroupSize + slotGroup.Key.Slot * 4, list);
                slots++; frames += list.Count;
            }
            return $"Sprite {targetSprite}: wrote {frames} frame(s) in {slots} slot(s) from {Path.GetFileName(folder)}." +
                   (sizeChanges.Count > 0 ? $" Sizes changed ({sizeChanges.Count}): the game reserves sprite memory for stock sizes, so test these: {string.Join(", ", sizeChanges.Take(4))}." : "");
        }

        /// <summary>Finds the sprite dump folders under a root (or the root itself if it is one). Returns (source id, folder).</summary>
        public static List<(int Id, string Folder)> FindDumps(string root)
        {
            var found = new List<(int, string)>();
            if (File.Exists(Path.Combine(root, "manifest.json")) && TryId(Path.GetFileName(root), out int self)) found.Add((self, root));
            if (Directory.Exists(root))
                foreach (var d in Directory.GetDirectories(root, "sprite_*").OrderBy(d => d))
                    if (File.Exists(Path.Combine(d, "manifest.json")) && TryId(Path.GetFileName(d), out int id)) found.Add((id, d));
            return found;
        }

        private static bool TryId(string name, out int id)
        {
            id = 0;
            return name.StartsWith("sprite_", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(7), out id);
        }

        /// <summary>
        /// Writes the shared 256-colour OBJ palette every character sprite indexes into, in formats image editors load: obj_palette.gpl (GIMP /
        /// Krita / Aseprite), obj_palette.pal (JASC, Paint Shop Pro / Aseprite) and obj_palette.png (a 16x16 swatch sheet, index = row*16+column).
        /// Recolouring = painting with THESE colours in indexed mode; a true-colour PNG is matched to the nearest entry on import.
        /// </summary>
        public static void ExportPalette(ROM rom, string folder)
        {
            Directory.CreateDirectory(folder);
            var pal = ReadPalette(rom);
            var gpl = new System.Text.StringBuilder("GIMP Palette\nName: Legacy of Goku II OBJ palette\nColumns: 16\n#\n");
            var jasc = new System.Text.StringBuilder("JASC-PAL\n0100\n256\n");
            for (int i = 0; i < 256; i++)
            {
                var c = i < pal.Length ? pal[i] : Color.Black;
                if (i == 0) c = Color.FromArgb(255, 255, 0, 255);   // transparent key, shown as magenta
                gpl.Append($"{c.R,3} {c.G,3} {c.B,3}  index {i}\n");
                jasc.Append($"{c.R} {c.G} {c.B}\n");
            }
            File.WriteAllText(Path.Combine(folder, "obj_palette.gpl"), gpl.ToString());
            File.WriteAllText(Path.Combine(folder, "obj_palette.pal"), jasc.ToString());
            using var sheet = new Bitmap(16 * 24, 16 * 24);
            using (var g = Graphics.FromImage(sheet))
                for (int i = 0; i < 256; i++)
                {
                    var c = i < pal.Length ? pal[i] : Color.Black;
                    if (i == 0) c = Color.FromArgb(255, 255, 0, 255);
                    using var brush = new SolidBrush(c);
                    g.FillRectangle(brush, i % 16 * 24, i / 16 * 24, 24, 24);
                }
            sheet.Save(Path.Combine(folder, "obj_palette.png"), ImageFormat.Png);
        }

        // ---- pixels ---------------------------------------------------------------------------------------------
        private static Color[] ReadPalette(ROM rom) =>
            Rendering.ItemIconReader.ReadOBJPalette(rom, new Config.Game { OBJPaletteOffset = ObjPalette, CharacterSpriteIndexOffset = FrameImport.CharacterSpriteIndex });

        private static byte[] TilesToIndices(byte[] tiles, int w, int h)
        {
            var idx = new byte[w * h];
            int tw = w / 8;
            for (int t = 0; t < tw * (h / 8); t++)
                for (int py = 0; py < 8; py++)
                    for (int px = 0; px < 8; px++)
                        idx[(t / tw * 8 + py) * w + t % tw * 8 + px] = tiles[t * 64 + py * 8 + px];
            return idx;
        }

        private static void FlipX(byte[] idx, int w, int h)
        {
            for (int y = 0; y < h; y++) Array.Reverse(idx, y * w, w);
        }

        private static void SaveIndexedPng(string path, byte[] idx, int w, int h, Color[] palette)
        {
            using var bmp = new Bitmap(w, h, PixelFormat.Format8bppIndexed);
            var pal = bmp.Palette;
            for (int i = 0; i < 256; i++) pal.Entries[i] = i < palette.Length ? palette[i] : Color.Black;
            pal.Entries[0] = Color.FromArgb(0, 255, 0, 255);   // index 0 is the transparent key
            bmp.Palette = pal;
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            try
            {
                for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(idx, y * w, data.Scan0 + y * data.Stride, w);
            }
            finally { bmp.UnlockBits(data); }
            bmp.Save(path, ImageFormat.Png);
        }
    }
}
