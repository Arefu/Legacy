using System.Text;
using DrGero.IO;

namespace DrGero.Engine
{
    /// <summary>
    /// Game song <-> FastTracker 2 ".xm" module (the 15-channel, variable-row, sampled-instrument model of this engine matches XM closely; MOD's
    /// 4 channels / 31 samples cannot hold these songs). What survives the trip: notes, note-off, instruments (samples, loops, ping-pong),
    /// volume, pan (8xx), tempo (Fxx, BPM), sample offset (9xx), retrigger (E9x), song speed/BPM. The game's envelope, vibrato, tremolo, slide
    /// and portamento effects have no faithful XM equivalent and are dropped on export -- use the JSON export for a lossless copy.
    /// Pitch: the game's note 60 plays an instrument at its own sample rate; XM note x = note + 1 + shift, with the instrument's relative
    /// note / finetune carrying the rate (see <see cref="RelativePitch"/>).
    /// </summary>
    public static class XmCodec
    {
        private const double XmC4Rate = 8363.0;

        /// <summary>Semitones (relative note + finetune/128) that make XM note x = game note + 1 + <paramref name="shift"/> sound like the game.</summary>
        private static double RelativePitch(int rate, int shift) => 12 * Math.Log2(rate / XmC4Rate) - 12 - shift;

        // ---- export -------------------------------------------------------------------------------------------
        public static byte[] Export(ROM rom, int trackIndex, Song song, out string report)
        {
            var track = AudioTables.ReadTrack(rom, trackIndex);
            int bankSize = AudioTables.BankSize(rom, track.Bank);
            var notes = song.Patterns.Values.SelectMany(p => p.Rows).SelectMany(r => r).Where(e => e.Note is int n && n != 255).Select(e => e.Note!.Value).ToList();
            int maxNote = notes.Count > 0 ? notes.Max() : 0, shift = maxNote > 95 ? 95 - maxNote : 0, dropped = 0, lostEffects = 0;
            int channels = Math.Max(2, song.Patterns.Values.SelectMany(p => p.Rows).SelectMany(r => r).Select(e => e.Channel).DefaultIfEmpty(2).Max());
            channels = channels + (channels & 1);
            if (song.Order.Count > 256) throw new InvalidOperationException("XM supports at most 256 order entries.");

            var ids = song.Order.Distinct().OrderBy(x => x).ToList();
            var xmIndex = ids.Select((id, i) => (id, i)).ToDictionary(t => t.id, t => t.i);
            var lastVol = new int[16]; Array.Fill(lastVol, 64);
            var lastNote = new int[16]; Array.Fill(lastNote, -1);
            var lastEff = new int[16]; var lastParam = new int[16];
            var used = new SortedSet<int>();
            var patternBytes = new Dictionary<int, byte[]>();
            foreach (int id in song.Order.Where(id => !patternBytes.ContainsKey(id)))
            {
                var p = song.Patterns[id];
                var o = new MemoryStream();
                foreach (var row in p.Rows)
                {
                    var cells = new byte[channels][];
                    foreach (var e in row.Where(e => e.Channel >= 1 && e.Channel <= channels))
                    {
                        int note = 0, inst = 0, vol = 0, fx = 0, param = 0, ch = e.Channel;
                        // "0x10 / 0x40 / 0x80" flags re-trigger / re-apply the channel's STORED note / volume / effect (Sequencer_ParseRow), so
                        // resolve them against what earlier events on this channel set, in play order.
                        bool trigger = e.Note != null || (e.Flags & 0x11) != 0;
                        int? noteValue = e.Note ?? (trigger && lastNote[ch] >= 0 ? lastNote[ch] : null);
                        if (e.Note != null) lastNote[ch] = e.Note.Value;
                        if (noteValue is int n)
                        {
                            if (n == 255) note = 97;
                            else if (n + 1 + shift is >= 1 and <= 96) note = n + 1 + shift;
                            else { dropped++; trigger = false; }
                        }
                        if (e.Instrument is int ins) { inst = ins + 1; used.Add(ins); }
                        if (e.Volume is int v) { lastVol[ch] = Math.Clamp(v, 0, 64); vol = 0x10 + lastVol[ch]; }
                        else if ((trigger && note != 97) || (e.Flags & 0x40) != 0) vol = 0x10 + lastVol[ch];
                        int? ef = e.Effect ?? ((e.Flags & 0x80) != 0 ? lastEff[ch] : null);
                        int efParam = e.Effect != null ? e.EffectParam : lastParam[ch];
                        if (e.Effect != null) { lastEff[ch] = e.Effect.Value; lastParam[ch] = e.EffectParam; }
                        if (ef is int effect)
                        {
                            switch (effect)
                            {
                                case 1: fx = 0x0F; param = Math.Clamp(efParam, 1, 31); break;   // set speed (ticks per row)
                                case 27: fx = 0x0E; param = 0xD0 | (efParam & 0xF); break;   // note delay = XM EDx
                                case 24: fx = 0x08; param = Math.Clamp(128 + (sbyte)efParam * 2, 0, 255); break;
                                case 20: fx = 0x0F; param = Math.Clamp(efParam, 32, 255); break;
                                case 15: fx = 0x09; param = efParam & 0xFF; break;
                                case 17: if ((efParam & 0xF) != 0) { fx = 0x0E; param = 0x90 | (efParam & 0xF); } break;   // retrigger every N ticks = XM E9x
                                case 0 or 3 or 9 or 10 or 13 or 14 or 16: break;   // plain note-on variants: nothing to carry
                                default: lostEffects++; break;
                            }
                        }
                        cells[e.Channel - 1] = PackCell(note, inst, vol, fx, param);
                    }
                    for (int c = 0; c < channels; c++) { var cell = cells[c]; if (cell == null) o.WriteByte(0x80); else o.Write(cell); }
                }
                patternBytes[id] = o.ToArray();
            }

            int lastInst = used.Count > 0 ? used.Max() : -1;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.Latin1);
            w.Write(Encoding.ASCII.GetBytes("Extended Module: "));
            w.Write(Pad($"LoG2 t{trackIndex} s{shift}", 20));   // "s<shift>" is read back by Import so an exported song returns to its original pitch mapping
            w.Write((byte)0x1A); w.Write(Pad("DBZKit", 20)); w.Write((ushort)0x0104);
            w.Write(276); w.Write((ushort)song.Order.Count); w.Write((ushort)0); w.Write((ushort)channels); w.Write((ushort)ids.Count); w.Write((ushort)(lastInst + 1));
            w.Write((ushort)1); w.Write((ushort)Math.Clamp(song.Speed, 1, 31)); w.Write((ushort)Math.Clamp(song.Tempo, 32, 255));
            var order = new byte[256];
            for (int i = 0; i < song.Order.Count; i++) order[i] = (byte)xmIndex[song.Order[i]];
            w.Write(order);
            foreach (int id in ids)
            {
                var data = patternBytes[id];
                w.Write(9); w.Write((byte)0); w.Write((ushort)song.Patterns[id].Rows.Count); w.Write((ushort)data.Length); w.Write(data);
            }
            for (int i = 0; i <= lastInst; i++)
            {
                if (!used.Contains(i) || i >= bankSize) { w.Write(29); w.Write(Pad("", 22)); w.Write((byte)0); w.Write((ushort)0); continue; }
                var ins = AudioTables.ReadInstrument(rom, track.Bank, i);
                var pcm = AudioTables.ReadPcm(rom, ins.Data, ins.Length);
                double rel = RelativePitch(ins.Rate, shift);
                int relNote = (int)Math.Clamp(Math.Round(rel), -96, 95), fine = (int)Math.Clamp(Math.Round((rel - relNote) * 128), -128, 127);
                bool loop = ins.Loops && ins.Length > ins.LoopStart;
                w.Write(263); w.Write(Pad($"GBA {i:D3}", 22)); w.Write((byte)0); w.Write((ushort)1);
                w.Write(40); w.Write(new byte[96]); w.Write(new byte[96]); w.Write(new byte[10]);   // sample map (96), envelopes (48+48), then 10 bytes of envelope point counts / sustain / loop / type
                w.Write(new byte[4]); w.Write((ushort)0); w.Write(new byte[22]);   // vibrato, fadeout, reserved
                w.Write(pcm.Length); w.Write(loop ? ins.LoopStart : 0); w.Write(loop ? ins.Length - ins.LoopStart : 0);
                w.Write((byte)64); w.Write((sbyte)fine); w.Write((byte)(loop ? (ins.PingPong ? 2 : 1) : 0)); w.Write((byte)128); w.Write((sbyte)relNote); w.Write((byte)0);
                w.Write(Pad($"GBA {i:D3}", 22));
                sbyte prev = 0;
                foreach (byte b in pcm) { sbyte cur = (sbyte)b; w.Write((byte)(sbyte)(cur - prev)); prev = cur; }
            }
            report = $"{song.Order.Count} orders, {ids.Count} patterns, {channels} channels, {used.Count} instruments" +
                     (shift != 0 ? $"; notes shifted down {-shift} semitones to fit XM's 96-note range" : "") +
                     (dropped > 0 ? $"; {dropped} notes outside XM's range were dropped" : "") +
                     (lostEffects > 0 ? $"; {lostEffects} envelope/vibrato/slide/portamento effects have no XM equivalent and were dropped (use JSON for a lossless copy)" : "");
            return ms.ToArray();
        }

        private static byte[] Pad(string s, int n) { var b = new byte[n]; Encoding.Latin1.GetBytes(s.Length > n ? s[..n] : s).CopyTo(b, 0); return b; }

        private static byte[] PackCell(int note, int inst, int vol, int fx, int param)
        {
            int mask = (note != 0 ? 1 : 0) | (inst != 0 ? 2 : 0) | (vol != 0 ? 4 : 0) | (fx != 0 || param != 0 ? 8 | 16 : 0);
            var b = new List<byte> { (byte)(0x80 | mask) };
            if (note != 0) b.Add((byte)note);
            if (inst != 0) b.Add((byte)inst);
            if (vol != 0) b.Add((byte)vol);
            if (mask >= 8) { b.Add((byte)fx); b.Add((byte)param); }
            return [.. b];
        }

        // ---- import -------------------------------------------------------------------------------------------
        private sealed record XmSample(byte[] Pcm, int Loop, int LoopStart, int LoopLength, int Volume, int Rate, string Name);

        /// <summary>Replaces a song with an XM module. New instruments go in a private copy of the bank so other songs keep theirs.</summary>
        public static string Import(ROM rom, int trackIndex, byte[] xm)
        {
            if (xm.Length < 336 || Encoding.ASCII.GetString(xm, 0, 17) != "Extended Module: ") throw new InvalidDataException("Not an XM module.");
            int shift = 0;
            var nameMatch = System.Text.RegularExpressions.Regex.Match(Encoding.Latin1.GetString(xm, 17, 20), @"^LoG2 t\d+ s(-?\d+)");
            if (nameMatch.Success) shift = int.Parse(nameMatch.Groups[1].Value);
            int headerAt = 60, headerSize = BitConverter.ToInt32(xm, headerAt);
            int songLength = BitConverter.ToUInt16(xm, 64), channels = BitConverter.ToUInt16(xm, 68), patternCount = BitConverter.ToUInt16(xm, 70), instCount = BitConverter.ToUInt16(xm, 72);
            int speed = BitConverter.ToUInt16(xm, 76), bpm = BitConverter.ToUInt16(xm, 78);
            var warnings = new List<string>();
            var pos = headerAt + headerSize;

            var song = new Song { Speed = Math.Clamp(speed, 1, 31), Tempo = Math.Clamp(bpm, 32, 255) };
            for (int i = 0; i < songLength; i++) song.Order.Add(xm[80 + i]);
            if (BitConverter.ToUInt16(xm, 66) != 0) warnings.Add("restart position ignored (songs loop to the first order)");

            var events = new List<(int Pattern, int Row, SongEvent E, int Inst)>();
            var patterns = new List<SongPattern>();
            for (int pi = 0; pi < patternCount; pi++)
            {
                int len = BitConverter.ToInt32(xm, pos), rows = BitConverter.ToUInt16(xm, pos + 5), size = BitConverter.ToUInt16(xm, pos + 7);
                if (rows > 255) throw new InvalidDataException($"Pattern {pi} has {rows} rows; the game allows at most 255.");
                var p = new SongPattern();
                for (int r = 0; r < rows; r++) p.Rows.Add([]);
                int d = pos + len, end = d + size;
                for (int r = 0; r < rows && size > 0; r++)
                    for (int c = 0; c < channels; c++)
                    {
                        int b = xm[d++], note = 0, inst = 0, vol = 0, fx = 0, param = 0;
                        if ((b & 0x80) != 0)
                        {
                            if ((b & 1) != 0) note = xm[d++];
                            if ((b & 2) != 0) inst = xm[d++];
                            if ((b & 4) != 0) vol = xm[d++];
                            if ((b & 8) != 0) fx = xm[d++];
                            if ((b & 16) != 0) param = xm[d++];
                        }
                        else { note = b; inst = xm[d++]; vol = xm[d++]; fx = xm[d++]; param = xm[d++]; }
                        if (c >= 15) { if (note != 0 || inst != 0) warnings.Add("channels above 15 dropped"); continue; }
                        var e = new SongEvent { Channel = c + 1 };
                        if (note is >= 1 and <= 96) e.Note = note - 1 - shift; else if (note == 97) e.Note = 255;
                        if (vol is >= 0x10 and <= 0x50) e.Volume = vol - 0x10;
                        switch (fx)
                        {
                            case 0x08: e.Effect = 24; e.EffectParam = (byte)(sbyte)Math.Clamp((param - 128) / 2, -64, 63); break;
                            case 0x0F when param >= 32: e.Effect = 20; e.EffectParam = param; break;
                            case 0x0F when param >= 1: e.Effect = 1; e.EffectParam = param; break;   // < 32: set speed
                            case 0x0F: break;
                            case 0x09: e.Effect = 15; e.EffectParam = param; break;
                            case 0x0E when (param >> 4) == 9 && (param & 0xF) != 0: e.Effect = 17; e.EffectParam = param & 0xF; break;
                            case 0x0E when (param >> 4) == 0xD: e.Effect = 27; e.EffectParam = param & 0xF; break;
                            case 0x0C: e.Volume = Math.Clamp(param, 0, 64); break;
                            case 0x00: break;
                            default: warnings.Add($"effect {fx:X}xx dropped"); break;
                        }
                        if (e.Note == null && e.Volume == null && e.Effect == null && inst == 0) continue;
                        if (inst != 0) events.Add((pi, r, e, inst));
                        p.Rows[r].Add(e);
                    }
                patterns.Add(p);
                pos = end;
            }

            // instruments
            var samples = new List<XmSample?>();
            for (int i = 0; i < instCount; i++)
            {
                int size = BitConverter.ToInt32(xm, pos), n = BitConverter.ToUInt16(xm, pos + 27);
                if (n == 0) { samples.Add(null); pos += size; continue; }
                int shSize = BitConverter.ToInt32(xm, pos + 29), sh = pos + size;
                var heads = new List<(int Len, int LoopStart, int LoopLen, int Vol, int Fine, int Type, int Rel, string Name)>();
                for (int s = 0; s < n; s++)
                {
                    int a = sh + s * shSize;
                    heads.Add((BitConverter.ToInt32(xm, a), BitConverter.ToInt32(xm, a + 4), BitConverter.ToInt32(xm, a + 8), xm[a + 12], (sbyte)xm[a + 13], xm[a + 14], (sbyte)xm[a + 16], Encoding.Latin1.GetString(xm, a + 18, 22).TrimEnd('\0')));
                }
                int dat = sh + n * shSize;
                XmSample? first = null;
                for (int s = 0; s < n; s++)
                {
                    var h = heads[s];
                    bool wide = (h.Type & 0x10) != 0;
                    int count = wide ? h.Len / 2 : h.Len;
                    var pcm = new byte[count];
                    int acc = 0;
                    for (int k = 0; k < count; k++)
                    {
                        if (wide) { acc += BitConverter.ToInt16(xm, dat + k * 2); pcm[k] = (byte)(sbyte)Math.Clamp((short)acc >> 8, -128, 127); }
                        else { acc = (sbyte)(acc + (sbyte)xm[dat + k]); pcm[k] = (byte)acc; }
                    }
                    dat += h.Len;
                    if (s == 0)
                    {
                        int ls = wide ? h.LoopStart / 2 : h.LoopStart, ll = wide ? h.LoopLen / 2 : h.LoopLen;
                        double total = h.Rel + h.Fine / 128.0;
                        int rate = (int)Math.Round(XmC4Rate * Math.Pow(2, (total + 12 + shift) / 12.0));
                        first = new XmSample(pcm, h.Type & 3, ls, ll, h.Vol, rate, h.Name);
                    }
                }
                if (n > 1) warnings.Add($"instrument {i + 1}: only its first sample is used");
                samples.Add(first);
                pos = dat;
            }

            // build the bank: keep the song's existing instruments, add new ones (reusing identical stock ones)
            var track = AudioTables.ReadTrack(rom, trackIndex);
            int existing = AudioTables.BankSize(rom, track.Bank);
            var bank = new List<byte[]>();
            for (int i = 0; i < existing; i++) bank.Add(rom.ReadBytesAt(track.Bank + i * 12, 12));
            var map = new int[instCount + 1];
            for (int i = 0; i < instCount; i++)
            {
                var s = samples[i];
                if (s == null || s.Pcm.Length == 0) { map[i + 1] = 0; continue; }
                var (pcm, rate) = Wav.Fit(s.Pcm, s.Rate);
                int halvings = 0; for (int x = s.Pcm.Length, y = pcm.Length; x > y && halvings < 8; x = (x + 1) / 2) halvings++;
                int mode = s.Loop == 0 || s.LoopLength <= 1 ? 0 : (s.Loop == 2 ? 3 : 1);
                int loopStart = 0;
                if (mode != 0)
                {
                    loopStart = s.LoopStart >> halvings;
                    int end = (s.LoopStart + s.LoopLength) >> halvings;
                    if (end < pcm.Length) pcm = pcm[..Math.Max(end, 1)];
                }
                int found = -1;
                for (int b = 0; b < existing && found < 0; b++)
                {
                    var ins = AudioTables.ReadInstrument(rom, track.Bank, b);
                    if (ins.Length == pcm.Length && Math.Abs(ins.Rate - rate) <= ins.Rate / 200 + 2 && ins.Mode == mode && (mode == 0 || ins.LoopStart == loopStart) && AudioTables.ReadPcm(rom, ins.Data, ins.Length).AsSpan().SequenceEqual(pcm)) found = b;
                }
                if (found < 0)
                {
                    if (bank.Count >= AudioTables.MaxInstruments) throw new InvalidOperationException("Too many instruments: the game addresses at most 255.");
                    int data = rom.AllocateFreeSpace(pcm.Length);
                    rom.WriteBytesAt(data, pcm);
                    var e = new byte[12];
                    BitConverter.GetBytes(0x08000000u | (uint)data).CopyTo(e, 0);
                    BitConverter.GetBytes((ushort)mode).CopyTo(e, 4);
                    BitConverter.GetBytes((ushort)Math.Min(loopStart, pcm.Length)).CopyTo(e, 6);
                    BitConverter.GetBytes((ushort)pcm.Length).CopyTo(e, 8);
                    BitConverter.GetBytes((ushort)rate).CopyTo(e, 10);
                    bank.Add(e);
                    found = bank.Count - 1;
                }
                map[i + 1] = found;
            }
            foreach (var (_, _, e, inst) in events)
            {
                e.Instrument = map[inst];
                if (e.Note is int n && n != 255 && e.Volume == null) e.Volume = Math.Clamp(samples[inst - 1]?.Volume ?? 64, 0, 64);
            }

            int? bankAddress = null;
            if (bank.Count > existing)
            {
                var raw = bank.SelectMany(b => b).ToArray();
                bankAddress = rom.AllocateFreeSpace(raw.Length);
                rom.WriteBytesAt(bankAddress.Value, raw);
            }
            for (int i = 0; i < patterns.Count; i++) song.Patterns[i] = patterns[i];
            AudioTables.WriteSong(rom, trackIndex, song, bankAddress);
            return $"Imported {song.Order.Count} orders, {patterns.Count} patterns, {instCount} instruments ({bank.Count - existing} new)." +
                   (warnings.Count > 0 ? " Notes: " + string.Join("; ", warnings.Distinct().Take(6)) + "." : "");
        }
    }
}
