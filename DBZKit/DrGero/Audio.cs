using System.Text;
using System.Text.Json;
using DrGero.IO;

namespace DrGero.Engine
{
    // The game's own tracker-style sound engine (all layouts verified in IDA, see Audio-Notes.md):
    //   SFX   : 64 x {u32 data, u32 loop, u16 length, u16 rate}      at 0xE15B0, signed 8-bit PCM
    //   Songs : 44 x MusicTrackInfo (20 bytes)                      at 0x1D4B2C
    //   Bank  : 101 x {u32 data, u16 mode, u16 loopStart, u16 length, u16 rate} shared by every song (mode bit0 loop, bit1 ping-pong)
    // A song is an order list of pattern ids; a pattern is {u8 rows, then per row events ended by 0}. See SongCodec.

    public sealed record SfxSample(int Index, int Address, int Data, bool Loop, int Length, int Rate);

    public sealed record InstrumentSample(int Index, int Address, int Data, int Mode, int LoopStart, int Length, int Rate)
    {
        public bool Loops => (Mode & 1) != 0;
        public bool PingPong => (Mode & 2) != 0;
    }

    /// <summary>One pattern event. Null fields were not present in the event's flag byte.</summary>
    public sealed class SongEvent
    {
        public int Channel { get; set; }      // 1..15
        public int? Note { get; set; }        // index into the note table (60 = the instrument's native pitch), 255 = note off
        public int? Instrument { get; set; }
        public int? Volume { get; set; }      // 0..64
        public int? Effect { get; set; }
        public int EffectParam { get; set; }
        public int Flags { get; set; }        // raw flag byte, kept so a lossless round trip is possible
    }

    public sealed class SongPattern
    {
        public List<List<SongEvent>> Rows { get; set; } = [];
    }

    public sealed class Song
    {
        public int Tempo { get; set; } = 125;         // BPM-like: samples per tick = 40000 / Tempo
        public int Speed { get; set; } = 6;           // ticks per row
        public List<int> Order { get; set; } = [];
        public Dictionary<int, SongPattern> Patterns { get; set; } = [];
    }

    public static class AudioTables
    {
        public const int SfxTable = 0xE15B0, SfxCount = 64;
        public const int MusicTable = 0x1D4B2C, MusicCount = 44, MusicEntrySize = 20;
        public const int BankCount = 101, MaxInstruments = 255;
        public const int NoteCount = 104, NativeNote = 60;

        private static uint U32(ROM r, int a) => BitConverter.ToUInt32(r.ReadBytesAt(a, 4));
        private static int U16(ROM r, int a) => BitConverter.ToUInt16(r.ReadBytesAt(a, 2));
        private static bool IsRomPtr(uint v, ROM r) => v is >= 0x08000000 and < 0x0A000000 && (int)(v & 0x00FFFFFF) < r.Length;
        private static int Off(uint v) => (int)(v & 0x00FFFFFF);

        /// <summary>True when the ROM has the sound tables where the US ROM has them.</summary>
        public static bool IsSupported(ROM rom)
        {
            if (rom.Length < 0x800000) return false;
            for (int i = 0; i < 4; i++)
                if (!IsRomPtr(U32(rom, MusicTable + i * MusicEntrySize), rom) || !IsRomPtr(U32(rom, SfxTable + i * 12), rom)) return false;
            return true;
        }

        // ---- SFX ----------------------------------------------------------------------------------------------
        public static SfxSample ReadSfx(ROM rom, int i)
        {
            int a = SfxTable + i * 12;
            return new SfxSample(i, a, Off(U32(rom, a)), U32(rom, a + 4) != 0, U16(rom, a + 8), U16(rom, a + 10));
        }

        public static byte[] ReadPcm(ROM rom, int data, int length) => length <= 0 ? [] : rom.ReadBytesAt(data, Math.Min(length, rom.Length - data));

        /// <summary>Points an SFX at new PCM (stored at the end of the ROM). Length is limited to 65535 samples by the table's u16.</summary>
        public static void WriteSfx(ROM rom, int i, byte[] pcm, int rate, bool? loop = null)
        {
            if (pcm.Length is 0 or > 65535) throw new InvalidOperationException("An SFX must be 1-65535 samples (after resampling).");
            int a = SfxTable + i * 12, data = rom.AllocateFreeSpace(pcm.Length);
            rom.WriteBytesAt(data, pcm);
            rom.PatchInt32(a, (int)(0x08000000u | (uint)data));
            if (loop != null) rom.PatchInt32(a + 4, loop.Value ? 1 : 0);
            rom.PatchInt16(a + 8, (short)pcm.Length);
            rom.PatchInt16(a + 10, (short)rate);
        }

        // ---- songs / instruments ------------------------------------------------------------------------------
        public sealed record TrackHeader(int Index, int Address, int PatternTable, int OrderList, int Bank, int Presets, int Tempo, int Speed);

        public static TrackHeader ReadTrack(ROM rom, int i)
        {
            int a = MusicTable + i * MusicEntrySize;
            var b = rom.ReadBytesAt(a, MusicEntrySize);
            return new TrackHeader(i, a, Off(BitConverter.ToUInt32(b, 0)), Off(BitConverter.ToUInt32(b, 4)), Off(BitConverter.ToUInt32(b, 8)), Off(BitConverter.ToUInt32(b, 12)), b[16], b[17]);
        }

        public static int BankAddress(ROM rom) => ReadTrack(rom, 0).Bank;

        public static InstrumentSample ReadInstrument(ROM rom, int bank, int i)
        {
            int a = bank + i * 12;
            return new InstrumentSample(i, a, Off(U32(rom, a)), U16(rom, a + 4), U16(rom, a + 6), U16(rom, a + 8), U16(rom, a + 10));
        }

        /// <summary>The number of instruments a song's bank has (a song that got its own bank stores it after the shared 101).</summary>
        public static int BankSize(ROM rom, int bank)
        {
            int n = 0;
            while (n < MaxInstruments && bank + (n + 1) * 12 <= rom.Length)
            {
                uint p = U32(rom, bank + n * 12);
                int rate = U16(rom, bank + n * 12 + 10);
                if (!IsRomPtr(p, rom) || rate is < 1000 or > 60000) break;
                n++;
            }
            return n;
        }

        public static void WriteInstrument(ROM rom, int bank, int i, byte[] pcm, int mode, int loopStart, int rate)
        {
            if (pcm.Length is 0 or > 65535) throw new InvalidOperationException("An instrument sample must be 1-65535 samples.");
            int data = rom.AllocateFreeSpace(pcm.Length);
            rom.WriteBytesAt(data, pcm);
            var e = new byte[12];
            BitConverter.GetBytes(0x08000000u | (uint)data).CopyTo(e, 0);
            BitConverter.GetBytes((ushort)mode).CopyTo(e, 4);
            BitConverter.GetBytes((ushort)Math.Min(loopStart, pcm.Length)).CopyTo(e, 6);
            BitConverter.GetBytes((ushort)pcm.Length).CopyTo(e, 8);
            BitConverter.GetBytes((ushort)rate).CopyTo(e, 10);
            rom.WriteBytesAt(bank + i * 12, e);
        }

        public static byte[] ReadPresets(ROM rom, TrackHeader t) => rom.ReadBytesAt(t.Presets, 14);

        public static Song ReadSong(ROM rom, int index)
        {
            var t = ReadTrack(rom, index);
            var presets = ReadPresets(rom, t);
            var song = new Song { Tempo = t.Tempo, Speed = t.Speed };
            for (int o = t.OrderList; song.Order.Count < 512; o++)
            {
                int id = rom.ReadBytesAt(o, 1)[0];
                if (id == 255) break;
                song.Order.Add(id);
            }
            foreach (int id in song.Order.Distinct())
                song.Patterns[id] = ReadPattern(rom, Off(U32(rom, t.PatternTable + id * 4)), presets);
            return song;
        }

        private static SongPattern ReadPattern(ROM rom, int address, byte[] presets)
        {
            var p = new SongPattern();
            var last = new int[16];
            int o = address;
            int rows = rom.ReadBytesAt(o++, 1)[0];
            byte Next() => rom.ReadBytesAt(o++, 1)[0];
            for (int r = 0; r < rows; r++)
            {
                var row = new List<SongEvent>();
                while (true)
                {
                    int b = Next();
                    if (b == 0) break;
                    int ch = b & 15, hi = b >> 4, flags;
                    if (ch == 0) throw new InvalidDataException($"Bad pattern event at 0x{o - 1:X}.");
                    if (hi == 0) flags = last[ch] = Next();
                    else if (hi == 1) flags = last[ch];
                    else flags = presets[hi - 2];
                    var e = new SongEvent { Channel = ch, Flags = flags };
                    if ((flags & 1) != 0) e.Note = Next();
                    if ((flags & 2) != 0) e.Instrument = Next();
                    if ((flags & 4) != 0) e.Volume = Next();
                    if ((flags & 8) != 0) { e.Effect = Next(); e.EffectParam = Next(); }
                    row.Add(e);
                }
                p.Rows.Add(row);
            }
            return p;
        }

        /// <summary>
        /// Serialises a song to the end of the ROM and repoints track <paramref name="index"/> at it. Events are written with explicit flag bytes
        /// or one of the track's 14 presets; the "reuse last flags" form is never used because it depends on the play path.
        /// </summary>
        public static void WriteSong(ROM rom, int index, Song song, int? bankAddress = null)
        {
            var t = ReadTrack(rom, index);
            var presets = ReadPresets(rom, t);
            if (song.Order.Count == 0) throw new InvalidOperationException("The song has no patterns.");
            int maxId = song.Order.Max();
            var ptrs = new uint[maxId + 1];
            foreach (int id in song.Order.Distinct())
            {
                var blob = SerialisePattern(song.Patterns[id], presets);
                int at = rom.AllocateFreeSpace(blob.Length);
                rom.WriteBytesAt(at, blob);
                ptrs[id] = 0x08000000u | (uint)at;
            }
            var table = new byte[ptrs.Length * 4];
            for (int i = 0; i < ptrs.Length; i++) BitConverter.GetBytes(ptrs[i]).CopyTo(table, i * 4);
            int tableAt = rom.AllocateFreeSpace(table.Length);
            rom.WriteBytesAt(tableAt, table);
            var order = song.Order.Select(x => (byte)x).Append((byte)255).ToArray();
            int orderAt = rom.AllocateFreeSpace(order.Length);
            rom.WriteBytesAt(orderAt, order);

            rom.PatchInt32(t.Address, (int)(0x08000000u | (uint)tableAt));
            rom.PatchInt32(t.Address + 4, (int)(0x08000000u | (uint)orderAt));
            if (bankAddress != null) rom.PatchInt32(t.Address + 8, (int)(0x08000000u | (uint)bankAddress.Value));
            rom.PatchByte(t.Address + 16, (byte)Math.Clamp(song.Tempo, 1, 255));
            rom.PatchByte(t.Address + 17, (byte)Math.Clamp(song.Speed, 1, 255));
        }

        private static byte[] SerialisePattern(SongPattern p, byte[] presets)
        {
            if (p.Rows.Count is < 1 or > 255) throw new InvalidOperationException("A pattern must have 1-255 rows.");
            var o = new List<byte> { (byte)p.Rows.Count };
            foreach (var row in p.Rows)
            {
                foreach (var e in row)
                {
                    int f = (e.Note != null ? 1 : 0) | (e.Instrument != null ? 2 : 0) | (e.Volume != null ? 4 : 0) | (e.Effect != null ? 8 : 0);
                    f |= e.Flags & 0xF0;                       // behaviour bits (legato etc.) survive a JSON round trip; XM import leaves them 0
                    int preset = Array.IndexOf(presets, (byte)f);
                    if (f == 0 || e.Channel is < 1 or > 15) continue;
                    if (preset >= 0) o.Add((byte)((preset + 2) << 4 | e.Channel));
                    else { o.Add((byte)e.Channel); o.Add((byte)f); }
                    if (e.Note != null) o.Add((byte)e.Note.Value);
                    if (e.Instrument != null) o.Add((byte)e.Instrument.Value);
                    if (e.Volume != null) o.Add((byte)e.Volume.Value);
                    if (e.Effect != null) { o.Add((byte)e.Effect.Value); o.Add((byte)e.EffectParam); }
                }
                o.Add(0);
            }
            return [.. o];
        }

        // ---- lossless JSON ------------------------------------------------------------------------------------
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
        public static string SongToJson(Song s) => JsonSerializer.Serialize(s, Json);
        public static Song SongFromJson(string json) => JsonSerializer.Deserialize<Song>(json, Json) ?? throw new InvalidDataException("Not a song file.");
    }

    /// <summary>8-bit signed PCM <-> WAV, plus the resampling the tables' u16 limits force.</summary>
    public static class Wav
    {
        public static byte[] Write(byte[] signed8, int rate)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write("RIFF"u8.ToArray()); w.Write(36 + signed8.Length); w.Write("WAVE"u8.ToArray());
            w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate); w.Write((short)1); w.Write((short)8);
            w.Write("data"u8.ToArray()); w.Write(signed8.Length);
            foreach (byte b in signed8) w.Write((byte)(b + 128));
            return ms.ToArray();
        }

        /// <summary>Reads PCM WAV (8/16/24-bit, any channel count) and returns mono signed 8-bit at the file's rate.</summary>
        public static (byte[] Pcm, int Rate) Read(byte[] file)
        {
            if (file.Length < 44 || Encoding.ASCII.GetString(file, 0, 4) != "RIFF" || Encoding.ASCII.GetString(file, 8, 4) != "WAVE") throw new InvalidDataException("Not a WAV file.");
            int pos = 12, channels = 1, bits = 16, rate = 16000, fmtTag = 1;
            int dataAt = -1, dataLen = 0;
            while (pos + 8 <= file.Length)
            {
                string id = Encoding.ASCII.GetString(file, pos, 4);
                int len = BitConverter.ToInt32(file, pos + 4), body = pos + 8;
                if (id == "fmt ") { fmtTag = BitConverter.ToUInt16(file, body); channels = BitConverter.ToUInt16(file, body + 2); rate = BitConverter.ToInt32(file, body + 4); bits = BitConverter.ToUInt16(file, body + 14); }
                else if (id == "data") { dataAt = body; dataLen = Math.Min(len, file.Length - body); break; }
                pos = body + len + (len & 1);
            }
            if (dataAt < 0) throw new InvalidDataException("The WAV has no data chunk.");
            if (fmtTag is not (1 or 0xFFFE) || bits is not (8 or 16 or 24)) throw new InvalidDataException("Only 8/16/24-bit PCM WAV files are supported.");
            int step = bits / 8, frames = dataLen / (step * channels);
            var pcm = new byte[frames];
            for (int f = 0; f < frames; f++)
            {
                double sum = 0;
                for (int c = 0; c < channels; c++)
                {
                    int o = dataAt + (f * channels + c) * step;
                    sum += bits switch
                    {
                        8 => file[o] - 128,
                        16 => BitConverter.ToInt16(file, o) / 256.0,
                        _ => Sign24(file[o + 2] << 16 | file[o + 1] << 8 | file[o]) / 65536.0,
                    };
                }
                pcm[f] = (byte)(sbyte)Math.Clamp((int)Math.Round(sum / channels), -128, 127);
            }
            return (pcm, rate);
        }

        private static int Sign24(int v) => (v & 0x800000) != 0 ? v - (1 << 24) : v;

        /// <summary>Halves the sample rate (averaging pairs) until the data fits 65535 samples and the rate is at most <paramref name="maxRate"/>.</summary>
        public static (byte[] Pcm, int Rate) Fit(byte[] pcm, int rate, int maxRate = 32000)
        {
            while (pcm.Length > 65535 || rate > maxRate)
            {
                var half = new byte[(pcm.Length + 1) / 2];
                for (int i = 0; i < half.Length; i++)
                {
                    int a = (sbyte)pcm[2 * i], b = 2 * i + 1 < pcm.Length ? (sbyte)pcm[2 * i + 1] : a;
                    half[i] = (byte)(sbyte)((a + b) / 2);
                }
                pcm = half; rate = Math.Max(1, rate / 2);
            }
            return (pcm, Math.Max(rate, 1000));
        }
    }
}
