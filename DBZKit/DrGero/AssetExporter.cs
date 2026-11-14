using System.Drawing.Imaging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DrGero.IO;
using DrGero.Rendering;
using Game = DrGero.Config.Game;

namespace DrGero.Catalog
{
    /// <summary>
    /// Writes every asset the catalog knows to a folder tree, one folder per kind, for the decomp's build:
    ///   &lt;root&gt;/&lt;kind&gt;/&lt;asset&gt;/  what it looks or reads like (PNG / TXT / the preview's own data file),
    ///                            res_XXXXXXXX.bin   each resource block it owns, unpacked (the form to edit),
    ///                            res_XXXXXXXX.rom   the same block exactly as stored in the ROM (header + compressed stream),
    ///                            data_XXXXXXXX.bin  other ROM bytes it owns that aren't resource blocks (tables, raw images);
    ///   &lt;root&gt;/manifest.json     every asset with its ROM addresses, sizes, compression mode and files,
    ///                            so a builder can put each block back (or recompress an edited one) and repoint what uses it.
    /// The catalog must be indexed first: that is what says which asset owns which block.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    public static class AssetExporter
    {
        public sealed record Result(int Assets, int Blocks, int Files, List<string> Problems);

        private sealed record BlockEntry(string RomAddress, int RomLength, int Mode, int Size, string Packed, string Unpacked);
        private sealed record RangeEntry(string RomAddress, int Length, string File);
        private sealed record AssetEntry(
            string Kind, string Name, string Folder, int Id, string? RomAddress,
            Dictionary<string, string> Details, List<string> Files, List<BlockEntry> Blocks, List<RangeEntry> Data);
        private sealed record Manifest(string Game, string RomSha1, string Note, List<AssetEntry> Assets);

        /// <param name="skip">Kinds not to write (e.g. "Sound", when the decomp already extracts audio its own way).</param>
        public static Result Export(byte[] romBytes, Game game, AssetCatalog catalog, string root, IEnumerable<string>? skip = null,
            IProgress<(int Done, int Total, string What)>? progress = null, CancellationToken cancel = default)
        {
            if (!catalog.Indexed) throw new InvalidOperationException("Index the catalog first: it says which asset owns which block.");
            var skipped = new HashSet<string>(skip ?? [], StringComparer.OrdinalIgnoreCase);
            var owned = catalog.Blocks.Where(b => b.Owner != null).GroupBy(b => b.Owner!).ToDictionary(g => g.Key, g => g.ToList());

            // Every asset with something to write: a preview, blocks, or raw ranges. Groups (zones, regions of the ROM) become folders.
            var jobs = new List<(AssetCategory Category, AssetNode Node, string Folder)>();
            foreach (var category in catalog.Categories.Where(c => !skipped.Contains(c.Name)))
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                void Walk(IEnumerable<AssetNode> nodes, string folder)
                {
                    foreach (var node in nodes)
                    {
                        if (node.Children.Count > 0 && node.Preview == null) { Walk(node.Children, Path.Combine(folder, Slug(node.Name))); continue; }
                        string name = Slug(node.Name);
                        string path = Path.Combine(folder, name);
                        for (int n = 2; !used.Add(path); n++) path = Path.Combine(folder, $"{name}_{n}");
                        jobs.Add((category, node, path));
                        if (node.Children.Count > 0) Walk(node.Children, path);
                    }
                }
                Walk(category.Nodes, Slug(category.Name));
            }

            var entries = new AssetEntry?[jobs.Count];
            var problems = new System.Collections.Concurrent.ConcurrentBag<string>();
            int done = 0, files = 0, blocks = 0;
            var options = new ParallelOptions { CancellationToken = cancel, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            // Each worker has its own ROM copy (ROM reads move a shared position) and its own profile copy: the map renderer keeps
            // its tile caches per Game, and sharing one between threads draws over the same bitmaps.
            Parallel.For(0, jobs.Count, options, () => (Rom: ROM.FromBytes((byte[])romBytes.Clone()), Game: game.Clone()), (i, _, local) =>
            {
                var (rom, workerGame) = local;
                var (category, node, folder) = jobs[i];
                string dir = Path.Combine(root, folder);
                Directory.CreateDirectory(dir);
                var written = new List<string>();
                void Write(string file, byte[] data)
                {
                    File.WriteAllBytes(Path.Combine(dir, file), data);
                    written.Add(Path.Combine(folder, file).Replace('\\', '/'));
                }

                if (node.Preview != null)
                {
                    try
                    {
                        using var preview = node.Preview(rom, workerGame);
                        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var (label, image) in preview.Images)
                        {
                            string file = Slug(label.Length > 0 ? label : "image");
                            for (int n = 2; !names.Add(file); n++) file = $"{Slug(label)}_{n}";
                            using var ms = new MemoryStream();
                            image.Save(ms, ImageFormat.Png);
                            Write(file + ".png", ms.ToArray());
                        }
                        if (preview.Text != null) Write("text.txt", Encoding.UTF8.GetBytes(preview.Text.Replace("\r\n", "\n")));
                        if (preview.Raw != null) Write("data." + preview.RawExtension, preview.Raw);
                    }
                    catch (Exception e) { problems.Add($"{category.Name} / {node.Name}: couldn't draw ({e.Message})"); }
                }

                var blockList = new List<BlockEntry>();
                foreach (var b in owned.TryGetValue(node, out var mine) ? mine : [])
                {
                    string stem = $"res_{0x08000000 | b.Address:X8}";
                    Write(stem + ".rom", rom.ReadBytesAt(b.Address, b.End - b.Address));
                    try { Write(stem + ".bin", JCALG1.Decompress(rom, b.Address)); }
                    catch (Exception e) { problems.Add($"{stem}: couldn't unpack ({e.Message})"); }
                    blockList.Add(new BlockEntry($"0x{0x08000000 | b.Address:X8}", b.End - b.Address, b.Mode, b.Size,
                        Path.Combine(folder, stem + ".rom").Replace('\\', '/'), Path.Combine(folder, stem + ".bin").Replace('\\', '/')));
                }
                var rangeList = new List<RangeEntry>();
                foreach (var (start, length) in node.Ranges)
                {
                    if (length <= 0 || start < 0 || start + length > rom.Length) continue;
                    string file = $"data_{0x08000000 | start:X8}.bin";
                    Write(file, rom.ReadBytesAt(start, length));
                    rangeList.Add(new RangeEntry($"0x{0x08000000 | start:X8}", length, Path.Combine(folder, file).Replace('\\', '/')));
                }

                entries[i] = new AssetEntry(category.Name, node.Name, folder.Replace('\\', '/'), node.Id,
                    node.Address >= 0 ? $"0x{0x08000000 | node.Address:X8}" : null,
                    node.Details.GroupBy(d => d.Label).ToDictionary(g => g.Key, g => g.First().Value), written, blockList, rangeList);
                Interlocked.Add(ref files, written.Count);
                Interlocked.Add(ref blocks, blockList.Count);
                int now = Interlocked.Increment(ref done);
                progress?.Report((now, jobs.Count, node.Name));
                return local;
            }, local => MapRenderer.ResetCache(local.Game));

            var manifest = new Manifest(game.GAME_CODE, Convert.ToHexString(SHA1.HashData(romBytes)).ToLowerInvariant(),
                "Written by DBZKit's asset export. res_*.rom = the block exactly as in the ROM (8-byte ResourceHeader {mode, size} + data; " +
                "mode 0 stored, 1/2 JCALG1); res_*.bin = unpacked; data_*.bin = other ROM bytes the asset owns. Addresses are GBA (0x08...).",
                [.. entries.OfType<AssetEntry>()]);
            File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            return new Result(manifest.Assets.Count, blocks, files + 1, [.. problems.OrderBy(p => p)]);
        }

        /// <summary>A file/folder name: lower case, letters and digits, words joined by "_" ("Cell, "Perfect" (3)" -> "cell_perfect_3").</summary>
        public static string Slug(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
                else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
            }
            string slug = sb.ToString().Trim('_');
            return slug.Length == 0 ? "item" : slug.Length > 60 ? slug[..60].TrimEnd('_') : slug;
        }
    }
}
