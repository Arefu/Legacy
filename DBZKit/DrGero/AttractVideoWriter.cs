using System.Drawing;
using System.Runtime.Versioning;
using DrGero.IO;
using DrGero.Rendering;

namespace DrGero.Boot
{
    /// <summary>
    /// Writes the attract-mode video (<see cref="GameIntroVideo"/>): one frame, or a whole new video.
    ///
    /// Layout (read side in GameIntroVideo): at FrameTableAddress, u32 frame count, 256 BGR555 palette colours (128 words), then one
    /// pointer per frame to a ResourceHeader. Mode 1 = keyframe (JCALG1, replaces the screen), mode 2 = delta (JCALG1, added byte-wise
    /// mod 256 onto the previous frame). Frames are 240x160 8bpp in 8x8 tile order. The stock video has 181 frames (88 keyframes, 93 deltas).
    ///
    /// Each frame is written as whichever of keyframe / delta compresses smaller (<see cref="JCALG1.Compress"/>), so a mostly-still
    /// video stays small. New blocks go into free space (old ones are left as dead bytes). A video with more frames than the table had
    /// moves the table and repoints the code literal(s) that hold its address (one in the US ROM, at 0x0801EDCC).
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    public static class AttractVideoWriter
    {
        public const int FrameBytes = GameIntroVideo.FrameWidth * GameIntroVideo.FrameHeight;
        private const int HeaderWords = 129;
        /// <summary>The cartridge window the GBA can address (0x08000000-0x09FFFFFF).</summary>
        public const long MaxRomBytes = 0x2000000;

        /// <summary>Playback rate: GameIntroVideo_Update_DecodeFrame shows a new frame every 6 vblanks (cmp r1, #6), ~9.95 fps.</summary>
        public const double FramesPerSecond = 59.7275 / 6;

        private static int Table(ROM rom) => GameIntroVideo.TableOffset(rom);

        /// <summary>The palette stored with the video (index 0 included; the viewer shows it as transparent).</summary>
        public static Color[] ReadPalette(ROM rom)
        {
            var raw = rom.ReadBytesAt(Table(rom) + 4, 512);
            return [.. Enumerable.Range(0, 256).Select(i => ImageImport.FromBgr555(BitConverter.ToUInt16(raw, i * 2)))];
        }

        /// <summary>Compresses one frame against the previous one; returns (mode, stream) for the smaller of keyframe / delta.</summary>
        private static (int Mode, byte[] Stream) Encode(byte[] frame, byte[]? previous)
        {
            var key = JCALG1.Compress(frame);
            if (previous == null) return (1, key);
            var delta = new byte[frame.Length];
            for (int i = 0; i < delta.Length; i++) delta[i] = (byte)(frame[i] - previous[i]);
            var diff = JCALG1.Compress(delta);
            return diff.Length < key.Length ? (2, diff) : (1, key);
        }

        private static int WriteFrame(ROM rom, byte[] frame, byte[]? previous)
        {
            var (mode, stream) = Encode(frame, previous);
            return ImageImport.WriteResource(rom, mode, FrameBytes, stream);
        }

        /// <summary>
        /// Replaces frame <paramref name="index"/> (tile-order indices into the video's palette). Only it and the next frame are rewritten:
        /// the next one is re-encoded against the new picture, so everything after it plays exactly as before.
        /// </summary>
        public static string ReplaceFrame(ROM rom, int index, byte[] tiledFrame)
        {
            if (tiledFrame.Length != FrameBytes) throw new ArgumentException($"A frame is {FrameBytes} bytes (240x160).");
            var frames = GameIntroVideo.DecodeAllFramesRaw(rom).Select(f => f.Data).ToList();
            if (index < 0 || index >= frames.Count) throw new ArgumentOutOfRangeException(nameof(index));
            frames[index] = tiledFrame;

            int pointers = Table(rom) + HeaderWords * 4;
            long before = rom.Length;
            for (int i = index; i <= Math.Min(index + 1, frames.Count - 1); i++)
            {
                int at = WriteFrame(rom, frames[i], i > 0 ? frames[i - 1] : null);
                rom.PatchInt32(pointers + i * 4, (int)(0x08000000u | (uint)at));
            }
            return $"Frame {index} replaced{(index + 1 < frames.Count ? $" (frame {index + 1} re-encoded against it)" : "")}; ROM grew by {rom.Length - before} bytes.";
        }

        /// <summary>
        /// Replaces the whole video with <paramref name="tiledFrames"/> and, if given, a new palette (up to 256 colours).
        /// </summary>
        public static string WriteVideo(ROM rom, IReadOnlyList<byte[]> tiledFrames, Color[]? palette, IProgress<int>? progress = null)
        {
            if (tiledFrames.Count == 0) throw new ArgumentException("A video needs at least one frame.");
            if (tiledFrames.Any(f => f.Length != FrameBytes)) throw new ArgumentException($"Every frame must be {FrameBytes} bytes (240x160).");

            int table = Table(rom), oldCount = GameIntroVideo.GetFrameCount(rom);
            var paletteBytes = palette == null ? rom.ReadBytesAt(table + 4, 512)
                : [.. Enumerable.Range(0, 256).SelectMany(i => BitConverter.GetBytes(i < palette.Length ? ImageImport.ToBgr555(palette[i]) : (ushort)0))];
            long before = rom.Length;

            // Compress everything first (each frame only needs the one before it, so in parallel), and check it fits before the ROM is
            // touched: the GBA maps at most 32 MB of cartridge, so nothing past that can be pointed at.
            var encoded = new (int Mode, byte[] Stream)[tiledFrames.Count];
            int done = 0;
            Parallel.For(0, tiledFrames.Count, i =>
            {
                encoded[i] = Encode(tiledFrames[i], i > 0 ? tiledFrames[i - 1] : null);
                progress?.Report(Interlocked.Increment(ref done));
            });
            long needed = encoded.Sum(e => (long)((8 + e.Stream.Length + 3) & ~3)) + (HeaderWords + tiledFrames.Count) * 4 + 64;
            if (rom.Length + needed > MaxRomBytes)
                throw new InvalidOperationException($"This video needs {needed / 1024 / 1024.0:0.0} MB but the ROM only has room for {(MaxRomBytes - rom.Length) / 1024 / 1024.0:0.0} MB more " +
                    "(a GBA cartridge tops out at 32 MB). Use a shorter clip. Nothing was changed.");

            // Frames first (they only append), then the table.
            var addresses = new int[tiledFrames.Count];
            for (int i = 0; i < tiledFrames.Count; i++)
                addresses[i] = ImageImport.WriteResource(rom, encoded[i].Mode, FrameBytes, encoded[i].Stream);

            int at = table;
            string moved = "";
            if (tiledFrames.Count > oldCount)
            {
                at = rom.AllocateFreeSpace((HeaderWords + tiledFrames.Count) * 4);
                int patched = Repoint(rom, table, (HeaderWords + oldCount) * 4, at);
                if (patched == 0) throw new InvalidOperationException("Couldn't find the code that points at the frame table, so a longer video can't be stored. Use at most " + oldCount + " frames.");
                moved = $" The frame table moved to 0x{0x08000000 | at:X8} ({patched} reference(s) repointed).";
            }
            var header = new byte[(HeaderWords + tiledFrames.Count) * 4];
            BitConverter.GetBytes(tiledFrames.Count).CopyTo(header, 0);
            paletteBytes.CopyTo(header, 4);
            for (int i = 0; i < addresses.Length; i++) BitConverter.GetBytes(0x08000000u | (uint)addresses[i]).CopyTo(header, (HeaderWords + i) * 4);
            rom.WriteBytesAt(at, header);

            return $"Wrote a {tiledFrames.Count}-frame attract video{(palette != null ? " with a new palette" : "")}; ROM grew by {rom.Length - before} bytes.{moved}";
        }

        /// <summary>Repoints every aligned word that points into [old, old + length) at the same offset from <paramref name="newAddress"/>.</summary>
        private static int Repoint(ROM rom, int old, int length, int newAddress)
        {
            uint lo = 0x08000000u | (uint)old, hi = lo + (uint)length;
            var bytes = rom.ToArray();
            int patched = 0;
            for (int o = 0; o + 4 <= bytes.Length; o += 4)
            {
                if (o >= old && o < old + length) continue;
                uint v = BitConverter.ToUInt32(bytes, o);
                if (v < lo || v >= hi) continue;
                rom.PatchInt32(o, (int)(0x08000000u | (uint)newAddress) + (int)(v - lo));
                patched++;
            }
            return patched;
        }
    }
}
