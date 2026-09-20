// STATUS (2026-09-20): complete. Sound tab with player (play/pause/stop/seek/volume/loop) over winmm, songs rendered by DrGero/AudioRender.cs,
// SFX / instrument WAV import + export, song XM / JSON import + export. The renderer's slide / vibrato / envelope handling is transcribed from
// the game's code but has not been compared with real hardware output; Audio-Notes.md has the formats and the IDA state.
using DrGero.Engine;
using DrGero.IO;

namespace DBZKit
{
    /// <summary>
    /// Sound tab: sound effects, songs (BGM) and the shared instrument bank, with a player (play / pause / stop / seek / volume / loop).
    /// Songs are rendered by DrGero's re-implementation of the game's own sequencer + mixer (SongRenderer) and played through winmm.
    /// SFX / instruments: export WAV, replace from WAV. Songs: export/import XM (tracker module) or JSON (lossless), export the rendered WAV.
    /// </summary>
    internal sealed class AudioPanel : UserControl
    {
        private enum Kind { Sfx, Song, Instrument }

        private readonly EditSession _session;
        private ROM? _rom => _session.Rom;

        private readonly Label _noRom = new() { Text = "Open a ROM (File > Open Rom) to use the sound editor.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        private readonly Panel _content = new() { Dock = DockStyle.Fill, Visible = false };
        private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, Font = new Font("Consolas", 9.5f) };
        private readonly Label _info = new() { Dock = DockStyle.Top, Height = 84, Padding = new Padding(6) };
        private readonly FlowLayoutPanel _actions = new() { Dock = DockStyle.Top, Height = 74, Padding = new Padding(4) };
        private readonly Button _exportWav = Btn("Export WAV..."), _importWav = Btn("Replace from WAV..."),
                                _exportXm = Btn("Export XM..."), _importXm = Btn("Import XM..."), _exportJson = Btn("Export JSON..."), _importJson = Btn("Import JSON..."),
                                _exportRender = Btn("Export played WAV...");
        private readonly Button _play = Btn("Play"), _stop = Btn("Stop");
        private readonly CheckBox _loop = new() { Text = "Loop", AutoSize = true, Margin = new Padding(8, 8, 0, 0) };
        private readonly TrackBar _progress = new() { Minimum = 0, Maximum = 1000, TickStyle = TickStyle.None, Dock = DockStyle.Fill, SmallChange = 5, LargeChange = 50 };
        private readonly Label _time = new() { AutoSize = true, Text = "0:00 / 0:00", Margin = new Padding(8, 8, 0, 0), MinimumSize = new Size(190, 0) };
        private readonly TrackBar _volume = new() { Minimum = 0, Maximum = 100, Value = 80, TickStyle = TickStyle.None, Width = 150, SmallChange = 5, LargeChange = 10 };
        private readonly Label _volumeLabel = new() { AutoSize = true, Text = "80%", Margin = new Padding(4, 8, 0, 0) };
        private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(6), ForeColor = Color.DimGray };
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 80 };

        private readonly WaveOutPlayer _player = new();
        private readonly Dictionary<(Kind, int), RenderedSong> _rendered = [];
        private (Kind Kind, int Index)? _loaded;    // what the player's buffer holds
        private RenderedSong? _loadedSong;
        private bool _filling, _seeking, _wasPlaying, _busy;

        private static Button Btn(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3) };

        public AudioPanel(EditSession session)
        {
            _session = session;
            Dock = DockStyle.Fill;

            _kind.Items.AddRange(["Sound effects", "Songs (BGM)", "Instruments (shared bank)"]);
            _kind.SelectedIndex = 1;
            _kind.SelectedIndexChanged += (_, _) => Fill();
            _list.SelectedIndexChanged += (_, _) => { if (!_filling) SelectionChanged(); };
            _list.DoubleClick += (_, _) => Guard(TogglePlay);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(4) };
            top.Controls.AddRange([new Label { Text = "Show:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) }, _kind]);
            var left = new Panel { Dock = DockStyle.Left, Width = 420 };
            left.Controls.Add(_list);
            left.Controls.Add(top);

            _actions.Controls.AddRange([_exportWav, _importWav, _exportRender, _exportXm, _importXm, _exportJson, _importJson]);

            var transport = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, ColumnCount = 1, RowCount = 3, Padding = new Padding(4) };
            transport.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            transport.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            transport.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            buttons.Controls.AddRange([_play, _stop, _loop, _time, new Label { Text = "Volume", AutoSize = true, Margin = new Padding(16, 8, 0, 0) }, _volume, _volumeLabel]);
            transport.Controls.Add(buttons, 0, 0);
            transport.Controls.Add(_progress, 0, 1);

            var right = new Panel { Dock = DockStyle.Fill };
            right.Controls.Add(_status);
            right.Controls.Add(transport);
            right.Controls.Add(_actions);
            right.Controls.Add(_info);

            _content.Controls.Add(right);
            _content.Controls.Add(left);
            _content.Controls.Add(new ApplyBar(session));
            Controls.Add(_content);
            Controls.Add(_noRom);

            _play.Click += (_, _) => Guard(TogglePlay);
            _stop.Click += (_, _) => StopPlayback();
            _volume.ValueChanged += (_, _) => { _player.Volume = _volume.Value / 100.0; _volumeLabel.Text = _volume.Value + "%"; };
            _progress.MouseDown += (_, _) => _seeking = true;
            _progress.MouseUp += (_, _) => { _seeking = false; Seek(); };
            _progress.KeyUp += (_, _) => Seek();
            _timer.Tick += (_, _) => Tick();
            _exportWav.Click += (_, _) => Guard(ExportWav);
            _importWav.Click += (_, _) => Guard(ImportWav);
            _exportRender.Click += (_, _) => Guard(ExportRendered);
            _exportXm.Click += (_, _) => Guard(ExportXm);
            _importXm.Click += (_, _) => Guard(ImportXm);
            _exportJson.Click += (_, _) => Guard(ExportJson);
            _importJson.Click += (_, _) => Guard(ImportJson);

            session.Loaded += OnLoaded;
            session.Changed += source => { if (!ReferenceEquals(source, this)) { _rendered.Clear(); StopPlayback(); _loaded = null; Fill(); } };
            _player.Volume = 0.8;
            _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Dispose(); _player.Dispose(); }
            base.Dispose(disposing);
        }

        private Kind Current => (Kind)Math.Max(0, _kind.SelectedIndex);
        private int Selected => _list.SelectedIndex;

        private void OnLoaded()
        {
            StopPlayback();
            _rendered.Clear();
            _loaded = null;
            bool ok = _rom != null && AudioTables.IsSupported(_rom);
            _content.Visible = ok;
            _noRom.Visible = !ok;
            if (_rom != null && !ok) _noRom.Text = "This ROM doesn't have the US sound tables where expected, so the sound editor is disabled.";
            if (ok) Fill();
        }

        private void Fill()
        {
            var rom = _rom;
            if (rom == null || !AudioTables.IsSupported(rom)) return;
            int keep = Selected;
            _filling = true;
            _list.BeginUpdate();
            _list.Items.Clear();
            switch (Current)
            {
                case Kind.Sfx:
                    for (int i = 0; i < AudioTables.SfxCount; i++)
                    {
                        var s = AudioTables.ReadSfx(rom, i);
                        _list.Items.Add($"SFX {i,2}   {s.Length,6} smp  {s.Rate,5} Hz  {(double)s.Length / Math.Max(1, s.Rate),5:0.00}s{(s.Loop ? "  loop" : "")}");
                    }
                    break;
                case Kind.Song:
                    for (int i = 0; i < AudioTables.MusicCount; i++)
                    {
                        var t = AudioTables.ReadTrack(rom, i);
                        _list.Items.Add($"Song {i,2}   tempo {t.Tempo,3}  speed {t.Speed}");
                    }
                    break;
                default:
                    int bank = AudioTables.BankAddress(rom);
                    for (int i = 0; i < AudioTables.BankCount; i++)
                    {
                        var s = AudioTables.ReadInstrument(rom, bank, i);
                        _list.Items.Add($"Inst {i,3}  {s.Length,6} smp  {s.Rate,5} Hz{(s.Loops ? (s.PingPong ? "  ping-pong" : "  loop") : "")}");
                    }
                    break;
            }
            _list.EndUpdate();
            _filling = false;
            if (_list.Items.Count > 0) _list.SelectedIndex = Math.Clamp(keep, 0, _list.Items.Count - 1);
            SelectionChanged();
        }

        private void SelectionChanged()
        {
            StopPlayback();
            var k = Current;
            bool ok = _rom != null && Selected >= 0;
            _exportWav.Visible = _importWav.Visible = ok && k != Kind.Song;
            _exportXm.Visible = _importXm.Visible = _exportJson.Visible = _importJson.Visible = _exportRender.Visible = ok && k == Kind.Song;
            _play.Enabled = _stop.Enabled = ok;
            if (!ok) { _info.Text = ""; return; }
            int i = Selected;
            switch (k)
            {
                case Kind.Sfx:
                    var s = AudioTables.ReadSfx(_rom!, i);
                    _info.Text = $"Sound effect {i}: {s.Length} samples at {s.Rate} Hz (signed 8-bit mono), data at file 0x{s.Data:X}.\r\n" +
                                 "Replace from WAV converts to mono 8-bit, keeps it under 65535 samples, and stores it at the end of the ROM.";
                    break;
                case Kind.Instrument:
                    var n = AudioTables.ReadInstrument(_rom!, AudioTables.BankAddress(_rom!), i);
                    _info.Text = $"Instrument {i}: {n.Length} samples at {n.Rate} Hz, loop start {n.LoopStart}, mode {n.Mode}. Note 60 plays it at its own rate.\r\n" +
                                 "This bank is shared by every song: replacing an instrument changes it in all songs that use it.";
                    break;
                default:
                    var t = AudioTables.ReadTrack(_rom!, i);
                    _info.Text = $"Song {i}: tempo {t.Tempo}, speed {t.Speed}, pattern table 0x{t.PatternTable:X}, order list 0x{t.OrderList:X}.\r\n" +
                                 "Play uses a re-implementation of the game's mixer (notes, volume, pan, tempo, loops, slides, vibrato, envelope). XM is editable in a tracker; JSON keeps every effect exactly.";
                    break;
            }
            SetIdleTime();
        }

        // ---- player ------------------------------------------------------------------------------------------------
        private static string Clock(double seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

        private void SetIdleTime() { _time.Text = "0:00 / 0:00"; _progress.Value = 0; _play.Text = "Play"; }

        private void StopPlayback()
        {
            _player.Stop();
            _wasPlaying = false;
            if (_progress.Value != 0) _progress.Value = 0;
            _play.Text = "Play";
            if (_loaded != null && _player.HasBuffer) _time.Text = $"0:00 / {Clock((double)_player.TotalFrames / _player.Rate)}";
        }

        private async void TogglePlay()
        {
            if (_busy || _rom == null || Selected < 0) return;
            var key = (Current, Selected);
            if (_loaded == key && _player.HasBuffer)
            {
                if (_player.IsPaused) { _player.Resume(); _play.Text = "Pause"; _wasPlaying = true; return; }
                if (_player.IsPlaying) { _player.Pause(); _play.Text = "Resume"; return; }
                StartFrom(_progress.Value / 1000.0);
                return;
            }
            _busy = true;
            try
            {
                _play.Enabled = false;
                if (key.Item1 == Kind.Song)
                {
                    if (!_rendered.TryGetValue(key, out var song))
                    {
                        Say("Rendering song...");
                        var rom = _rom; int idx = Selected;
                        song = await Task.Run(() => SongRenderer.Render(rom, idx, AudioTables.ReadSong(rom, idx)));
                        _rendered[key] = song;
                    }
                    _loadedSong = song;
                    _player.Load(song.Pcm, 2, song.Rate);
                    Say($"{Clock(song.Seconds)} long, {song.OrderStartFrames.Length} orders" + (song.Truncated ? " (stopped at 15 minutes)" : "") + ".");
                }
                else
                {
                    _loadedSong = null;
                    var (pcm, rate, _) = CurrentPcm();
                    _player.Load(pcm.Select(b => (short)((sbyte)b << 8)).ToArray(), 1, rate);
                    Say("");
                }
                _loaded = key;
                StartFrom(0);
            }
            finally { _busy = false; _play.Enabled = true; }
        }

        private void StartFrom(double fraction)
        {
            _player.Play((int)(fraction * _player.TotalFrames));
            _wasPlaying = true;
            _play.Text = "Pause";
        }

        private void Seek()
        {
            if (_loaded == null || !_player.HasBuffer) return;
            bool wasPaused = _player.IsPaused, playing = _player.IsPlaying || _wasPlaying;
            if (!playing) { UpdateTime(_progress.Value / 1000.0 * _player.TotalFrames); return; }
            _player.Play((int)(_progress.Value / 1000.0 * _player.TotalFrames));
            if (wasPaused) _player.Pause();
        }

        private void Tick()
        {
            if (_loaded == null || !_player.HasBuffer || !_wasPlaying) return;
            if (!_player.IsPlaying && !_player.IsPaused)
            {
                if (_loop.Checked && !_seeking) { _player.Play(0); return; }
                StopPlayback();
                return;
            }
            int frame = _player.PositionFrames;
            if (!_seeking) _progress.Value = Math.Clamp((int)(frame * 1000L / Math.Max(1, _player.TotalFrames)), 0, 1000);
            UpdateTime(frame);
        }

        private void UpdateTime(double frame)
        {
            double total = (double)_player.TotalFrames / _player.Rate;
            string order = "";
            if (_loadedSong is { OrderStartFrames.Length: > 0 } s)
            {
                int idx = Array.FindLastIndex(s.OrderStartFrames, f => f <= frame);
                order = $"   order {Math.Max(0, idx) + 1}/{s.OrderStartFrames.Length}";
            }
            _time.Text = $"{Clock(frame / _player.Rate)} / {Clock(total)}{order}";
        }

        // ---- editing -----------------------------------------------------------------------------------------------
        private void Guard(Action a)
        {
            try { a(); }
            catch (Exception ex) { _status.ForeColor = Color.Firebrick; _status.Text = ex.Message; }
        }

        private void Say(string text) { _status.ForeColor = Color.DimGray; _status.Text = text; }

        private (byte[] Pcm, int Rate, bool Loop) CurrentPcm()
        {
            var rom = _rom!;
            if (Current == Kind.Sfx) { var s = AudioTables.ReadSfx(rom, Selected); return (AudioTables.ReadPcm(rom, s.Data, s.Length), s.Rate, s.Loop); }
            var n = AudioTables.ReadInstrument(rom, AudioTables.BankAddress(rom), Selected);
            return (AudioTables.ReadPcm(rom, n.Data, n.Length), n.Rate, n.Loops);
        }

        private string Ask(bool save, string filter, string name)
        {
            if (save)
            {
                using var d = new SaveFileDialog { Filter = filter, FileName = name };
                return d.ShowDialog(this) == DialogResult.OK ? d.FileName : "";
            }
            using var o = new OpenFileDialog { Filter = filter };
            return o.ShowDialog(this) == DialogResult.OK ? o.FileName : "";
        }

        private string Stem => Current switch { Kind.Sfx => $"sfx_{Selected:D2}", Kind.Song => $"song_{Selected:D2}", _ => $"instrument_{Selected:D3}" };

        private void Edited(string message)
        {
            _rendered.Clear();
            _loaded = null;
            int keep = Selected;
            Fill();
            _list.SelectedIndex = keep;
            _session.NotifyChanged(this);
            Say(message + " Apply to DBZKit / Save ROM As to keep it.");
        }

        private void ExportWav()
        {
            string path = Ask(true, "WAV (*.wav)|*.wav", Stem + ".wav");
            if (path == "") return;
            var (pcm, rate, _) = CurrentPcm();
            File.WriteAllBytes(path, Wav.Write(pcm, rate));
            Say($"Wrote {Path.GetFileName(path)}.");
        }

        private void ImportWav()
        {
            string path = Ask(false, "WAV (*.wav)|*.wav", "");
            if (path == "") return;
            var rom = _rom!;
            var (raw, rawRate) = Wav.Read(File.ReadAllBytes(path));
            var (pcm, rate) = Wav.Fit(raw, rawRate);
            if (Current == Kind.Sfx) AudioTables.WriteSfx(rom, Selected, pcm, rate);
            else
            {
                int bank = AudioTables.BankAddress(rom);
                var old = AudioTables.ReadInstrument(rom, bank, Selected);
                AudioTables.WriteInstrument(rom, bank, Selected, pcm, old.Mode, old.LoopStart, rate);
            }
            Edited($"Replaced with {Path.GetFileName(path)}: {pcm.Length} samples at {rate} Hz" + (rate != rawRate ? $" (resampled from {rawRate} Hz)" : "") + ".");
        }

        private void ExportRendered()
        {
            string path = Ask(true, "WAV (*.wav)|*.wav", Stem + "_rendered.wav");
            if (path == "") return;
            var key = (Kind.Song, Selected);
            if (!_rendered.TryGetValue(key, out var song)) _rendered[key] = song = SongRenderer.Render(_rom!, Selected, AudioTables.ReadSong(_rom!, Selected));
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write("RIFF"u8.ToArray()); w.Write(36 + song.Pcm.Length * 2); w.Write("WAVEfmt "u8.ToArray());
            w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(song.Rate); w.Write(song.Rate * 4); w.Write((short)4); w.Write((short)16);
            w.Write("data"u8.ToArray()); w.Write(song.Pcm.Length * 2);
            foreach (short s in song.Pcm) w.Write(s);
            File.WriteAllBytes(path, ms.ToArray());
            Say($"Wrote {Path.GetFileName(path)} ({Clock(song.Seconds)}).");
        }

        private void ExportXm()
        {
            string path = Ask(true, "FastTracker 2 module (*.xm)|*.xm", Stem + ".xm");
            if (path == "") return;
            File.WriteAllBytes(path, XmCodec.Export(_rom!, Selected, AudioTables.ReadSong(_rom!, Selected), out string report));
            Say($"Wrote {Path.GetFileName(path)}: {report}.");
        }

        private void ImportXm()
        {
            string path = Ask(false, "FastTracker 2 module (*.xm)|*.xm", "");
            if (path == "") return;
            Edited(XmCodec.Import(_rom!, Selected, File.ReadAllBytes(path)));
        }

        private void ExportJson()
        {
            string path = Ask(true, "Song JSON (*.json)|*.json", Stem + ".json");
            if (path == "") return;
            File.WriteAllText(path, AudioTables.SongToJson(AudioTables.ReadSong(_rom!, Selected)));
            Say($"Wrote {Path.GetFileName(path)} (every event and effect, lossless).");
        }

        private void ImportJson()
        {
            string path = Ask(false, "Song JSON (*.json)|*.json", "");
            if (path == "") return;
            var song = AudioTables.SongFromJson(File.ReadAllText(path));
            AudioTables.WriteSong(_rom!, Selected, song);
            Edited($"Imported {Path.GetFileName(path)}: {song.Order.Count} orders, {song.Patterns.Count} patterns.");
        }
    }
}
