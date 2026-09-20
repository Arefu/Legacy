using System.Runtime.InteropServices;

namespace DBZKit
{
    /// <summary>
    /// Minimal winmm waveOut player for one in-memory 16-bit PCM buffer: play from any frame (seek), pause, resume, stop, live position and
    /// device volume. No dependencies; the whole rendered song is queued as a single buffer.
    /// </summary>
    internal sealed class WaveOutPlayer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormatEx { public ushort Tag, Channels; public uint SamplesPerSec, AvgBytesPerSec; public ushort BlockAlign, Bits, Size; }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHdr { public IntPtr Data; public uint BufferLength, BytesRecorded; public IntPtr User; public uint Flags, Loops; public IntPtr Next, Reserved; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MmTime { public uint Type; public uint Value; public uint Pad; }

        private const uint WaveMapper = 0xFFFFFFFF, TimeBytes = 4, WhdrDone = 1;

        [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr h, uint device, ref WaveFormatEx format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern int waveOutClose(IntPtr h);
        [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] private static extern int waveOutPause(IntPtr h);
        [DllImport("winmm.dll")] private static extern int waveOutRestart(IntPtr h);
        [DllImport("winmm.dll")] private static extern int waveOutGetPosition(IntPtr h, ref MmTime time, int size);
        [DllImport("winmm.dll")] private static extern int waveOutSetVolume(IntPtr h, uint volume);

        private IntPtr _h, _hdr;
        private GCHandle _pin;
        private short[] _pcm = [];
        private int _channels = 1, _rate = 16000, _startFrame;
        private double _volume = 0.8;

        public bool IsPaused { get; private set; }
        public bool HasBuffer => _pcm.Length > 0;
        public int TotalFrames => _pcm.Length / Math.Max(1, _channels);
        public int Rate => _rate;
        public double Volume { get => _volume; set { _volume = Math.Clamp(value, 0, 1); ApplyVolume(); } }

        /// <summary>True while a queued buffer has not finished (paused counts as playing).</summary>
        public bool IsPlaying
        {
            get
            {
                if (_hdr == IntPtr.Zero) return false;
                var hdr = Marshal.PtrToStructure<WaveHdr>(_hdr);
                return (hdr.Flags & WhdrDone) == 0;
            }
        }

        public void Load(short[] pcm, int channels, int rate)
        {
            Stop();
            Close();
            _pcm = pcm; _channels = channels; _rate = rate; _startFrame = 0;
            var fmt = new WaveFormatEx { Tag = 1, Channels = (ushort)channels, SamplesPerSec = (uint)rate, Bits = 16, BlockAlign = (ushort)(channels * 2), AvgBytesPerSec = (uint)(rate * channels * 2) };
            if (waveOutOpen(out _h, WaveMapper, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) { _h = IntPtr.Zero; throw new InvalidOperationException("No audio output device is available."); }
            ApplyVolume();
        }

        public void Play(int startFrame = 0)
        {
            if (_h == IntPtr.Zero || _pcm.Length == 0) return;
            waveOutReset(_h);
            waveOutRestart(_h);   // reset does not clear a pending pause
            Release();
            _startFrame = Math.Clamp(startFrame, 0, Math.Max(0, TotalFrames - 1));
            _pin = GCHandle.Alloc(_pcm, GCHandleType.Pinned);
            _hdr = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHdr>());
            var hdr = new WaveHdr
            {
                Data = _pin.AddrOfPinnedObject() + _startFrame * _channels * 2,
                BufferLength = (uint)((TotalFrames - _startFrame) * _channels * 2),
            };
            Marshal.StructureToPtr(hdr, _hdr, false);
            int size = Marshal.SizeOf<WaveHdr>();
            waveOutPrepareHeader(_h, _hdr, size);
            IsPaused = false;
            waveOutWrite(_h, _hdr, size);
        }

        public void Pause() { if (_h != IntPtr.Zero && IsPlaying && !IsPaused) { waveOutPause(_h); IsPaused = true; } }
        public void Resume() { if (_h != IntPtr.Zero && IsPaused) { waveOutRestart(_h); IsPaused = false; } }

        public void Stop()
        {
            if (_h != IntPtr.Zero) waveOutReset(_h);
            Release();
            IsPaused = false;
        }

        /// <summary>Frames played so far (absolute position in the loaded buffer).</summary>
        public int PositionFrames
        {
            get
            {
                if (_h == IntPtr.Zero || _hdr == IntPtr.Zero) return _startFrame;
                var t = new MmTime { Type = TimeBytes };
                if (waveOutGetPosition(_h, ref t, Marshal.SizeOf<MmTime>()) != 0) return _startFrame;
                return Math.Min(TotalFrames, _startFrame + (int)(t.Value / (uint)(_channels * 2)));
            }
        }

        private void ApplyVolume()
        {
            if (_h == IntPtr.Zero) return;
            uint v = (uint)(_volume * 0xFFFF);
            waveOutSetVolume(_h, v | v << 16);
        }

        private void Release()
        {
            if (_hdr != IntPtr.Zero)
            {
                if (_h != IntPtr.Zero) waveOutUnprepareHeader(_h, _hdr, Marshal.SizeOf<WaveHdr>());
                Marshal.FreeHGlobal(_hdr);
                _hdr = IntPtr.Zero;
            }
            if (_pin.IsAllocated) _pin.Free();
        }

        private void Close()
        {
            if (_h != IntPtr.Zero) { waveOutClose(_h); _h = IntPtr.Zero; }
        }

        public void Dispose() { Stop(); Close(); }
    }
}
