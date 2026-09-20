using System.Drawing;
using System.Drawing.Imaging;
using DrGero.IO;

namespace DrGero.Engine
{
    /// <summary>
    /// Which animation an ability plays, and how to change it. Every stock OnUse function is the same 24-byte Thumb routine:
    ///   push {r3,lr}; ldrh r2,[r0,#4]; sub r2,#EP; strh r2,[r0,#4]; mov r0,r1; ldr r1,[r1]; ldr r2,[r1,#0x28]; movs r1,#ANIM; bl Ptr_InvokeHandler; pop {r3,pc}
    /// (CERTAINTY: HIGH for the byte pattern -- checked on abilities 0-4, 8, 9, 10, 13 of the US ROM; clones we write have a different shape -- see CloneWithAnimation -- because free space is beyond BL range.)
    /// Changing an ability's animation clones that routine into free space with a new ANIM byte and repoints the ability's OnUse at the
    /// clone. Ability 6/7 and the transformations use different code and report null ("custom code").
    /// The animation's init then sets entity+0x4C = spriteRecord+K, so the character's sprite needs frames at K (see BlockOf).
    /// </summary>
    public static class AbilityAnimations
    {
        /// <summary>Animations whose init function is known to be safe to run for the player (HIGH certainty on ids and blocks, from IDA).</summary>
        public static readonly IReadOnlyDictionary<int, (string Name, int Block)> Known = new Dictionary<int, (string, int)>
        {
            [34] = ("Ki Blast", 156), [44] = ("Scatter Shot", 156), [45] = ("Masenko ball", 156), [46] = ("Big Bang", 156), [47] = ("Burning attack", 156),
            [48] = ("Kamehameha", 364), [49] = ("Special Beam Cannon", 364), [50] = ("Sword Blast", 364), [51] = ("Spirit Bomb", 380),
        };

        public static int BlockOf(int animation) => Known.TryGetValue(animation, out var k) ? k.Block : -1;

        private static int Off(uint p) => (int)(p & 0x00FFFFFF) & ~1;

        // Original shape: ... movs r1,#ANIM at [16..17]; bl at [18..21]; pop {r3,pc}.  Clone shape (ours): movs r1,#ANIM at [14..15]; ldr r3,[pc,#0]; bx r3; .word.
        private static bool IsOriginalShape(byte[] f) =>
            f.Length >= 24 && f[0] == 0x08 && f[1] == 0xB5 && f[2] == 0x82 && f[3] == 0x88 && f[5] == 0x3A && f[6] == 0x82 && f[7] == 0x80 &&
            f[17] == 0x21 && (f[19] & 0xF8) == 0xF0 && (f[21] & 0xF8) == 0xF8 && f[22] == 0x08 && f[23] == 0xBD;

        private static bool IsCloneShape(byte[] f) =>
            f.Length >= 24 && f[0] == 0x82 && f[1] == 0x88 && f[3] == 0x3A && f[4] == 0x82 && f[5] == 0x80 && f[15] == 0x21 &&
            f[16] == 0x00 && f[17] == 0x4B && f[18] == 0x18 && f[19] == 0x47;

        private static bool IsStandard(byte[] f) => IsOriginalShape(f) || IsCloneShape(f);
        private static int AnimOf(byte[] f) => IsCloneShape(f) ? f[14] : f[16];
        private static int CostOf(byte[] f) => IsCloneShape(f) ? f[2] : f[4];

        /// <summary>The animation index the ability's OnUse plays, or null when it isn't the standard routine.</summary>
        public static int? Get(ROM rom, AbilityEntry a)
        {
            if (a.OnUse is < 0x08000000 or >= 0x0A000000) return null;
            var f = rom.ReadBytesAt(Off(a.OnUse), 24);
            return IsStandard(f) ? AnimOf(f) : null;
        }

        public static int? EpCost(ROM rom, AbilityEntry a)
        {
            if (a.OnUse is < 0x08000000 or >= 0x0A000000) return null;
            var f = rom.ReadBytesAt(Off(a.OnUse), 24);
            return IsStandard(f) ? CostOf(f) : null;
        }

        /// <summary>Clones the ability's OnUse with a different animation. Returns the new function address (Thumb bit set).</summary>
        public static uint CloneWithAnimation(ROM rom, AbilityEntry a, int animation)
        {
            if (animation is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(animation));
            int src = Off(a.OnUse);
            var f = rom.ReadBytesAt(src, 24);
            if (!IsStandard(f)) throw new InvalidOperationException("This ability's OnUse isn't the standard routine, so its animation can't be swapped automatically.");

            int target;
            byte[] body;
            if (IsCloneShape(f)) { target = (int)(BitConverter.ToUInt32(f, 20) & 0x00FFFFFF) & ~1; body = f[..16]; }
            else
            {
                // Decode the original BL to find Ptr_InvokeHandler.
                int hw1 = f[18] | f[19] << 8, hw2 = f[20] | f[21] << 8;
                int off = ((hw1 & 0x7FF) << 12) | ((hw2 & 0x7FF) << 1);
                if ((off & 0x400000) != 0) off -= 0x800000;
                target = src + 18 + 4 + off;
                body = f[2..18];
            }

            // The clone lives at the far end of the ROM, beyond a Thumb BL's +-4 MB reach, so instead of a BL it TAIL-CALLS: it keeps the
            // caller's LR, loads the target from a literal and BX's to it (no push/pop needed). Layout (24 bytes, 4-aligned):
            //   [0..15] = original bytes 2..17 (ldrh/sub/strh/mov/ldr/ldr/adds/movs r1,#ANIM); [16] ldr r3,[pc,#0]; [18] bx r3; [20] .word target|1
            var g = new byte[24];
            Array.Copy(body, 0, g, 0, 16);
            g[14] = (byte)animation;
            g[16] = 0x00; g[17] = 0x4B; g[18] = 0x18; g[19] = 0x47;
            BitConverter.GetBytes(0x08000000u | (uint)target | 1).CopyTo(g, 20);
            int dst = rom.AllocateFreeSpace(24);
            rom.WriteBytesAt(dst, g);
            return 0x08000000u | (uint)dst | 1;
        }
    }

    /// <summary>
    /// PNG -> sprite-frame ".bin". A frame blob is {u32 mode = 1 (JCALG1), u32 byteCount, literal-only JCALG1 stream} (mode 0 "stored" was dropped, see CompressLiteral). Old note: mode 0 made the engine's frame
    /// uploader (sub_8007772, 0x8007772) point at the data directly instead of decompressing JCALG1 (CERTAINTY: MEDIUM -- read from the
    /// decompile, not run in an emulator). Tile data is 8x8 tiles in row-major tile order, 64 bytes per tile, palette indices into the
    /// shared OBJ palette (0x1DA6C8); index 0 is transparent. Sizes must be a GBA OAM shape: 8x8 16x16 32x32 64x64, 16x8 32x8 32x16 64x32, 8x16 8x32 16x32 32x64.
    /// </summary>
    public static class FrameImport
    {
        private static readonly (int W, int H, int Shape, int Size)[] Shapes =
        [
            (8, 8, 0, 0), (16, 16, 0, 1), (32, 32, 0, 2), (64, 64, 0, 3),
            (16, 8, 1, 0), (32, 8, 1, 1), (32, 16, 1, 2), (64, 32, 1, 3),
            (8, 16, 2, 0), (8, 32, 2, 1), (16, 32, 2, 2), (32, 64, 2, 3),
        ];

        public const int CharacterSpriteIndex = 0x3B4E74, ObjPalette = 0x1DA6C8, RecordCopySize = 0x1A0;

        // g_CharacterSpriteIndex (file 0x3B4E74) is reached through ONE code literal: Character_GetSpriteId (0x08009324) loads 0x083B4E90
        // (= table + 7 words, because ids 1-6 are the party) from file offset 0x959C and indexes it with no bounds check. Moving the table only
        // needs that literal patched, so it can be relocated to make room for new sprite ids (HIGH: it is the only word in the ROM near the table).
        public const int IndexLiteral = 0x959C, IndexCapacity = 256;

        /// <summary>File offset of sprite id 0's entry in the (possibly relocated) sprite index table.</summary>
        public static int IndexAddress(ROM rom)
        {
            uint lit = BitConverter.ToUInt32(rom.ReadBytesAt(IndexLiteral, 4));
            return IsRomPtr(lit, rom) ? (int)(lit & 0x00FFFFFF) - 7 * 4 : CharacterSpriteIndex;
        }

        public static bool IndexRelocated(ROM rom) => IndexAddress(rom) != CharacterSpriteIndex;

        private static bool IsDescriptorPointer(ROM rom, uint v)
        {
            if (!IsRomPtr(v, rom) || (v & 3) != 0) return false;
            var d = rom.ReadBytesAt((int)(v & 0x00FFFFFF), 12);
            return Shapes.Any(t => t.W == d[2] && t.H == d[3]) && IsRomPtr(BitConverter.ToUInt32(d, 8), rom);
        }

        /// <summary>
        /// How many bytes a sprite record really uses: the 0x4C-byte header plus as many 16-byte groups (Down/Up/Left/Right) as hold frame pointers.
        /// A record is followed directly by its frame descriptors (or the next resource), so the first non-null word that is not a frame pointer ends it.
        /// Records are NOT a fixed size: 7 groups is common, some run past 0x1A0 (sprite 61 has 22 groups). Fixed-size copies used to truncate those.
        /// </summary>
        public static int RecordExtent(ROM rom, int recordAddress)
        {
            int last = 0;
            for (int k = FirstGroup; k < FirstGroup + 64 * GroupSize && recordAddress + k + 4 <= rom.Length; k += 4)
            {
                uint w = BitConverter.ToUInt32(rom.ReadBytesAt(recordAddress + k, 4));
                if (w == 0) continue;
                if (!IsDescriptorPointer(rom, w)) break;
                last = k;
            }
            int groups = last == 0 ? 0 : (last + 4 - FirstGroup + GroupSize - 1) / GroupSize;
            return FirstGroup + groups * GroupSize;
        }

        /// <summary>Bytes a record needs when copied or edited: at least <see cref="RecordCopySize"/> (so ability blocks like +364 exist) or its real extent.</summary>
        public static int RecordLimit(ROM rom, int recordAddress) => Math.Max(RecordCopySize, RecordExtent(rom, recordAddress));

        /// <summary>The groups of a sprite that can hold frames (the ones "Dump all sprites" walks and the Sprite editor offers). 0 when it has no record.</summary>
        public static int GroupsIn(ROM rom, int spriteId)
        {
            uint ptr = BitConverter.ToUInt32(rom.ReadBytesAt(IndexAddress(rom) + spriteId * 4, 4));
            return IsRomPtr(ptr, rom) ? (RecordLimit(rom, (int)(ptr & 0x00FFFFFF)) - FirstGroup) / GroupSize : 0;
        }

        /// <summary>
        /// Makes a NEW sprite id whose record is a copy of <paramref name="sourceId"/>'s (its frames are shared until edited; the Sprite editor copies a
        /// record before writing to it, so editing the clone never touches the original). The index table is relocated on first use to hold
        /// <see cref="IndexCapacity"/> ids. Returns the new id, which a display row can use as its sprite id.
        /// </summary>
        public static int CloneSprite(ROM rom, int sourceId)
        {
            uint src = BitConverter.ToUInt32(rom.ReadBytesAt(IndexAddress(rom) + sourceId * 4, 4));
            if (!IsRomPtr(src, rom)) throw new InvalidOperationException($"Sprite {sourceId} has no record to copy.");
            if (!IndexRelocated(rom))
            {
                var table = new byte[IndexCapacity * 4];
                rom.ReadBytesAt(CharacterSpriteIndex, 157 * 4).CopyTo(table, 0);      // ids 0..156 are the real entries; what follows is other data
                int at = rom.AllocateFreeSpace(table.Length);
                rom.WriteBytesAt(at, table);
                rom.PatchInt32(IndexLiteral, (int)(0x08000000u | (uint)(at + 7 * 4)));
            }
            int index = IndexAddress(rom);
            int newId = -1;
            for (int id = 157; id < IndexCapacity; id++)
                if (BitConverter.ToUInt32(rom.ReadBytesAt(index + id * 4, 4)) == 0) { newId = id; break; }
            if (newId < 0) throw new InvalidOperationException("The sprite index table is full.");
            int size = RecordLimit(rom, (int)(src & 0x00FFFFFF));
            var record = rom.ReadBytesAt((int)(src & 0x00FFFFFF), size);
            int copy = rom.AllocateFreeSpace(size);
            rom.WriteBytesAt(copy, record);
            rom.WriteBytesAt(index + newId * 4, BitConverter.GetBytes(0x08000000u | (uint)copy));
            return newId;
        }

        public sealed record Converted(int Width, int Height, byte[] Indices, string Report);

        /// <summary>Reads a PNG (indexed: indices used as-is; otherwise: exact/nearest match to the OBJ palette, alpha &lt; 128 = 0).</summary>
        public static bool IsValidSize(int w, int h) => Shapes.Any(s => s.W == w && s.H == h);

        /// <param name="requireShape">false when the caller will pad the image to a stock size afterwards (it must then fit within 64x64).</param>
        public static Converted FromPng(string path, ROM rom, bool requireShape = true)
        {
            using var bmp = new Bitmap(path);
            int w = bmp.Width, h = bmp.Height;
            if (w > 64 || h > 64) throw new InvalidOperationException($"{Path.GetFileName(path)} is {w}x{h}; frames are at most 64x64.");
            if (requireShape && !IsValidSize(w, h))
                throw new InvalidOperationException($"{Path.GetFileName(path)} is {w}x{h}. Frame sizes must be one of: " + string.Join(", ", Shapes.Select(s => $"{s.W}x{s.H}")) + ".");

            var idx = new byte[w * h];
            string report;
            if (bmp.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
                try
                {
                    for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, idx, y * w, w);
                }
                finally { bmp.UnlockBits(data); }
                report = $"indexed PNG: {idx.Distinct().Count()} palette indices used, copied as-is.";
            }
            else
            {
                var pal = Rendering.ItemIconReader.ReadOBJPalette(rom, new Config.Game { OBJPaletteOffset = ObjPalette, CharacterSpriteIndexOffset = CharacterSpriteIndex });
                int exact = 0, near = 0;
                int key0 = BitConverter.ToUInt16(rom.ReadBytesAt(ObjPalette, 2)); // raw palette entry 0 = 0x7C1F magenta: the game's transparent key (ReadOBJPalette replaces it with clear)
                var cache = new Dictionary<int, byte>();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var c = bmp.GetPixel(x, y);
                        if (c.A < 128) { idx[y * w + x] = 0; continue; }
                        // The game's transparent colour is palette entry 0 (the pink); a pixel of exactly that colour IS transparent.
                        if ((c.R >> 3) == (key0 & 31) && (c.G >> 3) == (key0 >> 5 & 31) && (c.B >> 3) == (key0 >> 10 & 31)) { idx[y * w + x] = 0; continue; }
                        int key = (c.R >> 3) | (c.G >> 3) << 5 | (c.B >> 3) << 10;
                        if (!cache.TryGetValue(key, out byte best))
                        {
                            int bestD = int.MaxValue;
                            for (int i = 1; i < 256 && i < pal.Length; i++)
                            {
                                int dr = (pal[i].R >> 3) - (c.R >> 3), dg = (pal[i].G >> 3) - (c.G >> 3), db = (pal[i].B >> 3) - (c.B >> 3);
                                int d = dr * dr + dg * dg + db * db;
                                if (d < bestD) { bestD = d; best = (byte)i; }
                            }
                            cache[key] = best;
                            if (bestD == 0) exact++; else near++;
                        }
                        idx[y * w + x] = best;
                    }
                report = $"true-colour PNG matched to the OBJ palette ({cache.Count} distinct colours, {near} not an exact palette colour).";
            }
            return new Converted(w, h, idx, report);
        }

        /// <summary>
        /// JCALG1 stream that contains ONLY literals: a 9-bit literal-size header (REQUIRED, see below), then per byte a `1` bit + 8 bits, then the end marker (13 zero bits), padded to whole
        /// 32-bit words, bits MSB-first (matches the decoder in JCALG1.cs; round-trip tested, and verified against the game's own ARM decoder in an emulator -- 2026-09-20). ~12% bigger than the input but decodes through
        /// the exact mode-1 path every stock frame uses, so nothing depends on the engine accepting "stored" frames.
        /// </summary>
        public static byte[] CompressLiteral(byte[] data)
        {
            var bits = new List<bool>(data.Length * 9 + 60);
            // The engine's decoder (IWRAM 0x3000000) never initialises its literal width or offset: every stock stream begins with a
            // "literal size change" op, and without one it reads garbage widths, decodes junk and overruns the stack (jump to invalid
            // address). Emit that op first: 0 0 1 (one-byte-phrase group), 0000 (=-1: special), 0 (not a raw run), 1 (8-bit literals, no offset).
            foreach (int b in new[] { 0, 0, 1, 0, 0, 0, 0, 0, 1 }) bits.Add(b == 1);
            foreach (byte b in data) { bits.Add(true); for (int i = 7; i >= 0; i--) bits.Add((b >> i & 1) != 0); }
            for (int i = 0; i < 13; i++) bits.Add(false);
            while (bits.Count % 32 != 0) bits.Add(false);
            var outp = new byte[bits.Count / 8];
            for (int i = 0; i < bits.Count; i++)
            {
                int word = i / 32, bit = 31 - i % 32; // MSB-first within a little-endian 32-bit word
                if (bits[i]) outp[word * 4 + bit / 8] |= (byte)(1 << (bit % 8));
            }
            return outp;
        }

        /// <summary>
        /// Puts a smaller image on a transparent (index 0) canvas of the stock frame's size: centred horizontally, bottom-aligned (stock frames
        /// keep the feet ~8 px below the origin, and 16x32 / 32x32 frames share the same centre). Throws if the image doesn't fit.
        /// </summary>
        public static Converted PadTo(Converted c, int width, int height)
        {
            if (c.Width == width && c.Height == height) return c;
            if (c.Width > width || c.Height > height) throw new InvalidOperationException($"A {c.Width}x{c.Height} image doesn't fit in the stock {width}x{height} frame -- shrink the PNG.");
            var idx = new byte[width * height];
            int ox = (width - c.Width) / 2, oy = height - c.Height;
            for (int y = 0; y < c.Height; y++) Array.Copy(c.Indices, y * c.Width, idx, (oy + y) * width + ox, c.Width);
            return new Converted(width, height, idx, c.Report + $" Padded {c.Width}x{c.Height} -> {width}x{height} (transparent, centred, feet at the bottom).");
        }

        /// <summary>Row-major 8x8 tiles, as {u32 1 (JCALG1), u32 decompressed size, literal-only stream}.</summary>
        public static byte[] ToBlob(Converted c)
        {
            int tw = c.Width / 8, th = c.Height / 8;
            var tiles = new byte[tw * th * 64];
            for (int ty = 0; ty < th; ty++)
                for (int tx = 0; tx < tw; tx++)
                    for (int py = 0; py < 8; py++)
                        for (int px = 0; px < 8; px++)
                            tiles[(ty * tw + tx) * 64 + py * 8 + px] = c.Indices[(ty * 8 + py) * c.Width + tx * 8 + px];
            var stream = CompressLiteral(tiles);
            var blob = new byte[8 + stream.Length];
            BitConverter.GetBytes(1u).CopyTo(blob, 0);
            BitConverter.GetBytes((uint)tiles.Length).CopyTo(blob, 4);
            stream.CopyTo(blob, 8);
            return blob;
        }

        /// <summary>12-byte frame descriptor: x/y offset, w, h, OAM attr0|attr1&lt;&lt;16 (256 colours), pointer to the blob.</summary>
        public static byte[] ToDescriptor(Converted c, sbyte xOffset, sbyte yOffset, uint blobAddress)
        {
            if (!IsValidSize(c.Width, c.Height)) throw new InvalidOperationException($"{c.Width}x{c.Height} is not a GBA sprite size. Use a size from: " + string.Join(", ", Shapes.Select(t => $"{t.W}x{t.H}")) + ", or tick Pad so it is fitted to the stock frame.");
            var s = Shapes.First(t => t.W == c.Width && t.H == c.Height);
            uint attr0 = 0x2000u | (uint)(s.Shape << 14), attr1 = (uint)(s.Size << 14);
            var d = new byte[12];
            d[0] = (byte)xOffset; d[1] = (byte)yOffset; d[2] = (byte)c.Width; d[3] = (byte)c.Height;
            BitConverter.GetBytes(attr0 | attr1 << 16).CopyTo(d, 4);
            BitConverter.GetBytes(blobAddress).CopyTo(d, 8);
            return d;
        }

        /// <summary>True when the sprite id has a record with at least one valid frame pointer in its frame run.</summary>
        public static bool HasRecord(ROM rom, int spriteId)
        {
            uint ptr = BitConverter.ToUInt32(rom.ReadBytesAt(IndexAddress(rom) + spriteId * 4, 4));
            if (!IsRomPtr(ptr, rom)) return false;
            int rec = (int)(ptr & 0x00FFFFFF);
            for (int k = FirstGroup; k < FirstGroup + 12 * 4; k += 4)
            {
                uint fp = BitConverter.ToUInt32(rom.ReadBytesAt(rec + k, 4));
                if (!IsRomPtr(fp, rom)) continue;
                var d = rom.ReadBytesAt((int)(fp & 0x00FFFFFF), 12);
                if (Shapes.Any(t => t.W == d[2] && t.H == d[3])) return true;
            }
            return false;
        }

        public static bool IsRomPtr(uint v, ROM r) => v is >= 0x08000000 and < 0x0A000000 && (int)(v & 0x00FFFFFF) + 12 <= r.Length;

        /// <summary>
        /// Copies a sprite's record to free space (0x1A0 bytes), zeroes any frame-pointer word that isn't null or a sane descriptor (past the
        /// real end of a short record those words are the NEXT resource's data), and repoints the sprite index. Returns the record's file offset.
        /// </summary>
        public static int RelocateRecord(ROM rom, int spriteId)
        {
            uint ptr = BitConverter.ToUInt32(rom.ReadBytesAt(IndexAddress(rom) + spriteId * 4, 4));
            if (!IsRomPtr(ptr, rom)) throw new InvalidOperationException($"Sprite {spriteId} has no record.");
            int size = RecordLimit(rom, (int)(ptr & 0x00FFFFFF));
            var rec = rom.ReadBytesAt((int)(ptr & 0x00FFFFFF), size);
            for (int k = 0x4C; k + 4 <= size; k += 4)
            {
                uint w = BitConverter.ToUInt32(rec, k);
                bool keep = w == 0;
                if (!keep && IsRomPtr(w, rom))
                {
                    var d = rom.ReadBytesAt((int)(w & 0x00FFFFFF), 12);
                    keep = d[2] != 0 && d[3] != 0 && d[2] % 8 == 0 && d[3] % 8 == 0;
                }
                if (!keep) BitConverter.GetBytes(0u).CopyTo(rec, k);
            }
            int addr = rom.AllocateFreeSpace(size);
            rom.WriteBytesAt(addr, rec);
            rom.WriteBytesAt(IndexAddress(rom) + spriteId * 4, BitConverter.GetBytes(0x08000000u | (uint)addr));
            return addr;
        }

        /// <summary>
        /// Writes the blob + descriptor to free space and points record+<paramref name="slotOffset"/> at the descriptor. If
        /// <paramref name="mirrorSlotOffset"/> is given, that slot gets an X-FLIPPED descriptor sharing the same tile data -- exactly how stock
        /// frames make "right" from "left" (attr1 bit 12; x offset mirrored to 16 - width - x).
        /// </summary>
        public static void WriteFrame(ROM rom, int spriteId, int slotOffset, Converted c, sbyte xOffset, sbyte yOffset, int mirrorSlotOffset = -1, int frame = -1)
        {
            uint checkPtr = BitConverter.ToUInt32(rom.ReadBytesAt(IndexAddress(rom) + spriteId * 4, 4));
            int limit = IsRomPtr(checkPtr, rom) ? RecordLimit(rom, (int)(checkPtr & 0x00FFFFFF)) : RecordCopySize;
            foreach (int so in new[] { slotOffset, mirrorSlotOffset })
                if (so != -1 && (so < 0x4C || so + 4 > limit || so % 4 != 0)) throw new ArgumentOutOfRangeException(nameof(slotOffset));
            uint ptr = BitConverter.ToUInt32(rom.ReadBytesAt(IndexAddress(rom) + spriteId * 4, 4));
            if (!IsRomPtr(ptr, rom)) throw new InvalidOperationException($"Sprite {spriteId} has no record.");
            int rec = (int)(ptr & 0x00FFFFFF);

            var blob = ToBlob(c);
            int blobAddr = rom.AllocateFreeSpace(blob.Length);
            rom.WriteBytesAt(blobAddr, blob);
            var desc = ToDescriptor(c, xOffset, yOffset, 0x08000000u | (uint)blobAddr);
            WriteFrameArray(rom, rec + slotOffset, desc, rec, frame);

            if (mirrorSlotOffset != -1)
            {
                var d = ToDescriptor(c, (sbyte)(16 - c.Width - xOffset), yOffset, 0x08000000u | (uint)blobAddr);
                d[7] |= 0x10; // attr1 bit 12 = horizontal flip
                WriteFrameArray(rom, rec + mirrorSlotOffset, d, rec, frame);
            }
        }

        /// <summary>
        /// A record slot points at an ARRAY of 12-byte descriptors, not a single one: the game builds the frame as
        /// `*(record + 0x4C + group + 4*direction) + 12 * frameIndex` (sub_800775A), and the animation code steps frameIndex (walk cycles 0-1,
        /// ki-blast pose 0-2, ...). The stock arrays are just packed back to back, so a one-descriptor replacement makes frameIndex 1+ read
        /// whatever follows -- our next blob's header -- as a descriptor, and the frame uploader then decompresses from a junk address
        /// (crash / never returns to idle). So the replacement array has as many descriptors as the stock one. If every stock frame is the same
        /// (a static pose, like ki blast) all of them become the new image; otherwise only frame 0 is replaced and the other frames stay stock.
        /// </summary>
        private static void WriteFrameArray(ROM rom, int slotAddress, byte[] newDescriptor, int recordAddress, int frame = -1)
        {
            if (frame >= 0)
            {
                // Replace ONLY animation frame `frame` of this slot; every other frame keeps its own picture.
                var existing = ReadStockArray(rom, recordAddress, BitConverter.ToUInt32(rom.ReadBytesAt(slotAddress, 4)));
                if (frame >= existing.Count) throw new InvalidOperationException($"This slot has {existing.Count} frame(s); frame {frame} does not exist.");
                var whole = new byte[12 * existing.Count];
                for (int i = 0; i < existing.Count; i++) (i == frame ? newDescriptor : existing[i]).CopyTo(whole, 12 * i);
                int where = rom.AllocateFreeSpace(whole.Length);
                rom.WriteBytesAt(where, whole);
                rom.WriteBytesAt(slotAddress, BitConverter.GetBytes(0x08000000u | (uint)where));
                return;
            }
            uint old = BitConverter.ToUInt32(rom.ReadBytesAt(slotAddress, 4));
            var stock = ReadStockArray(rom, recordAddress, old);
            int n = stock.Count == 0 ? 4 : stock.Count; // an empty slot: 4 frames covers every index the animations use (0-3)
            bool allSame = stock.Count == 0 || stock.All(d => d.AsSpan().SequenceEqual(stock[0]));
            var array = new byte[12 * n];
            for (int i = 0; i < n; i++) (i == 0 || allSame ? newDescriptor : stock[i]).CopyTo(array, 12 * i);
            int addr = rom.AllocateFreeSpace(array.Length);
            rom.WriteBytesAt(addr, array);
            rom.WriteBytesAt(slotAddress, BitConverter.GetBytes(0x08000000u | (uint)addr));
        }

        /// <summary>
        /// The descriptors of the array a record slot points at (its animation frames). Stock arrays are packed back to back, so a valid-looking
        /// run can spill into the NEXT slot's array: it stops at the nearest higher slot pointer in the same record.
        /// </summary>
        public static List<byte[]> ReadStockArray(ROM rom, int recordAddress, uint slotPointer)
        {
            var stock = new List<byte[]>();
            if (!IsRomPtr(slotPointer, rom) || (slotPointer & 3) != 0) return stock;
            int at = (int)(slotPointer & 0x00FFFFFF), limit = MaxFrames;
            for (int k = FirstGroup; k + 4 <= RecordLimit(rom, recordAddress); k += 4)
            {
                uint other = BitConverter.ToUInt32(rom.ReadBytesAt(recordAddress + k, 4));
                if (IsRomPtr(other, rom) && (int)(other & 0x00FFFFFF) > at) limit = Math.Min(limit, ((int)(other & 0x00FFFFFF) - at) / 12);
            }
            for (int i = 0; i < limit && at + 12 * (i + 1) <= rom.Length; i++)
            {
                var d = rom.ReadBytesAt(at + 12 * i, 12);
                if (!Shapes.Any(t => t.W == d[2] && t.H == d[3]) || !IsRomPtr(BitConverter.ToUInt32(d, 8), rom)) break;
                stock.Add(d);
            }
            return stock;
        }

        /// <summary>
        /// Points record slot <paramref name="slotOffset"/> at a NEW array with exactly these frames (each with its own blob and offsets), one
        /// descriptor per animation frame. Used by the sprite-folder import; nothing of the stock frames is kept.
        /// </summary>
        public static void WriteFrameSequence(ROM rom, int spriteId, int slotOffset, IReadOnlyList<(Converted C, sbyte X, sbyte Y)> frames)
        {
            if (frames.Count == 0) throw new InvalidOperationException("A slot needs at least one frame.");
            uint ptr = BitConverter.ToUInt32(rom.ReadBytesAt(IndexAddress(rom) + spriteId * 4, 4));
            if (!IsRomPtr(ptr, rom)) throw new InvalidOperationException($"Sprite {spriteId} has no record.");
            int rec = (int)(ptr & 0x00FFFFFF);
            if (slotOffset < 0x4C || slotOffset + 4 > RecordLimit(rom, rec) || slotOffset % 4 != 0) throw new ArgumentOutOfRangeException(nameof(slotOffset));
            var array = new byte[12 * frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                var (c, x, y) = frames[i];
                var blob = ToBlob(c);
                int blobAddr = rom.AllocateFreeSpace(blob.Length);
                rom.WriteBytesAt(blobAddr, blob);
                ToDescriptor(c, x, y, 0x08000000u | (uint)blobAddr).CopyTo(array, 12 * i);
            }
            int addr = rom.AllocateFreeSpace(array.Length);
            rom.WriteBytesAt(addr, array);
            rom.WriteBytesAt(rec + slotOffset, BitConverter.GetBytes(0x08000000u | (uint)addr));
        }

        private const int MaxFrames = 8;

        /// <summary>Slot layout of a 4-frame group: 0 = down, 1 = up, 2 = left, 3 = right (stock: an X-flipped copy of 2). Confirmed on sprites 57 and 60.</summary>
        public static readonly string[] SlotNames = ["Down", "Up", "Left", "Right"];
        public const int FirstGroup = 0x4C, GroupSize = 16, GroupCount = 21; // the base record (0x1A0 bytes) has groups at 0x4C + 16n, n = 0..20; a sprite can have more, see GroupsIn (0x9C = 5, 0x16C = 18, 0x17C = 19); Goku's record has 3 more frames after that

        /// <summary>The frame at a slot, drawn with the OBJ palette (index 0 transparent) and X-flip applied; null if the slot is empty/invalid.</summary>
        public static int SlotFrameCount(ROM rom, int spriteId, int slotOffset)
        {
            uint ptr = BitConverter.ToUInt32(rom.ReadBytesAt(IndexAddress(rom) + spriteId * 4, 4));
            if (!IsRomPtr(ptr, rom)) return 0;
            int rec = (int)(ptr & 0x00FFFFFF);
            return ReadStockArray(rom, rec, BitConverter.ToUInt32(rom.ReadBytesAt(rec + slotOffset, 4))).Count;
        }

        public static Bitmap? RenderSlot(ROM rom, int spriteId, int slotOffset, int frame = 0)
        {
            try
            {
                uint ptr = BitConverter.ToUInt32(rom.ReadBytesAt(IndexAddress(rom) + spriteId * 4, 4));
                if (!IsRomPtr(ptr, rom)) return null;
                int recAddr = (int)(ptr & 0x00FFFFFF);
                var stock = ReadStockArray(rom, recAddr, BitConverter.ToUInt32(rom.ReadBytesAt(recAddr + slotOffset, 4)));
                if (frame < 0 || frame >= stock.Count) return null;
                var d = stock[frame];
                int w = d[2], h = d[3];
                if (w == 0 || h == 0 || w % 8 != 0 || h % 8 != 0) return null;
                var tiles = Rendering.JCALG1.Decompress(rom, (int)(BitConverter.ToUInt32(d, 8) & 0x00FFFFFF));
                if (tiles.Length < w * h) return null;
                var pal = Rendering.ItemIconReader.ReadOBJPalette(rom, new Config.Game { OBJPaletteOffset = ObjPalette, CharacterSpriteIndexOffset = CharacterSpriteIndex });
                var bmp = new Bitmap(w, h);
                int tw = w / 8;
                for (int t = 0; t < tw * (h / 8); t++)
                    for (int py = 0; py < 8; py++)
                        for (int px = 0; px < 8; px++)
                        {
                            byte i = tiles[t * 64 + py * 8 + px];
                            if (i != 0) bmp.SetPixel(t % tw * 8 + px, t / tw * 8 + py, pal[i]);
                        }
                if ((d[7] & 0x10) != 0) bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
                return bmp;
            }
            catch { return null; }
        }

        /// <summary>Health report for one sprite record: where it lives and every 4-frame group's state (bad pointers, sizes, undecodable tiles).</summary>
        public static string Inspect(ROM rom, int spriteId)
        {
            var sb = new System.Text.StringBuilder();
            uint ptr = BitConverter.ToUInt32(rom.ReadBytesAt(IndexAddress(rom) + spriteId * 4, 4));
            if (!IsRomPtr(ptr, rom)) return $"Sprite {spriteId} has no record.";
            int rec = (int)(ptr & 0x00FFFFFF);
            sb.AppendLine($"Sprite {spriteId}: record at file 0x{rec:X}.");
            for (int n = 0; n < GroupsIn(rom, spriteId); n++)
            {
                int off = FirstGroup + n * GroupSize;
                var parts = new List<string>();
                int valid = 0;
                for (int i = 0; i < 4; i++)
                {
                    uint fp = BitConverter.ToUInt32(rom.ReadBytesAt(rec + off + i * 4, 4));
                    if (fp == 0) { parts.Add("empty"); continue; }
                    if (!IsRomPtr(fp, rom)) { parts.Add($"BAD pointer {fp:X8}"); continue; }
                    if ((fp & 3) != 0) { parts.Add($"MISALIGNED descriptor {fp:X8}"); continue; }
                    var d = rom.ReadBytesAt((int)(fp & 0x00FFFFFF), 12);
                    uint bp = BitConverter.ToUInt32(d, 8);
                    string? problem = null;
                    if (!Shapes.Any(t => t.W == d[2] && t.H == d[3])) problem = $"bad size {d[2]}x{d[3]}";
                    else if (!IsRomPtr(bp, rom) || (bp & 3) != 0) problem = $"bad/misaligned tile pointer {bp:X8}";
                    else
                    {
                        uint mode = BitConverter.ToUInt32(rom.ReadBytesAt((int)(bp & 0x00FFFFFF), 4));
                        try
                        {
                            var t = Rendering.JCALG1.Decompress(rom, (int)(bp & 0x00FFFFFF));
                            if (t.Length != d[2] * d[3]) problem = $"tile data is {t.Length} bytes, expected {d[2] * d[3]}";
                        }
                        catch (Exception ex) { problem = "tile data doesn't decode (" + ex.GetType().Name + ")"; }
                        if (problem == null && mode > 1) problem = $"unknown blob mode {mode}";
                    }
                    if (problem != null) parts.Add($"{d[2]}x{d[3]} PROBLEM: {problem}");
                    else { valid++; parts.Add($"{d[2]}x{d[3]}{((d[7] & 0x10) != 0 ? " flipped" : "")}"); }
                }
                if (parts.All(x => x == "empty")) continue;
                sb.AppendLine($"  group +0x{off:X} (down/up/left/right): {(valid == 4 ? "OK" : "INCOMPLETE")} -- {string.Join(" | ", parts)}");
            }
            return sb.ToString();
        }
    }
}
