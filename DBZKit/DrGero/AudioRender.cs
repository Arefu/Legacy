// STATUS (2026-09-20): complete for the effects the songs use (1, 4-8, 15, 17, 20, 24, 27). Effects 11, 12 and 21 are not used by any song and are
// played as plain notes. Not compared with real hardware output (see Audio-Notes.md).
using DrGero.IO;

namespace DrGero.Engine
{
    /// <summary>The result of rendering a song: 16-bit interleaved stereo PCM.</summary>
    public sealed class RenderedSong
    {
        public short[] Pcm { get; init; } = [];
        public int Rate { get; init; }
        public int Channels => 2;
        public double Seconds => Pcm.Length / 2.0 / Rate;
        /// <summary>Frame index at which each order-list entry starts (for a "position" display).</summary>
        public int[] OrderStartFrames { get; init; } = [];
        public bool Truncated { get; init; }
    }

    /// <summary>
    /// A re-implementation of the game's own sequencer + mixer (Sequencer_ParseRow, Sequencer_RunChannelEffect, Audio_Fx*, AudioVoice_*,
    /// see Audio-Notes.md) that renders a song to PCM so DBZKit can play it. Output is identical in structure to the console:
    /// per-voice 14-bit-fixed-point resampling, master volume 4, left = (64-pan), right = (64+pan), accumulate then >>6 and clamp to 8 bits.
    /// Effects 27 (unknown) and the unused 11/12/21 behave as a plain note-on. CERTAINTY: HIGH on the note/volume/pan/tempo/loop model,
    /// MEDIUM on slide/vibrato/envelope details (read from the decompile, never compared against real hardware output).
    /// </summary>
    public static class SongRenderer
    {
        public const int Rate = 15978;           // Timer0 reload 0xFBE6 -> 16777216 / 1050
        private const int MasterVolume = 4;      // AudioSystem_Init: MusicChannels_SetMasterVolume(..., 4)
        private const int TickNumerator = 40000; // samples per tick = 40000 / tempo

        private const int NoteTable = 0x7FC93C, VibratoDownTable = 0x7FCB9C, VibratoUpTable = 0x7FCC1C, PitchBendUpTable = 0x7FCE1C, PitchBendDownTable = 0x7FCE9C, SineTable = 0x7FD09C;

        private enum Handler { None, Attack, DecayStop, SlideDown, SlideUp, PortamentoDown, PortamentoUp, Vibrato, Retrigger, NoteDelay }

        private sealed class Voice
        {
            // stored event state (Sequencer_ParseRow)
            public int Flags, LastFlags, Note, Inst, Vol, Effect, Param;
            // voice state
            public int Pending;        // +16: 1 active, 2 pitch, 4 restart, 8 reload instrument, 0x10 volume
            public int Mode;           // +17: bit0 loop, bit1 ping-pong, bit2 currently reversed
            public int State;          // +74: bit0/1 handler modes, bit2 note playing, bit3 portamento armed
            public Handler Handler;
            public int FreqBase, FreqCur, FreqTarget;
            public int VolumeNow;      // +72 / +18
            public int Pan;            // +19, signed
            public int VolL, VolR;
            public long Pos, Step, LoopStart, End;
            public int Rate;
            public byte[] Pcm = [];
            public int InstrumentIndex;
            public int Slide, Env, Rate75, Retrig, RetrigCounter;   // +77, +76, +75, +78 (nibble), +82
            public int VibPhase, VibSpeed, VibDepth;
            public bool Active => (Pending & 1) != 0;
        }

        private sealed class Ctx(ROM rom, AudioTables.TrackHeader track)
        {
            public readonly ROM Rom = rom;
            public readonly AudioTables.TrackHeader Track = track;
            public readonly int[] Freq = ReadFreq(rom);
            public readonly ushort[] Down = ReadU16(rom, PitchBendDownTable, 256), Up = ReadU16(rom, PitchBendUpTable, 256), VibDown = ReadU16(rom, VibratoDownTable, 256), VibUp = ReadU16(rom, VibratoUpTable, 256);
            public readonly sbyte[] Sine = rom.ReadBytesAt(SineTable, 64).Select(b => (sbyte)b).ToArray();
            public readonly Dictionary<int, byte[]> Samples = [];
            public int BankSize = AudioTables.BankSize(rom, track.Bank);

            private static int[] ReadFreq(ROM r) { var b = r.ReadBytesAt(NoteTable, AudioTables.NoteCount * 4); return Enumerable.Range(0, AudioTables.NoteCount).Select(i => BitConverter.ToInt32(b, i * 4)).ToArray(); }
            private static ushort[] ReadU16(ROM r, int a, int n) { var b = r.ReadBytesAt(a, n * 2); return Enumerable.Range(0, n).Select(i => BitConverter.ToUInt16(b, i * 2)).ToArray(); }
        }

        public static RenderedSong Render(ROM rom, int trackIndex, Song song, double maxSeconds = 900)
        {
            var track = AudioTables.ReadTrack(rom, trackIndex);
            var ctx = new Ctx(rom, track);
            var v = new Voice[16];
            for (int i = 0; i < 16; i++) v[i] = new Voice { Vol = 64, VolumeNow = 64 };
            var pcm = new List<short>(1 << 20);
            var orderStarts = new List<int>();

            int tickLen = TickNumerator / Math.Max(1, song.Tempo), speed = Math.Max(1, song.Speed);
            int tickCounter = 1, orderIndex = -1, row = 0, patternRows = 1;
            SongPattern? pattern = null;
            int maxFrames = (int)(maxSeconds * Rate);
            bool finished = false, truncated = false;

            while (!finished)
            {
                if (--tickCounter == 0)
                {
                    row++;
                    if (row >= patternRows)
                    {
                        orderIndex++;
                        if (orderIndex >= song.Order.Count) break;               // the 0xFF terminator: the game loops here, we stop
                        pattern = song.Patterns[song.Order[orderIndex]];
                        patternRows = Math.Max(1, pattern.Rows.Count);
                        row = 0;
                        orderStarts.Add(pcm.Count / 2);
                    }
                    for (int c = 1; c <= 15; c++) v[c].Flags = 0;
                    foreach (var e in pattern!.Rows[row]) ApplyEvent(v[e.Channel], e);
                    for (int c = 1; c <= 15; c++) RunChannelEffect(ctx, v[c], ref tickLen, ref speed);
                    tickCounter = speed;
                }
                else
                {
                    for (int c = 1; c <= 15; c++) RunHandler(ctx, v[c]);
                }
                for (int c = 1; c <= 15; c++) Commit(ctx, v[c]);
                MixTicks(ctx, v, tickLen, pcm);
                if (pcm.Count / 2 >= maxFrames) { truncated = true; break; }
            }
            return new RenderedSong { Pcm = [.. pcm], Rate = Rate, OrderStartFrames = [.. orderStarts], Truncated = truncated };
        }

        // ---- Sequencer_ParseRow: store one event's data on its channel ------------------------------------------
        private static void ApplyEvent(Voice c, SongEvent e)
        {
            c.Flags = (e.Note != null ? 1 : 0) | (e.Instrument != null ? 2 : 0) | (e.Volume != null ? 4 : 0) | (e.Effect != null ? 8 : 0) | (e.Flags & 0xF0);
            if (e.Note != null) c.Note = e.Note.Value;
            if (e.Instrument != null) c.Inst = e.Instrument.Value;
            if (e.Volume != null) c.Vol = e.Volume.Value;
            if (e.Effect != null) { c.Effect = e.Effect.Value; c.Param = e.EffectParam; }
        }

        // ---- Sequencer_RunChannelEffect ---------------------------------------------------------------------------
        private static void RunChannelEffect(Ctx x, Voice c, ref int tickLen, ref int speed)
        {
            c.State = (c.State >> 2) << 2;                  // handler bits cleared on every new row
            if ((c.State & 4) != 0 && c.FreqCur != c.FreqBase) { c.FreqCur = c.FreqBase; c.Pending |= 2; }
            if (c.Flags == 0) return;
            int effect = (c.Flags & 0x88) == 0 ? 0 : c.Effect;
            switch (effect)
            {
                // Effect 1 stores its param in MusicChannel+3 (g_LastNoteChannel in the old IDA name) = ticks per row: it is SET SPEED (params 3-7 in the ROM).
                case 1: NoteOn(x, c); speed = Math.Max(1, c.Param); break;
                case 4: NoteOn(x, c); Envelope(c); break;
                case 5: NoteOn(x, c); SlideSetup(x, c, down: true); break;
                case 6: NoteOn(x, c); SlideSetup(x, c, down: false); break;
                case 7: Portamento(x, c); break;
                case 8: NoteOn(x, c); VibratoSetup(x, c); break;
                case 15: NoteOn(x, c); if ((c.Flags & 0x11) != 0) { c.Pos = (long)c.Param << 22; c.Pending &= ~4; c.Mode &= ~4; } break;
                case 17: NoteOn(x, c); if (c.Param != 0) c.Retrig = c.Param; c.Handler = Handler.Retrigger; c.RetrigCounter = c.Retrig & 0xF; c.State = ((c.State >> 2) << 2) + 2; break;
                case 20: NoteOn(x, c); if (c.Param > 0) tickLen = TickNumerator / c.Param; break;
                case 24: NoteOn(x, c); c.Pan = (sbyte)c.Param; c.Pending |= 0x10; break;
                // Effect 27 = NOTE DELAY (Audio_Fx27_NoteDelay 0x0802054C): the note is NOT triggered now; RetrigCounter = param and the per-tick
                // handler calls NoteOn when it reaches 0 (handler mode 2 = always runs).
                case 27: c.RetrigCounter = c.Param; c.Handler = Handler.NoteDelay; c.State = ((c.State >> 2) << 2) + 2; break;
                default: NoteOn(x, c); break;
            }
        }

        // ---- Audio_NoteOn ----------------------------------------------------------------------------------------
        private static void NoteOn(Ctx x, Voice c)
        {
            int f = c.Flags;
            if ((f & 0x11) != 0)
            {
                if (c.Note == 255) { c.State &= ~4; c.Pending &= ~1; return; }
                c.VolumeNow = c.Vol;
                c.FreqBase = x.Freq[Math.Clamp(c.Note, 0, x.Freq.Length - 1)];
                c.State = 4;
                if ((f & 0x20) != 0) { c.FreqCur = c.FreqBase; c.Pending |= 0x17; }
                else { c.InstrumentIndex = c.Inst; c.FreqCur = c.FreqBase; c.Pending |= 0x1F; }
            }
            else if ((f & 0x44) != 0)
            {
                c.VolumeNow = c.Vol;
                c.Pending |= 0x10;
            }
        }

        // ---- effect setup ----------------------------------------------------------------------------------------
        private static void Envelope(Voice c)
        {
            if (c.Param != 0) c.Env = c.Param;
            int v = c.Env;
            if ((v & 0xF) != 0)
            {
                if ((v >> 4) != 0)
                {
                    if ((~v & 0xF) != 0) { if ((v >> 4) == 15) { c.Rate75 = v & 0xF; DecayStop(c); } }
                    else { c.Rate75 = v >> 4; Attack(c); }
                }
                else { c.Rate75 = v; c.Handler = Handler.DecayStop; c.State |= 1; if (c.Rate75 == 15) DecayStop(c); }
            }
            else { c.Rate75 = v >> 4; c.Handler = Handler.Attack; c.State |= 1; if (c.Rate75 == 15) Attack(c); }
        }

        private static void Attack(Voice c)
        {
            c.VolumeNow += c.Rate75;
            if (c.VolumeNow >= 64) { c.VolumeNow = 64; c.State = (c.State >> 2) << 2; }
            c.Pending |= 0x10;
        }

        private static void DecayStop(Voice c)
        {
            c.VolumeNow -= c.Rate75;
            if (c.VolumeNow <= 0) { c.VolumeNow = 0; c.State = (c.State >> 2) << 2; }
            c.Pending |= 0x10;
        }

        private static void SlideSetup(Ctx x, Voice c, bool down)
        {
            if (c.Param != 0) c.Slide = c.Param;
            if ((c.State & 4) == 0) return;
            int hi = c.Slide >> 4, fine = c.Slide & 0xF;
            if (down)
            {
                if (hi == 15) SetBase(c, (long)c.FreqBase * x.Down[fine] >> 16);
                else if (hi == 14) SetBase(c, (long)c.FreqBase * x.Up[fine] >> 16);
                else { c.Handler = Handler.SlideDown; c.State |= 1; }
            }
            else
            {
                if (hi == 15) SetBase(c, (long)c.FreqBase * x.VibUp[fine] >> 14);
                else if (hi == 14) SetBase(c, (long)c.FreqBase * x.VibDown[fine] >> 15);
                else { c.Handler = Handler.SlideUp; c.State |= 1; }
            }
        }

        private static void SetBase(Voice c, long freq) { c.FreqBase = (int)freq; c.FreqCur = (int)freq; c.Pending |= 2; }

        private static void Portamento(Ctx x, Voice c)
        {
            if (c.Param != 0) c.Slide = c.Param;
            if ((c.State & 4) == 0) return;
            bool arm = false;
            if ((c.Flags & 0x11) != 0)
            {
                if (c.Note != 255) { c.FreqTarget = x.Freq[Math.Clamp(c.Note, 0, x.Freq.Length - 1)]; c.State |= 8; arm = true; }
            }
            else if ((c.State & 8) != 0) arm = true;
            if (arm) { c.Handler = c.FreqTarget <= c.FreqBase ? Handler.PortamentoDown : Handler.PortamentoUp; c.State |= 1; }
            if ((c.Flags & 0x44) != 0) { c.VolumeNow = c.Vol; c.Pending |= 0x10; }
        }

        private static void VibratoSetup(Ctx x, Voice c)
        {
            if ((c.State & 4) == 0) return;
            if ((c.Flags & 0x11) != 0) c.VibPhase = 0;
            int p = c.Param;
            if ((p & 0xF) != 0) c.VibDepth = (4 * p) & 0x3F;
            if ((p >> 4) != 0) c.VibSpeed = p >> 4;
            c.Handler = Handler.Vibrato; c.State |= 1;
            RunHandler(x, c, force: true);
        }

        // ---- per-tick handlers (Sequencer_Tick) ------------------------------------------------------------------
        private static void RunHandler(Ctx x, Voice c, bool force = false)
        {
            int mode = c.State & 3;
            if (!force && (mode == 0 || (mode == 1 && (c.State & 4) == 0))) return;
            switch (c.Handler)
            {
                case Handler.Attack: Attack(c); break;
                case Handler.DecayStop: DecayStop(c); break;
                case Handler.SlideDown: SetBase(c, (long)c.FreqBase * x.Down[c.Slide & 0xFF] >> 16); break;
                case Handler.SlideUp: SetBase(c, (long)c.FreqBase * x.VibUp[c.Slide & 0xFF] >> 14); break;
                case Handler.PortamentoDown:
                    SetBase(c, (long)c.FreqBase * x.Down[c.Slide & 0xFF] >> 16);
                    if (c.FreqBase < c.FreqTarget) { SetBase(c, c.FreqTarget); c.State = ((c.State & 0xF7) >> 2) << 2; }
                    break;
                case Handler.PortamentoUp:
                    SetBase(c, (long)c.FreqBase * x.VibUp[c.Slide & 0xFF] >> 14);
                    if (c.FreqBase > c.FreqTarget) { SetBase(c, c.FreqTarget); c.State = ((c.State & 0xF7) >> 2) << 2; }
                    break;
                case Handler.Vibrato:
                    c.VibPhase = (c.VibPhase + c.VibSpeed) & 0x3F;
                    int m = c.VibDepth * x.Sine[c.VibPhase] >> 6;
                    c.FreqCur = (int)(m >= 0 ? (long)c.FreqBase * x.VibDown[m] >> 15 : (long)c.FreqBase * x.Up[-m] >> 16);
                    c.Pending |= 2;
                    break;
                case Handler.NoteDelay:
                    if (--c.RetrigCounter == 0) NoteOn(x, c);
                    break;
                case Handler.Retrigger:
                    if (--c.RetrigCounter == 0) { c.Pending |= 5; c.RetrigCounter = c.Retrig & 0xF; c.State |= 4; }
                    break;
            }
        }

        // ---- AudioVoice_CommitPendingChanges ----------------------------------------------------------------------
        private static void Commit(Ctx x, Voice c)
        {
            if (!c.Active) return;
            if ((c.Pending & 0x10) != 0)
            {
                int v2 = MasterVolume * c.VolumeNow;
                c.VolL = ((64 - c.Pan) * v2) >> 10;
                c.VolR = ((c.Pan + 64) * v2) >> 10;
            }
            if ((c.Pending & 8) != 0)
            {
                int i = c.InstrumentIndex;
                if (i >= 0 && i < x.BankSize)
                {
                    var ins = AudioTables.ReadInstrument(x.Rom, x.Track.Bank, i);
                    if (!x.Samples.TryGetValue(i, out var pcm)) x.Samples[i] = pcm = AudioTables.ReadPcm(x.Rom, ins.Data, ins.Length);
                    c.Pcm = pcm; c.LoopStart = (long)ins.LoopStart << 14; c.End = (long)ins.Length << 14; c.Rate = ins.Rate; c.Mode = ins.Mode & 3;
                }
                else { c.Pcm = []; }
            }
            if ((c.Pending & 4) != 0) { c.Pos = 0; c.Mode &= ~4; }
            if ((c.Pending & 2) != 0)
            {
                long step = (4195L * (((long)c.FreqCur * c.Rate) >> 14) + 2047) >> 12;
                c.Step = (c.Mode & 4) != 0 ? -step : step;
            }
            c.Pending = 1;
        }

        // ---- AudioVoice_MixSamples ------------------------------------------------------------------------------
        private static void MixTicks(Ctx x, Voice[] voices, int frames, List<short> output)
        {
            var accL = new int[frames]; var accR = new int[frames];
            for (int c = 1; c <= 15; c++)
            {
                var v = voices[c];
                if (!v.Active) continue;
                if (v.Pcm.Length == 0 || (v.VolL == 0 && v.VolR == 0)) { AdvanceSilent(v, frames); continue; }
                for (int i = 0; i < frames && v.Active; i++)
                {
                    int idx = (int)(v.Pos >> 14);
                    if (idx >= 0 && idx < v.Pcm.Length)
                    {
                        int s = (sbyte)v.Pcm[idx];
                        accL[i] += s * v.VolL; accR[i] += s * v.VolR;
                    }
                    Advance(v);
                }
            }
            for (int i = 0; i < frames; i++)
            {
                output.Add((short)(Math.Clamp(accL[i] >> 6, -128, 127) << 8));
                output.Add((short)(Math.Clamp(accR[i] >> 6, -128, 127) << 8));
            }
        }

        private static void AdvanceSilent(Voice v, int frames) { for (int i = 0; i < frames && v.Active; i++) Advance(v); }

        private static void Advance(Voice v)
        {
            v.Pos += v.Step;
            bool loop = (v.Mode & 1) != 0 && v.End > v.LoopStart, ping = (v.Mode & 2) != 0;
            if (!loop)
            {
                if (v.Pos >= v.End) { v.Pending &= ~1; }
                return;
            }
            if (ping)
            {
                if ((v.Mode & 4) == 0) { if (v.Pos >= v.End) { v.Pos = v.End - 1 - (v.Pos - v.End); v.Step = -Math.Abs(v.Step); v.Mode |= 4; } }
                else if (v.Pos < v.LoopStart) { v.Pos = v.LoopStart + (v.LoopStart - v.Pos); v.Step = Math.Abs(v.Step); v.Mode &= ~4; }
            }
            else if (v.Pos >= v.End) v.Pos -= v.End - v.LoopStart;
        }
    }
}
