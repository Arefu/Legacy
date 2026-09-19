using DrGero.IO;
using DrGero.Rendering;
using System.Drawing;
using System.Drawing.Imaging;

namespace DrGero.Boot
{
    /// <summary>
    /// Reads (and, via <see cref="DeveloperIntroWriter"/>, patches) the game's real
    /// pre-title "intro video" and text-only credits roll.
    ///
    /// CONFIRMED via IDA 2026-09 (Game_Main @0x08000158): boot order is (1)
    /// DeveloperIntro_Splash_LoadAndDraw @0x0801EDFA -- one full-screen bitmap, (2)
    /// music track 14 starts, (3) DeveloperIntro_Slideshow_Init/_Update
    /// @0x0801D3A4/0x0801D496 -- a timed cross-fade slideshow of full-screen bitmaps,
    /// THEN (4) the credits roll + title screen loop. This is the real "video": a
    /// frame-counted sequence (0-160 hold slide1, 161-176 fade, 177-319 hold slide2,
    /// 320 load slide3, 321-336 fade, 337-500 hold slide3, 501-516 fade to black) of
    /// EXACTLY 4 full-screen bitmaps (1 pre-splash + 3 slideshow frames), each
    /// independently JCALG1-compressed (format=1, decompressed size 0x9600 = 240*160
    /// exactly, flat 8bpp indexed, NOT tiled, NOT deltas against each other). All 4
    /// share the same BG palette @0x081D4F50 (DMA'd to BG palette RAM, 0x05000000) --
    /// NOT the OBJ palette other sprite readers in this DLL (ItemIconReader etc.) use.
    ///
    /// The title screen's own spinning-ball idle animation is a SEPARATE thing (plays
    /// later, while waiting for input at the title screen) and is out of scope here.
    /// </summary>
    public readonly struct DeveloperIntroSlide(string name, int dataAddress, int literalPoolAddress, int holdVblanks, int fadeInVblanks)
    {
        /// <summary>Human-readable label, playback order.</summary>
        public string Name { get; } = name;

        /// <summary>Real GBA address (0x08XXXXXX) of this slide's [format][size][data] resource, as Resource_LoadOrDecompress reads it.</summary>
        public int DataAddress { get; } = dataAddress;

        /// <summary>
        /// Real GBA address (0x08XXXXXX) of the Thumb literal-pool slot holding a
        /// pointer to <see cref="DataAddress"/> (e.g. 0x0801D558 for slide 1, referenced
        /// by `LDR R0, =...` in DeveloperIntro_Slideshow_Init). Patch THIS address (not
        /// DataAddress) to redirect the game at a replacement resource -- see
        /// <see cref="DeveloperIntroWriter"/>.
        /// </summary>
        public int LiteralPoolAddress { get; } = literalPoolAddress;

        /// <summary>Vblanks (~59.7Hz) this slide cross-fades in for before holding -- CONFIRMED from DeveloperIntro_Slideshow_Update's frame-counter branches (splash's own timing isn't traced, reuses the slideshow's as a placeholder).</summary>
        public int FadeInVblanks { get; } = fadeInVblanks;

        /// <summary>Vblanks this slide holds static after fading in.</summary>
        public int HoldVblanks { get; } = holdVblanks;

        /// <summary>
        /// Returns a copy with different timing. NOTE: these vblank counts are hardcoded
        /// immediate values inside DeveloperIntro_Slideshow_Update's Thumb code, not a ROM
        /// data table -- this does NOT patch the ROM, it's for a consumer's own (e.g.
        /// preview-tool) playback state only.
        /// </summary>
        public DeveloperIntroSlide WithTiming(int holdVblanks, int fadeInVblanks) => new(Name, DataAddress, LiteralPoolAddress, holdVblanks, fadeInVblanks);
    }

    public static class DeveloperIntro
    {
        /// <summary>Every slide is a flat 240x160 8bpp indexed bitmap -- CONFIRMED, every slide's own JCALG1 header decompresses to exactly this many bytes.</summary>
        public const int FrameWidth = 240;
        public const int FrameHeight = 160;

        /// <summary>Shared BG palette (see class doc) for every slide.</summary>
        public const int PaletteAddress = 0x081D4F50;

        /// <summary>Ordered exactly as Game_Main plays them.</summary>
        public static readonly DeveloperIntroSlide[] Slides =
        [
            new("Pre-boot splash",   dataAddress: 0x087007BC, literalPoolAddress: 0x0801EED8, holdVblanks: 160, fadeInVblanks: 16),
            new("Slideshow frame 1", dataAddress: 0x086FFC34, literalPoolAddress: 0x0801D558, holdVblanks: 160, fadeInVblanks: 16),
            new("Slideshow frame 2", dataAddress: 0x086FFF70, literalPoolAddress: 0x0801D560, holdVblanks: 143, fadeInVblanks: 16),
            new("Slideshow frame 3", dataAddress: 0x0870138C, literalPoolAddress: 0x0801D57C, holdVblanks: 164, fadeInVblanks: 16),
        ];

        public static Color[] GetPalette(ROM rom)
        {
            rom.PushPosition(PaletteAddress & 0x00FFFFFF);
            var colors = new Color[256];
            for (int i = 0; i < colors.Length; i++)
            {
                int c = rom.ReadShort();
                int r = ((c & 0x1F) * 0x21) >> 2;
                int g = (((c >> 5) & 0x1F) * 0x21) >> 2;
                int b = (((c >> 10) & 0x1F) * 0x21) >> 2;
                colors[i] = Color.FromArgb(0xFF, r, g, b);
            }
            colors[0] = Color.FromArgb(0, 0, 0, 0); // palette index 0 = transparent, GBA convention
            rom.PopPosition();
            return colors;
        }

        /// <summary>
        /// The slide's raw, uncompressed 240x160 8bpp indexed pixel bytes -- the exact
        /// bytes Resource_LoadOrDecompress_impl hands the game post-JCALG1, and the exact
        /// format a replacement passed to <see cref="DeveloperIntroWriter"/> must match.
        /// </summary>
        public static byte[] GetRawIndexedBytes(ROM rom, DeveloperIntroSlide slide) =>
            JCALG1.Decompress(rom, slide.DataAddress & 0x00FFFFFF);

        public static Bitmap GetFrame(ROM rom, DeveloperIntroSlide slide, Color[]? palette = null) =>
            RenderIndexed(GetRawIndexedBytes(rom, slide), FrameWidth, FrameHeight, palette);

        public static List<Bitmap> GetAllFrames(ROM rom, Color[]? palette = null)
        {
            var frames = new List<Bitmap>();
            foreach (var slide in Slides)
            {
                byte[] data = GetRawIndexedBytes(rom, slide);
                if (data.Length < FrameWidth * FrameHeight) continue; // corrupt/unexpected -- skip rather than render garbage
                frames.Add(RenderIndexed(data, FrameWidth, FrameHeight, palette));
            }
            return frames;
        }

        // Total length of one full loop, in vblank ticks -- e.g. the range for a seek bar.
        public static int GetTotalVblanks(DeveloperIntroSlide[] slides)
        {
            int total = 0;
            foreach (var s in slides) total += s.FadeInVblanks + s.HoldVblanks;
            return total;
        }

        /// <summary>Maps an absolute vblank position (wrapped to one loop) to a (slide index, tick within that slide) pair -- for seeking.</summary>
        public static (int SlideIndex, int LocalTick) ResolveGlobalTick(DeveloperIntroSlide[] slides, int globalTick)
        {
            int total = GetTotalVblanks(slides);
            int remaining = ((globalTick % total) + total) % total;
            for (int i = 0; i < slides.Length; i++)
            {
                int len = slides[i].FadeInVblanks + slides[i].HoldVblanks;
                if (remaining < len) return (i, remaining);
                remaining -= len;
            }
            return (slides.Length - 1, 0);
        }

        /// <summary>Inverse of ResolveGlobalTick.</summary>
        public static int ToGlobalTick(DeveloperIntroSlide[] slides, int slideIndex, int localTick)
        {
            int global = 0;
            for (int i = 0; i < slideIndex; i++)
                global += slides[i].FadeInVblanks + slides[i].HoldVblanks;
            return global + localTick;
        }

        public static Bitmap RenderIndexed(byte[] data, int width, int height, Color[]? palette)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, width, height);
            var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
            int stride = bmpData.Stride;
            byte[] buffer = new byte[stride * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = data[(y * width) + x];
                    Color c = palette != null && idx < palette.Length ? palette[idx] : Color.FromArgb(idx, idx, idx);
                    int offset = (y * stride) + (x * 4);
                    buffer[offset + 0] = c.B;
                    buffer[offset + 1] = c.G;
                    buffer[offset + 2] = c.R;
                    buffer[offset + 3] = c.A;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, bmpData.Scan0, buffer.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        /// <summary>
        /// Renders 8bpp indexed data stored as 8x8 TILES (row-major within each tile,
        /// tiles themselves laid out left-to-right/top-to-bottom) rather than one flat
        /// width*height raster -- the layout GBA BG tile VRAM (as opposed to bitmap-mode
        /// framebuffers) actually uses, and what every other sprite/tileset reader in
        /// this DLL assumes (see Rendering.ItemIconReader et al). Use this instead of
        /// <see cref="RenderIndexed"/> for data targeting BG/OBJ tile VRAM.
        /// </summary>
        public static Bitmap RenderIndexedTiled(byte[] data, int tilesWide, int tilesHigh, Color[]? palette)
        {
            const int tileSize = 8, bytesPerTile = 64; // 8x8 pixels, 8bpp = 64 bytes/tile
            int width = tilesWide * tileSize, height = tilesHigh * tileSize;
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, width, height);
            var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
            int stride = bmpData.Stride;
            byte[] buffer = new byte[stride * height];

            int tileIndex = 0;
            for (int ty = 0; ty < tilesHigh; ty++)
            {
                for (int tx = 0; tx < tilesWide; tx++)
                {
                    int tileOffset = tileIndex * bytesPerTile;
                    if (tileOffset + bytesPerTile > data.Length) { tileIndex++; continue; }

                    for (int py = 0; py < tileSize; py++)
                    {
                        for (int px = 0; px < tileSize; px++)
                        {
                            int idx = data[tileOffset + (py * tileSize) + px];
                            Color c = palette != null && idx < palette.Length ? palette[idx] : Color.FromArgb(idx, idx, idx);
                            int offset = (((ty * tileSize) + py) * stride) + (((tx * tileSize) + px) * 4);
                            buffer[offset + 0] = c.B;
                            buffer[offset + 1] = c.G;
                            buffer[offset + 2] = c.R;
                            buffer[offset + 3] = c.A;
                        }
                    }
                    tileIndex++;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, bmpData.Scan0, buffer.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }
    }

    public readonly struct CreditsEntry(string title, string subtitle)
    {
        public string Title { get; } = title;
        public string Subtitle { get; } = subtitle;
    }

    /// <summary>
    /// The text-only credits roll (Credits_DrawScreen_DevelopedByWebfoot @0x08001FE8) --
    /// CONFIRMED via IDA: flat-color BG tilemap fill plus font-rendered text, no bitmap
    /// at all. g_CreditsScreenChain (@0x081D6054) is a flat array of 60 8-byte
    /// {titlePtr, subtitlePtr} pairs, immediately followed by the string data itself in
    /// ROM (60*8 = 0x1E0 = exactly the gap to the first string).
    /// </summary>
    public static class DeveloperCredits
    {
        public const int ChainAddress = 0x081D6054;
        public const int EntryCount = 60; // CONFIRMED via IDA (sub_8002240, the pair-count helper Credits_State_Update calls)
        private const int EntryStride = 8;

        public static List<CreditsEntry> GetEntries(ROM rom)
        {
            var entries = new List<CreditsEntry>();

            for (int i = 0; i < EntryCount; i++)
            {
                rom.PushPosition((ChainAddress & 0x00FFFFFF) + (i * EntryStride));
                int titlePtr = rom.ReadPointer();
                int subtitlePtr = rom.ReadPointer();
                rom.PopPosition();

                string title = titlePtr != 0 ? ReadCString(rom, titlePtr) : "";
                string subtitle = subtitlePtr != 0 ? ReadCString(rom, subtitlePtr) : "";

                if (title.Length > 0 || subtitle.Length > 0)
                    entries.Add(new CreditsEntry(title, subtitle));
            }

            return entries;
        }

        private static string ReadCString(ROM rom, int address)
        {
            rom.PushPosition(address);
            string s = rom.ReadNullTerminatedString();
            rom.PopPosition();
            return s;
        }
    }

    /// <summary>
    /// The REAL pre-title "video": a genuine ~18-second, 181-frame animation with
    /// delta-encoded frames. An earlier pass in this codebase misidentified
    /// GameIntroVideo_Create (formerly named AttractMode_Create) as the credits roll --
    /// that was WRONG and has been corrected. It is not related to
    /// <see cref="DeveloperIntro"/> (independent full-frame slides) at all.
    ///
    /// CONFIRMED via IDA 2026-09 (GameIntroVideo_Update_DecodeFrame @0x0801A416):
    /// FrameTableAddress starts with a frame count (dword, =181 in this ROM build),
    /// followed by a 128-dword (512-byte) block whose contents aren't mapped yet, then
    /// an array of per-frame resource pointers starting at dword index 129 (byte
    /// offset 516). Each frame's resource is a normal [format][size][JCALG1 data]
    /// block; format 2 is GameIntroVideo's OWN repurposing of that field to mean
    /// "decompress into scratch, then ADDITIVELY BLEND every byte (mod 256) onto the
    /// previous frame" -- a genuine delta/diff frame, not a new independent image
    /// (matches the real ARM code's 4-way SWAR byte-add exactly; a plain per-byte add
    /// in managed code reproduces the identical result). Any other format value
    /// replaces the buffer outright (a keyframe) -- frame 0 is confirmed format 1 in
    /// this ROM build, so decoding always starts from a real keyframe.
    ///
    /// Runs once looping before the title screen is reachable, is skippable with
    /// A/Start, and replays again after the title screen's confirmed 30-second idle
    /// timeout (see TitleScreen_HandleInput / TitleScreen_State_DispatchOnMenuChoice).
    ///
    /// Palette is NOT independently confirmed -- an earlier pass here guessed the OBJ
    /// palette (0x081DA6C8) and it rendered as color noise (structurally correct
    /// playback -- right frame count, right delta accumulation -- garbage colors,
    /// confirmed by direct observation). Three candidates are exposed via
    /// <see cref="PaletteSource"/> rather than picking one blind a second time:
    ///   - Embedded: the 128-dword (512-byte = exactly a 256-color BGR555 table) block
    ///     between the frame count and the frame-pointer array (byte offset 4-516 of
    ///     FrameTableAddress) -- previously logged as "unmapped", but its size is a
    ///     suspiciously exact match for a baked-in palette, and GameIntroVideo_Create
    ///     never DMAs an external one, which a self-contained palette would explain.
    ///   - BG: 0x081D4F50, the palette DeveloperIntro (the screen that plays
    ///     immediately before this one, with no palette-clearing step in between) uses.
    ///   - OBJ: 0x081DA6C8, the original (disproven) guess -- kept for completeness.
    /// If colors are still wrong with all three, this needs an actual DMA trace, not
    /// another guess.
    /// </summary>
    public static class GameIntroVideo
    {
        public const int FrameWidth = 240;
        public const int FrameHeight = 160;
        public const int FrameTableAddress = 0x087FB8D0;
        private const int HeaderWordCount = 129; // frame count (1 word) + 128 palette/unmapped words, before the pointer array

        public enum PaletteSource { Embedded, BG, OBJ }

        public const int BGPaletteAddress = 0x081D4F50;
        public const int OBJPaletteAddress = 0x081DA6C8;

        public static int GetFrameCount(ROM rom)
        {
            rom.PushPosition(FrameTableAddress & 0x00FFFFFF);
            int count = rom.ReadInt();
            rom.PopPosition();
            return count;
        }

        /// <summary>Every frame's own resource address (real GBA 0x08XXXXXX), in playback order -- for asset extraction (each is its own [format][size][data] block, independent of the blend applied at playback).</summary>
        public static List<int> GetFrameResourceAddresses(ROM rom)
        {
            int count = GetFrameCount(rom);
            var addresses = new List<int>(count);
            rom.PushPosition((FrameTableAddress & 0x00FFFFFF) + (HeaderWordCount * 4));
            for (int i = 0; i < count; i++)
                addresses.Add(rom.ReadPointer());
            rom.PopPosition();
            return addresses;
        }

        /// <summary>
        /// One frame resource's raw decompressed bytes, UNBLENDED -- i.e. exactly what's
        /// stored at that ROM address, before DecodeAllFramesRaw applies the delta-blend
        /// (for format==2 frames) onto the running buffer. Use this for asset extraction
        /// (dumping "what's really at this address"); use DecodeAllFramesRaw for correct
        /// playback.
        /// </summary>
        public static byte[] GetRawFrameBytes(ROM rom, int resourceAddress) => JCALG1.Decompress(rom, resourceAddress);

        public static Color[] GetPalette(ROM rom, PaletteSource source = PaletteSource.Embedded) => source switch
        {
            PaletteSource.Embedded => ReadPalette(rom, (FrameTableAddress & 0x00FFFFFF) + 4),
            PaletteSource.BG => ReadPalette(rom, BGPaletteAddress & 0x00FFFFFF),
            PaletteSource.OBJ => ReadPalette(rom, OBJPaletteAddress & 0x00FFFFFF),
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };

        private static Color[] ReadPalette(ROM rom, int fileOffset)
        {
            rom.PushPosition(fileOffset);
            var colors = new Color[256];
            for (int i = 0; i < colors.Length; i++)
            {
                int c = rom.ReadShort();
                int r = ((c & 0x1F) * 0x21) >> 2;
                int g = (((c >> 5) & 0x1F) * 0x21) >> 2;
                int b = (((c >> 10) & 0x1F) * 0x21) >> 2;
                colors[i] = Color.FromArgb(0xFF, r, g, b);
            }
            colors[0] = Color.FromArgb(0, 0, 0, 0); // palette index 0 = transparent, GBA convention
            rom.PopPosition();
            return colors;
        }

        public readonly struct DecodedFrame(byte[] data, int format, int resourceAddress)
        {
            /// <summary>Raw 240x160 8bpp indexed bytes, post-blend -- ready to render or dump.</summary>
            public byte[] Data { get; } = data;
            /// <summary>This frame's own resource format flag (2 = delta, applied onto the previous frame; anything else = keyframe, replaces outright).</summary>
            public int Format { get; } = format;
            public bool IsDelta => Format == 2;
            /// <summary>Real GBA address (0x08XXXXXX) of this frame's [format][size][data] resource.</summary>
            public int ResourceAddress { get; } = resourceAddress;
        }

        /// <summary>
        /// Decodes every frame in playback order, applying the real delta-blend codec
        /// (see class doc). Frames must be decoded in order (each depends on the
        /// running buffer state left by the previous one).
        /// </summary>
        public static List<DecodedFrame> DecodeAllFramesRaw(ROM rom)
        {
            int count = GetFrameCount(rom);
            var frames = new List<DecodedFrame>(count);
            var buffer = new byte[FrameWidth * FrameHeight];

            for (int i = 0; i < count; i++)
            {
                int entryAddress = (FrameTableAddress & 0x00FFFFFF) + ((HeaderWordCount + i) * 4);
                rom.PushPosition(entryAddress);
                int resourceAddress = rom.ReadPointer();
                rom.PopPosition();

                rom.PushPosition(resourceAddress);
                int format = rom.ReadInt();
                rom.PopPosition();

                byte[] decoded = JCALG1.Decompress(rom, resourceAddress);
                int n = Math.Min(buffer.Length, decoded.Length);

                if (format == 2)
                {
                    // Delta frame: byte-wise mod-256 add onto the running buffer.
                    for (int p = 0; p < n; p++)
                        buffer[p] = (byte)(buffer[p] + decoded[p]);
                }
                else
                {
                    // Keyframe: replace outright.
                    Array.Copy(decoded, buffer, n);
                }

                frames.Add(new DecodedFrame((byte[])buffer.Clone(), format, resourceAddress));
            }

            return frames;
        }

        // 240x160 = 30x20 tiles of 8x8 -- this data targets BG tile VRAM (unk_6004000,
        // which falls in 0x06000000-0x0600FFFF), not a bitmap-mode framebuffer, so it
        // must be rendered tiled (see DeveloperIntro.RenderIndexedTiled's doc) -- an
        // earlier pass here used the flat DeveloperIntro.RenderIndexed and it came out
        // as scrambled noise, confirmed by direct observation once the palette (a
        // separate, now-fixed bug) was already correct.
        private const int TilesWide = FrameWidth / 8;
        private const int TilesHigh = FrameHeight / 8;

        public static List<Bitmap> DecodeAllFrames(ROM rom, Color[]? palette = null)
        {
            var frames = new List<Bitmap>();
            foreach (var raw in DecodeAllFramesRaw(rom))
                frames.Add(DeveloperIntro.RenderIndexedTiled(raw.Data, TilesWide, TilesHigh, palette));
            return frames;
        }
    }

    /// <summary>
    /// Writes replacement developer-intro slide art into a ROM.
    ///
    /// CONFIRMED via IDA 2026-09 (Resource_LoadOrDecompress_impl @0x0801F396): the
    /// game's generic asset loader supports THREE resource formats via a leading
    /// int32 flag -- 0 = stored/uncompressed (a plain memcpy_unaligned), 1 or 2 =
    /// JCALG1-compressed. So dropping in new art needs NO compressor: write a
    /// [format=0][size][raw 240x160 8bpp indexed bytes] block anywhere in the ROM,
    /// then patch the slide's LiteralPoolAddress to point at it. Old compressed data
    /// is left in place (dead bytes) rather than zeroed, matching this DLL's existing
    /// convention elsewhere (see Rendering.EntityWriter) of not risking a miscalculated
    /// zero-out clobbering adjacent live data.
    /// </summary>
    public static class DeveloperIntroWriter
    {
        /// <summary>
        /// Appends a format=0 "stored" resource block and returns its ROM file offset.
        /// </summary>
        public static int WriteStoredResource(ROM rom, byte[] rawIndexedPixels)
        {
            var block = new byte[8 + rawIndexedPixels.Length];
            BitConverter.GetBytes(0).CopyTo(block, 0); // format = 0 (stored/uncompressed)
            BitConverter.GetBytes(rawIndexedPixels.Length).CopyTo(block, 4);
            rawIndexedPixels.CopyTo(block, 8);

            int offset = rom.AllocateFreeSpace(block.Length);
            rom.WriteBytesAt(offset, block);
            return offset;
        }

        /// <summary>Repoints a slide's literal-pool pointer at a new ROM file offset.</summary>
        public static void RedirectSlide(ROM rom, DeveloperIntroSlide slide, int newDataFileOffset) =>
            rom.PatchInt32(slide.LiteralPoolAddress & 0x00FFFFFF, 0x08000000 | newDataFileOffset);

        /// <summary>
        /// Writes <paramref name="rawIndexedPixels"/> as a new stored resource and
        /// redirects <paramref name="slide"/> to it in one step. Pixels must be exactly
        /// DeveloperIntro.FrameWidth * FrameHeight bytes (240x160 8bpp indices into the
        /// shared palette, see DeveloperIntro.GetPalette) -- this does not quantize or
        /// resize an image for you.
        /// </summary>
        public static void ReplaceSlide(ROM rom, DeveloperIntroSlide slide, byte[] rawIndexedPixels)
        {
            int expected = DeveloperIntro.FrameWidth * DeveloperIntro.FrameHeight;
            if (rawIndexedPixels.Length != expected)
                throw new ArgumentException($"Expected {expected} bytes (240x160 8bpp indexed), got {rawIndexedPixels.Length}.", nameof(rawIndexedPixels));

            int offset = WriteStoredResource(rom, rawIndexedPixels);
            RedirectSlide(rom, slide, offset);
        }
    }

    /// <summary>
    /// The title screen's idle spinning-ball animation -- CONFIRMED via IDA
    /// (TitleScreen_UpdateBallAnimation @0x080003FA): 8 individually JCALG1-compressed
    /// frames, listed in g_TitleBallAnim_JCALG1FrameTable (dword[0]=count=8, followed
    /// by 8 raw ROM addresses). Unlike DeveloperIntro/GameIntroVideo's resources, these
    /// have NO [format][size] header -- called directly via jcalg1_iwram_dst on the raw
    /// stream, so decoding is JCALG1.DecompressUnknownHeader (skip 4 bytes, then decode
    /// with no length hint), not JCALG1.Decompress. CONFIRMED via IDA: plays directly
    /// after GameIntroVideo finishes/is skipped (Credits is NOT part of this chain --
    /// its real caller still isn't confirmed, do not conflate the two). Plays while
    /// the title screen waits for input -- distinct from GameIntroVideo (the pre-title
    /// video, plays before this) and DeveloperIntro (the boot-time slideshow).
    ///
    /// Renders TILED (8x8 blocks, see DeveloperIntro.RenderIndexedTiled's doc) -- this
    /// ROM's "Sprite_BlitToVRAM"-based assets consistently target BG tile VRAM
    /// (0x06000000-0x0600FFFF) rather than a bitmap-mode framebuffer (CONFIRMED the hard
    /// way: GameIntroVideo rendered as scrambled noise under a flat decode and was fixed
    /// by switching to tiled -- same asset family, same fix applies here).
    ///
    /// Palette: OBJ palette (0x081DA6C8) -- CONFIRMED via an actual traced DMA
    /// (sub_80005A4: DMA3SAD=&g_ObjPalette, DMA3DAD=0x05000200 = OBJ palette RAM), not a
    /// guess like GameIntroVideo's palette originally was.
    ///
    /// Frame pixel dimensions: CONFIRMED via IDA 2026-09 (TitleScreen_Create @0x0800020C,
    /// `sub_8020E5C(a1: v1+66, Width: 32, Height: 32)`) -- the ball's own canvas is
    /// allocated at exactly 32x32 pixels (4x4 tiles at 8bpp). An earlier pass here
    /// guessed a square tile grid from the decompressed byte count instead of using
    /// this confirmed size, and the guess silently matched zero frames (every frame
    /// skipped, tool showed no ball at all) -- fixed to use the real size directly.
    /// </summary>
    public static class TitleScreenBall
    {
        public const int FrameTableAddress = 0x081D8E28;
        public const int PaletteAddress = 0x081DA6C8;
        public const int FrameWidth = 32;
        public const int FrameHeight = 32;
        private const int MaxFrames = 64; // sanity bound

        /// <summary>Every frame's own resource address (real GBA 0x08XXXXXX) -- for asset extraction.</summary>
        public static List<int> GetFrameResourceAddresses(ROM rom)
        {
            rom.PushPosition(FrameTableAddress & 0x00FFFFFF);
            int count = rom.ReadInt();
            var addresses = new List<int>();
            for (int i = 0; i < count && i < MaxFrames; i++)
                addresses.Add(rom.ReadPointer());
            rom.PopPosition();
            return addresses;
        }

        /// <summary>
        /// One frame's raw decompressed bytes. CONFIRMED 2026-09 (direct test against the
        /// real ROM): these 8 frames are back-to-back headerless JCALG1 streams -- each
        /// decompression's end offset lands EXACTLY on the next frame's start address, and
        /// each one decompresses to precisely 1024 bytes (32x32 8bpp). An earlier pass here
        /// used JCALG1.DecompressUnknownHeader, which skips 4 bytes before decoding -- WRONG
        /// for this table specifically (there's no leading value to skip at all), and it
        /// made the decompressor read from the wrong bit position and crash with
        /// IndexOutOfRangeException on every single frame. DecompressSequential (no skip)
        /// is the correct call here.
        /// </summary>
        public static byte[] GetRawFrameBytes(ROM rom, int resourceAddress)
        {
            rom.PushPosition(resourceAddress & 0x00FFFFFF);
            var result = JCALG1.DecompressSequential(rom, FrameWidth * FrameHeight);
            rom.PopPosition();
            return result.Data;
        }

        public static Color[] GetPalette(ROM rom)
        {
            rom.PushPosition(PaletteAddress & 0x00FFFFFF);
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

        public static List<Bitmap> GetFrames(ROM rom, Color[]? palette = null)
        {
            var frames = new List<Bitmap>();

            foreach (int ptr in GetFrameResourceAddresses(rom))
            {
                if (ptr == 0) continue;
                try
                {
                    byte[] data = GetRawFrameBytes(rom, ptr);
                    frames.Add(DeveloperIntro.RenderIndexedTiled(data, FrameWidth / 8, FrameHeight / 8, palette));
                }
                catch
                {
                    // Corrupt/unexpected -- skip rather than render garbage.
                }
            }

            return frames;
        }
    }
}
