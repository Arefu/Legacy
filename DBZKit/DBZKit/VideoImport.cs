using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DBZKit
{
    /// <summary>
    /// Reads a video file (MP4/H.264, or anything else Windows can play) into 240x160 frames at a fixed frame rate, using Windows'
    /// own Media Foundation decoder -- nothing to install. Frames are taken at <c>fps</c> by holding the most recent decoded picture
    /// at each tick, so a 30 or 60 fps source comes out at the game's rate with the same running time.
    /// </summary>
    internal static class VideoImport
    {
        /// <summary>How the source picture is fitted into 240x160.</summary>
        public enum Fit { Letterbox, Crop, Stretch }

        /// <summary>
        /// Decodes the video and calls <paramref name="onFrame"/> once per output frame, in order (the bitmap is only valid during the
        /// call -- clone it to keep it). Returns how many frames were produced. Streams, so a long clip never sits in memory whole.
        /// </summary>
        public static int ReadFrames(string path, int width, int height, double fps, Fit fit, int maxFrames, Action<Bitmap> onFrame, IProgress<string>? progress = null)
        {
            Check(MFStartup(MF_VERSION, 0), "start Media Foundation");
            try
            {
                Check(MFCreateAttributes(out var attrs, 1), "create reader settings");
                var enableProcessing = MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING;
                Check(attrs.SetUINT32(ref enableProcessing, 1), "enable video conversion");
                int hr = MFCreateSourceReaderFromURL(path, attrs, out var reader);
                if (hr < 0) throw new InvalidOperationException($"Windows couldn't open this video (0x{hr:X8}). MP4 (H.264) works on any Windows that plays videos; \"N\" editions need the Media Feature Pack.");
                Marshal.ReleaseComObject(attrs);
                try { return Decode(reader, width, height, fps, fit, maxFrames, onFrame, progress); }
                finally { Marshal.ReleaseComObject(reader); }
            }
            finally { MFShutdown(); }
        }

        private static int Decode(IMFSourceReader reader, int width, int height, double fps, Fit fit, int maxFrames, Action<Bitmap> onFrame, IProgress<string>? progress)
        {
            Check(reader.SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, false), "select streams");
            Check(reader.SetStreamSelection(MF_SOURCE_READER_FIRST_VIDEO_STREAM, true), "select the video stream");

            Check(MFCreateMediaType(out var wanted), "create a media type");
            var major = MF_MT_MAJOR_TYPE; var video = MFMediaType_Video;
            var subtype = MF_MT_SUBTYPE; var rgb32 = MFVideoFormat_RGB32;
            Check(wanted.SetGUID(ref major, ref video), "ask for video");
            Check(wanted.SetGUID(ref subtype, ref rgb32), "ask for RGB32");
            int hr = reader.SetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, IntPtr.Zero, wanted);
            Marshal.ReleaseComObject(wanted);
            if (hr < 0) throw new InvalidOperationException($"This file has no video Windows can decode (0x{hr:X8}).");

            var (srcW, srcH, stride) = FrameFormat(reader);
            int count = 0;
            long tick = (long)Math.Round(10_000_000 / fps); // 100 ns units
            long nextTime = 0;
            // The latest decoded picture, box-averaged down to a few times the target size as it is copied out (a 4K frame is ~50 MB;
            // two of those don't fit a 32-bit process). The final high-quality scale only runs if a tick actually shows it -- a 30/60
            // fps source has 3-6 decoded pictures per output frame.
            Reduced? held = null;
            Bitmap? heldPicture = null;

            Bitmap Held() => heldPicture ??= Scale(held!.Value, width, height, fit);

            try
            {
                while (count < maxFrames)
                {
                    hr = reader.ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, out _, out int flags, out long time, out var sample);
                    if (hr < 0) throw new InvalidOperationException($"Decoding failed at {time / 1e7:0.00}s (0x{hr:X8}).");
                    if ((flags & MF_SOURCE_READERF_CURRENTMEDIATYPECHANGED) != 0) (srcW, srcH, stride) = FrameFormat(reader);
                    if (sample == null)
                    {
                        if ((flags & MF_SOURCE_READERF_ENDOFSTREAM) != 0) break;
                        continue;
                    }

                    Reduced picture;
                    try { picture = ReduceFrame(sample, srcW, srcH, stride, width, height); }
                    finally { Marshal.ReleaseComObject(sample); }

                    // Every tick before this picture's time shows the previous picture.
                    while (held != null && nextTime < time && count < maxFrames) { onFrame(Held()); count++; nextTime += tick; }
                    heldPicture?.Dispose();
                    heldPicture = null;
                    held = picture;
                    progress?.Report($"{time / 1e7:0.0}s ({count} frames)");
                    if ((flags & MF_SOURCE_READERF_ENDOFSTREAM) != 0) break;
                }
                if (held != null && count < maxFrames) { onFrame(Held()); count++; } // the last picture gets its tick
                return count;
            }
            finally { heldPicture?.Dispose(); }
        }

        private static (int W, int H, int Stride) FrameFormat(IMFSourceReader reader)
        {
            Check(reader.GetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, out var type), "read the video format");
            try
            {
                var sizeKey = MF_MT_FRAME_SIZE;
                Check(type.GetUINT64(ref sizeKey, out long size), "read the frame size");
                int w = (int)(size >> 32), h = (int)(size & 0xFFFFFFFF);
                var strideKey = MF_MT_DEFAULT_STRIDE;
                int stride = type.GetUINT32(ref strideKey, out int s) >= 0 ? s : w * 4;
                return (w, h, stride);
            }
            finally { Marshal.ReleaseComObject(type); }
        }

        /// <summary>A decoded picture after the cheap first reduction: tightly packed BGRX rows.</summary>
        private readonly record struct Reduced(byte[] Pixels, int W, int H);

        // Copies the picture out of Media Foundation's buffer one row at a time, averaging f x f blocks (the same f both ways, so the
        // aspect ratio is kept) down to no less than twice the target size. The high-quality scale afterwards does the rest.
        private static Reduced ReduceFrame(IMFSample sample, int srcW, int srcH, int stride, int width, int height)
        {
            int f = Math.Max(1, Math.Min(srcW / (width * 2), srcH / (height * 2)));
            int rw = srcW / f, rh = srcH / f, absStride = Math.Abs(stride);
            var pixels = new byte[rw * rh * 4];
            int area = f * f;

            Check(sample.ConvertToContiguousBuffer(out var buffer), "read a frame");
            try
            {
                Check(buffer.Lock(out IntPtr data, out _, out int length), "lock a frame");
                try
                {
                    // Output rows are independent (each reads its own f source rows), so they are split across cores.
                    Parallel.For(0, rh, () => (Row: new byte[srcW * 4], Sums: new int[rw * 3]), (oy, _, local) =>
                    {
                        var (row, sums) = local;
                        Array.Clear(sums);
                        for (int dy = 0; dy < f; dy++)
                        {
                            int y = oy * f + dy;
                            int from = stride >= 0 ? y * absStride : (srcH - 1 - y) * absStride; // negative stride = bottom-up rows
                            if (from + row.Length > length) continue;
                            Marshal.Copy(data + from, row, 0, row.Length);
                            for (int ox = 0, x = 0; ox < rw; ox++)
                                for (int dx = 0; dx < f; dx++, x++)
                                {
                                    sums[ox * 3] += row[x * 4];
                                    sums[ox * 3 + 1] += row[x * 4 + 1];
                                    sums[ox * 3 + 2] += row[x * 4 + 2];
                                }
                        }
                        int at = oy * rw * 4;
                        for (int ox = 0; ox < rw; ox++, at += 4)
                        {
                            pixels[at] = (byte)(sums[ox * 3] / area);
                            pixels[at + 1] = (byte)(sums[ox * 3 + 1] / area);
                            pixels[at + 2] = (byte)(sums[ox * 3 + 2] / area);
                            pixels[at + 3] = 255;
                        }
                        return local;
                    }, _ => { });
                }
                finally { buffer.Unlock(); }
            }
            finally { Marshal.ReleaseComObject(buffer); }
            return new Reduced(pixels, rw, rh);
        }

        private static Bitmap Scale(Reduced picture, int width, int height, Fit fit)
        {
            using var source = new Bitmap(picture.W, picture.H, PixelFormat.Format32bppRgb);
            var bits = source.LockBits(new Rectangle(0, 0, picture.W, picture.H), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                for (int y = 0; y < picture.H; y++)
                    Marshal.Copy(picture.Pixels, y * picture.W * 4, bits.Scan0 + y * bits.Stride, picture.W * 4);
            }
            finally { source.UnlockBits(bits); }
            return Scale(source, width, height, fit);
        }

        private static Bitmap Scale(Bitmap source, int width, int height, Fit fit)
        {
            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(result);
            g.Clear(Color.Black);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingMode = CompositingMode.SourceCopy;
            using var wrap = new ImageAttributes();
            wrap.SetWrapMode(WrapMode.TileFlipXY); // no dark fringe at the edges

            double sx = (double)width / source.Width, sy = (double)height / source.Height;
            Rectangle dest = new(0, 0, width, height), src = new(0, 0, source.Width, source.Height);
            if (fit == Fit.Letterbox)
            {
                double s = Math.Min(sx, sy);
                int w = (int)Math.Round(source.Width * s), h = (int)Math.Round(source.Height * s);
                dest = new Rectangle((width - w) / 2, (height - h) / 2, w, h);
            }
            else if (fit == Fit.Crop)
            {
                double s = Math.Max(sx, sy);
                int w = (int)Math.Round(width / s), h = (int)Math.Round(height / s);
                src = new Rectangle((source.Width - w) / 2, (source.Height - h) / 2, w, h);
            }
            g.DrawImage(source, dest, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, wrap);
            return result;
        }

        private static void Check(int hr, string what)
        {
            if (hr < 0) throw new InvalidOperationException($"Couldn't {what} (0x{hr:X8}).");
        }

        // ---- Media Foundation interop (only the calls used above; other vtable slots are placeholders) ----

        private const int MF_VERSION = 0x00020070;
        private const int MF_SOURCE_READER_FIRST_VIDEO_STREAM = unchecked((int)0xFFFFFFFC);
        private const int MF_SOURCE_READER_ALL_STREAMS = unchecked((int)0xFFFFFFFE);
        private const int MF_SOURCE_READERF_ENDOFSTREAM = 0x2;
        private const int MF_SOURCE_READERF_CURRENTMEDIATYPECHANGED = 0x20;

        private static readonly Guid MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");
        private static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        private static readonly Guid MF_MT_FRAME_SIZE = new("1652c33d-d6b2-4012-b834-72030849a37d");
        private static readonly Guid MF_MT_DEFAULT_STRIDE = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
        private static readonly Guid MFMediaType_Video = new("73646976-0000-0010-8000-00AA00389B71");
        private static readonly Guid MFVideoFormat_RGB32 = new("00000016-0000-0010-8000-00AA00389B71");

        [DllImport("mfplat.dll")] private static extern int MFStartup(int version, int flags);
        [DllImport("mfplat.dll")] private static extern int MFShutdown();
        [DllImport("mfplat.dll")] private static extern int MFCreateAttributes(out IMFAttributes attributes, int initialSize);
        [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out IMFMediaType mediaType);
        [DllImport("mfreadwrite.dll")]
        private static extern int MFCreateSourceReaderFromURL([MarshalAs(UnmanagedType.LPWStr)] string url, IMFAttributes attributes, out IMFSourceReader reader);

        [ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFAttributes
        {
            void GetItem(); void GetItemType(); void CompareItem(); void Compare();
            [PreserveSig] int GetUINT32(ref Guid key, out int value);
            [PreserveSig] int GetUINT64(ref Guid key, out long value);
            void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString();
            void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid key, int value);
            void SetUINT64(); void SetDouble();
            [PreserveSig] int SetGUID(ref Guid key, ref Guid value);
            void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
        }

        [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFMediaType
        {
            void GetItem(); void GetItemType(); void CompareItem(); void Compare();
            [PreserveSig] int GetUINT32(ref Guid key, out int value);
            [PreserveSig] int GetUINT64(ref Guid key, out long value);
            void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString();
            void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
            [PreserveSig] int SetUINT32(ref Guid key, int value);
            void SetUINT64(); void SetDouble();
            [PreserveSig] int SetGUID(ref Guid key, ref Guid value);
            void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
            void GetMajorType(); void IsCompressedFormat(); void IsEqual(); void GetRepresentation(); void FreeRepresentation();
        }

        [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFSample
        {
            void GetItem(); void GetItemType(); void CompareItem(); void Compare(); void GetUINT32(); void GetUINT64();
            void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString();
            void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
            void SetUINT32(); void SetUINT64(); void SetDouble(); void SetGUID();
            void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
            void GetSampleFlags(); void SetSampleFlags(); void GetSampleTime(); void SetSampleTime(); void GetSampleDuration(); void SetSampleDuration();
            void GetBufferCount(); void GetBufferByIndex();
            [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
            void AddBuffer(); void RemoveBufferByIndex(); void RemoveAllBuffers(); void GetTotalLength(); void CopyToBuffer();
        }

        [ComImport, Guid("045FA593-8799-42b8-BC8D-8968C6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFMediaBuffer
        {
            [PreserveSig] int Lock(out IntPtr buffer, out int maxLength, out int currentLength);
            [PreserveSig] int Unlock();
            void GetCurrentLength(); void SetCurrentLength(); void GetMaxLength();
        }

        [ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFSourceReader
        {
            void GetStreamSelection();
            [PreserveSig] int SetStreamSelection(int streamIndex, [MarshalAs(UnmanagedType.Bool)] bool selected);
            void GetNativeMediaType();
            [PreserveSig] int GetCurrentMediaType(int streamIndex, out IMFMediaType mediaType);
            [PreserveSig] int SetCurrentMediaType(int streamIndex, IntPtr reserved, IMFMediaType mediaType);
            void SetCurrentPosition();
            [PreserveSig] int ReadSample(int streamIndex, int controlFlags, out int actualStreamIndex, out int streamFlags, out long timestamp, out IMFSample? sample);
            void Flush(); void GetServiceForStream(); void GetPresentationAttribute();
        }
    }
}
