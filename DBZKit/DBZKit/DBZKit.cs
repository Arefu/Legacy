using DBZKit.Assets;
using DrGero.Boot;
using DrGero.IO;

namespace DBZKit
{
    public partial class DBZKit : Form
    {
        private readonly Dictionary<int, byte[]> _PortraitData = [];
        private readonly Dictionary<int, byte[]> _ItemData = [];
        private readonly Dictionary<int, byte[]> _SpriteData = [];
        private readonly Dictionary<int, byte[]> _AbilityData = [];

        private readonly ImageList _PortraitImageList;
        private readonly ImageList _AbilityImageList;
        private readonly ImageList _SpriteImageList;
        private readonly ImageList _ItemImageList;

        private byte[]? _GBARom;

        public DBZKit()
        {
            InitializeComponent();

            _PortraitImageList = new ImageList
            {
                ImageSize = new Size(128, 128),
                ColorDepth = ColorDepth.Depth32Bit
            };

            _ItemImageList = new ImageList
            {
                ImageSize = new Size(64, 64),
                ColorDepth = ColorDepth.Depth32Bit
            };

            _SpriteImageList = new ImageList
            {
                ImageSize = new Size(64, 128),
                ColorDepth = ColorDepth.Depth32Bit
            };

            _AbilityImageList = new ImageList
            {
                ImageSize = new Size(32, 16),
                ColorDepth = ColorDepth.Depth32Bit
            };

            ListView_MiscSprites.View = View.LargeIcon;
            ListView_MiscSprites.LargeImageList = _AbilityImageList;
            ListView_MiscSprites.ContextMenuStrip = AssetContextMenu;

            ListView_ItemViewer.View = View.LargeIcon;
            ListView_ItemViewer.LargeImageList = _ItemImageList;
            ListView_ItemViewer.ContextMenuStrip = AssetContextMenu;

            ListView_PortraitViewer.View = View.LargeIcon;
            ListView_PortraitViewer.LargeImageList = _PortraitImageList;
            ListView_PortraitViewer.ContextMenuStrip = AssetContextMenu;

            ListView_SpriteViewer.View = View.LargeIcon;
            ListView_SpriteViewer.LargeImageList = _SpriteImageList;
            ListView_SpriteViewer.ContextMenuStrip = AssetContextMenu;

            AssetContextMenu.Items.Add("Export PNG", null, OnExportPng);
            AssetContextMenu.Items.Add("Export BIN", null, OnExportBin);
            AssetContextMenu.Items.Add(new ToolStripSeparator());
            AssetContextMenu.Items.Add("Replace...", null, OnReplace);
        }

        private void OnExportPng(object? sender, EventArgs e)
        {
            ListView? SelectedListView = (ListView)((ContextMenuStrip)((ToolStripMenuItem)sender).Owner).SourceControl;
            if (SelectedListView == null || SelectedListView.SelectedItems.Count == 0)
                return;

            string key = SelectedListView.SelectedItems[0].ImageKey;

            SaveFileDialog save = new() { Filter = "PNG Image|*.png", FileName = key };
            if (save.ShowDialog() != DialogResult.OK)
                return;

            switch (SelectedListView.Name)
            {
                case "ListView_PortraitViewer":
                    Bitmap? bitmap = (Bitmap)_PortraitImageList.Images[key];
                    Portraits.ExportPng(bitmap, save.FileName);
                    break;
                case "ListView_ItemViewer":
                    _ = MessageBox.Show("1");
                    break;
                default:
                    break;
            }
        }

        private void OnExportBin(object? sender, EventArgs e)
        {
            if (ListView_PortraitViewer.SelectedItems.Count == 0)
            {
                return;
            }

            string key = ListView_PortraitViewer.SelectedItems[0].ImageKey;
            int index = int.Parse(key.Split('_')[1]);

            SaveFileDialog save = new() { Filter = "Binary|*.bin", FileName = key };
            if (save.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            Portraits.ExportBin(_PortraitData[index], save.FileName);
        }

        private void OnReplace(object? sender, EventArgs e)
        {
            if (ListView_PortraitViewer.SelectedItems.Count == 0)
            {
                return;
            }

            _ = ListView_PortraitViewer.SelectedItems[0].ImageKey;

            OpenFileDialog open = new() { Filter = "PNG Image|*.png|Binary|*.bin" };
            if (open.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _ = MessageBox.Show("Replace not yet implemented.", "Coming Soon", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenRomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog OpenFile = new();
            if (OpenFile.ShowDialog() != DialogResult.OK)
            {
                MessageBox.Show("Please select a GBA ROM file to continue.", "No ROM Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _GBARom = File.ReadAllBytes(OpenFile.FileName);
            Sprites.Load3(_GBARom, _SpriteImageList, ListView_SpriteViewer, GBA.ReadPalette(_GBARom, 0x081DA6C8));
            Portraits.Load(_GBARom, _PortraitImageList, ListView_PortraitViewer, _PortraitData, GBA.ReadPalette(_GBARom, 0x081DA6C8));
            Items.Load(_GBARom, _ItemImageList, ListView_ItemViewer, _ItemData, GBA.ReadPalette(_GBARom, 0x081DA6C8));
            //    Sprites.Load(_GBARom, _SpriteImageList, ListView_SpriteViewer, GBA.ReadPalette(_GBARom, 0x081DA6C8));
            //  Sprites.Load2(_GBARom, _SpriteImageList, ListView_SpriteViewer, GBA.ReadPalette(_GBARom, 0x081DA6C8));
            Unknowns.Load(_GBARom, _AbilityImageList, ListView_MiscSprites, _SpriteData, GBA.ReadPalette(_GBARom, 0x081DA6C8));

            LoadIntroTab();
            LoadAttractModeTab();
            LoadTitleScreenTab();
        }

        // Boot-sequence "Intro" tab -- backed by DrGero.Boot (DeveloperIntro /
        // DeveloperCredits / DeveloperIntroWriter), the shared SDK module any other
        // tool built on DrGero can reuse. See that file's class docs for what's
        // actually there: the real pre-title "video" (a timed cross-fade slideshow of
        // 4 full-screen bitmaps -- CONFIRMED via IDA, Game_Main's boot order), plus the
        // separate text-only credits roll (its own "Credits" tab). Deliberately does
        // NOT include the title screen's idle ball animation -- out of scope here.
        private readonly List<Bitmap> _slideFrames = [];
        private DeveloperIntroSlide[] _slides = [];
        private int _slideIndex;
        private int _slideTick; // vblank counter within the current slide (fade-in + hold)
        private bool _introPlaying;

        // Guards against TrackBar_IntroSeek's ValueChanged/Scroll firing while WE set
        // its Value during normal playback (vs. the user actually dragging it).
        private bool _updatingIntroSeekFromCode;

        // Timing editor NumericUpDowns, built once per ROM load -- indexed the same as
        // _slides/_slideFrames. Editing these only changes THIS tool's own playback (see
        // DeveloperIntroSlide.WithTiming's doc) -- it does not patch the ROM.
        private readonly List<(NumericUpDown FadeIn, NumericUpDown Hold)> _timingEditors = [];

        private ROM Rom => ROM.FromBytes(_GBARom!);

        private void LoadIntroTab()
        {
            IntroAnimationTimer.Stop();
            _introPlaying = false;
            Button_IntroPlayPause.Text = "Play";
            foreach (var frame in _slideFrames)
                frame.Dispose();
            _slideFrames.Clear();
            _slides = [];
            _slideIndex = 0;
            _slideTick = 0;
            PictureBox_IntroBall.Image = null;
            Label_IntroFrameInfo.Text = "No ROM loaded.";
            ListBox_CreditsViewer.Clear();
            Panel_IntroTiming.Controls.Clear();
            _timingEditors.Clear();

            if (_GBARom == null) return;

            var rom = Rom;

            var creditsLines = new List<string>();
            foreach (var entry in DeveloperCredits.GetEntries(rom))
            {
                if (entry.Title.Length > 0) creditsLines.Add(entry.Title.Replace('\n', ' '));
                if (entry.Subtitle.Length > 0) creditsLines.Add("    " + entry.Subtitle.Replace('\n', ' '));
            }
            ListBox_CreditsViewer.Lines = creditsLines.ToArray();

            _slides = (DeveloperIntroSlide[])DeveloperIntro.Slides.Clone();
            _slideFrames.AddRange(DeveloperIntro.GetAllFrames(rom, DeveloperIntro.GetPalette(rom)));
            BuildTimingEditor();

            if (_slideFrames.Count > 0)
            {
                _updatingIntroSeekFromCode = true;
                TrackBar_IntroSeek.Minimum = 0;
                TrackBar_IntroSeek.Maximum = Math.Max(1, DeveloperIntro.GetTotalVblanks(_slides) - 1);
                TrackBar_IntroSeek.Value = 0;
                _updatingIntroSeekFromCode = false;

                UpdateIntroFrame();
                _introPlaying = true;
                Button_IntroPlayPause.Text = "Pause";
                IntroAnimationTimer.Start();
            }
        }

        private void Button_IntroPlayPause_Click(object? sender, EventArgs e)
        {
            if (_slideFrames.Count == 0) return;

            _introPlaying = !_introPlaying;
            Button_IntroPlayPause.Text = _introPlaying ? "Pause" : "Play";
            if (_introPlaying) IntroAnimationTimer.Start();
            else IntroAnimationTimer.Stop();
        }

        // Builds one row per slide: name, a fade-in NumericUpDown, a hold NumericUpDown.
        // Built in code rather than the designer -- the row count is data-driven (one
        // per Intro.Slideshow entry), not a fixed layout.
        private void BuildTimingEditor()
        {
            const int rowHeight = 26;
            for (int i = 0; i < _slides.Length; i++)
            {
                int index = i; // captured per-row
                var slide = _slides[i];
                int y = i * rowHeight;

                var nameLabel = new Label { Text = slide.Name, Location = new Point(0, y + 3), Size = new Size(140, 20) };

                var fadeIn = new NumericUpDown { Minimum = 0, Maximum = 3000, Value = slide.FadeInVblanks, Location = new Point(145, y), Size = new Size(60, 20) };
                fadeIn.ValueChanged += (_, _) => { _slides[index] = _slides[index].WithTiming(_slides[index].HoldVblanks, (int)fadeIn.Value); RefreshSeekRange(); };

                var hold = new NumericUpDown { Minimum = 0, Maximum = 3000, Value = slide.HoldVblanks, Location = new Point(215, y), Size = new Size(60, 20) };
                hold.ValueChanged += (_, _) => { _slides[index] = _slides[index].WithTiming((int)hold.Value, _slides[index].FadeInVblanks); RefreshSeekRange(); };

                Panel_IntroTiming.Controls.Add(nameLabel);
                Panel_IntroTiming.Controls.Add(fadeIn);
                Panel_IntroTiming.Controls.Add(hold);
                _timingEditors.Add((fadeIn, hold));
            }
        }

        // Re-ranges the seek bar after a timing edit changes the loop's total length --
        // clamps the current position instead of resetting playback.
        private void RefreshSeekRange()
        {
            _updatingIntroSeekFromCode = true;
            TrackBar_IntroSeek.Maximum = Math.Max(1, DeveloperIntro.GetTotalVblanks(_slides) - 1);
            TrackBar_IntroSeek.Value = Math.Clamp(TrackBar_IntroSeek.Value, TrackBar_IntroSeek.Minimum, TrackBar_IntroSeek.Maximum);
            _updatingIntroSeekFromCode = false;
        }

        // Draws the current slide (cross-fading in from the previous one, then holding)
        // onto the real 240x160 GBA screen canvas, nearest-neighbor integer-scaled and
        // centered in the picture box so it's never stretched off the real aspect ratio.
        private void UpdateIntroFrame()
        {
            if (_slideFrames.Count == 0) return;

            var slide = _slides[_slideIndex];
            var current = _slideFrames[_slideIndex];

            var canvas = new Bitmap(DeveloperIntro.FrameWidth, DeveloperIntro.FrameHeight);
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.Black);

                if (slide.FadeInVblanks > 0 && _slideTick < slide.FadeInVblanks)
                {
                    var previous = _slideFrames[(_slideIndex - 1 + _slideFrames.Count) % _slideFrames.Count];
                    float alpha = (float)_slideTick / slide.FadeInVblanks;
                    DrawWithAlpha(g, previous, 1f - alpha);
                    DrawWithAlpha(g, current, alpha);
                }
                else
                {
                    g.DrawImageUnscaled(current, 0, 0);
                }
            }

            int displayScale = Math.Max(1, Math.Min(PictureBox_IntroBall.Width / DeveloperIntro.FrameWidth, PictureBox_IntroBall.Height / DeveloperIntro.FrameHeight));
            var scaled = new Bitmap(DeveloperIntro.FrameWidth * displayScale, DeveloperIntro.FrameHeight * displayScale);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(canvas, 0, 0, scaled.Width, scaled.Height);
            }
            canvas.Dispose();

            PictureBox_IntroBall.Image?.Dispose();
            PictureBox_IntroBall.Image = scaled;

            int slideLength = slide.FadeInVblanks + slide.HoldVblanks;
            string phase = _slideTick < slide.FadeInVblanks ? "fading in" : "holding";
            Label_IntroFrameInfo.Text =
                $"Slide {_slideIndex + 1} / {_slideFrames.Count}\r\n" +
                $"{slide.Name}\r\n\r\n" +
                $"ROM address: 0x{slide.DataAddress:X8}\r\n\r\n" +
                $"Tick: {_slideTick} / {slideLength} ({phase})\r\n" +
                $"Fade-in length: {slide.FadeInVblanks}\r\n" +
                $"Hold length: {slide.HoldVblanks}\r\n\r\n" +
                $"Global tick: {DeveloperIntro.ToGlobalTick(_slides, _slideIndex, _slideTick)} / {DeveloperIntro.GetTotalVblanks(_slides)}";

            _updatingIntroSeekFromCode = true;
            TrackBar_IntroSeek.Value = Math.Clamp(DeveloperIntro.ToGlobalTick(_slides, _slideIndex, _slideTick), TrackBar_IntroSeek.Minimum, TrackBar_IntroSeek.Maximum);
            _updatingIntroSeekFromCode = false;

            _slideTick++;
            if (_slideTick >= slideLength)
            {
                _slideTick = 0;
                _slideIndex = (_slideIndex + 1) % _slideFrames.Count;
            }
        }

        // User dragged the seek bar -- jump playback to that exact vblank position
        // without advancing (UpdateIntroFrame's own advance-and-loop logic still runs
        // on the next timer tick from here, so playback resumes naturally).
        private void TrackBar_IntroSeek_Scroll(object? sender, EventArgs e)
        {
            if (_updatingIntroSeekFromCode || _slideFrames.Count == 0) return;

            (_slideIndex, _slideTick) = DeveloperIntro.ResolveGlobalTick(_slides, TrackBar_IntroSeek.Value);
            UpdateIntroFrame();
        }

        // Filenames are stamped with the slide's real GBA ROM address (0x08XXXXXX, as
        // Resource_LoadOrDecompress's own literal-pool pointer holds it -- see IDA,
        // e.g. 0x0801D558 for Frame1) so a dumped file can be traced straight back to
        // where it lives in the ROM, not just a made-up index/name.
        private static string IntroFrameBaseName(int index, DeveloperIntroSlide slide) =>
            $"{index:D2}_byte_{slide.DataAddress:X8}_{slide.Name.Replace(' ', '_')}";

        // Exports the currently-displayed slide as both a PNG (for viewing/editing) and
        // a BIN of the raw, uncompressed 240x160 8bpp indexed bytes exactly as
        // Resource_LoadOrDecompress_impl hands them to the game post-JCALG1 -- i.e. the
        // format any replacement data needs to match for the format=0 "stored" passthrough
        // that path already supports (see Resource_LoadOrDecompress_impl in IDA).
        private void Button_IntroExportFrame_Click(object? sender, EventArgs e)
        {
            if (_slideFrames.Count == 0 || _GBARom == null) return;

            using var dialog = new SaveFileDialog
            {
                Title = "Export current intro frame",
                Filter = "PNG Image|*.png",
                FileName = IntroFrameBaseName(_slideIndex, _slides[_slideIndex])
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            _slideFrames[_slideIndex].Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);

            byte[] raw = DeveloperIntro.GetRawIndexedBytes(Rom, _slides[_slideIndex]);
            File.WriteAllBytes(Path.ChangeExtension(dialog.FileName, ".bin"), raw);
        }

        // Exports every slide as PNG + raw indexed BIN into a chosen folder -- for
        // editing outside the tool. Re-importing an edited replacement isn't wired up
        // yet, but CONFIRMED feasible without a JCALG1 compressor: Resource_LoadOrDecompress_impl
        // (0x0801F396) has a format==0 "stored, uncompressed" passthrough (a plain
        // memcpy_unaligned), so a [format=0][size][raw bytes] block dropped at a new ROM
        // location, with the literal-pool pointer patched to it (e.g. 0x0801D558 for
        // Frame1), works with no compressor needed.
        private void Button_IntroExportAll_Click(object? sender, EventArgs e)
        {
            if (_slideFrames.Count == 0 || _GBARom == null) return;

            using var dialog = new FolderBrowserDialog { Description = "Export all intro frames as PNG + BIN" };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            for (int i = 0; i < _slideFrames.Count; i++)
            {
                string baseName = IntroFrameBaseName(i, _slides[i]);
                _slideFrames[i].Save(Path.Combine(dialog.SelectedPath, baseName + ".png"), System.Drawing.Imaging.ImageFormat.Png);

                byte[] raw = DeveloperIntro.GetRawIndexedBytes(Rom, _slides[i]);
                File.WriteAllBytes(Path.Combine(dialog.SelectedPath, baseName + ".bin"), raw);
            }

            MessageBox.Show($"Exported {_slideFrames.Count} frame(s) (PNG + raw indexed BIN) to {dialog.SelectedPath}.\n\n" +
                "Note: importing an edited replacement back into the ROM isn't wired up yet, but is " +
                "confirmed feasible without a JCALG1 compressor (format=0 stored passthrough) -- ask " +
                "to have that built next.", "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void NumericUpDown_IntroSpeed_ValueChanged(object? sender, EventArgs e)
        {
            double speed = (double)NumericUpDown_IntroSpeed.Value;
            // 17ms = 1 vblank @ 59.7Hz (real-time, speed=1.0); clamp so extreme speeds
            // can't produce a zero/negative or absurdly long WinForms Timer interval.
            IntroAnimationTimer.Interval = Math.Clamp((int)Math.Round(17.0 / speed), 1, 2000);
        }

        private static void DrawWithAlpha(Graphics g, Bitmap bmp, float alpha)
        {
            alpha = Math.Clamp(alpha, 0f, 1f);
            var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha };
            using var attr = new System.Drawing.Imaging.ImageAttributes();
            attr.SetColorMatrix(matrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
            g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attr);
        }

        // ~1 vblank (59.7Hz) per tick -- matches IntroSlideshow_Update_FadeAndAdvance's
        // real timing (one advance per vblank), see Intro.cs's class doc.
        private void IntroAnimationTimer_Tick(object? sender, EventArgs e) => UpdateIntroFrame();

        // "Attract Mode" tab -- the REAL pre-title video (181 delta/keyframe-mixed
        // frames, CONFIRMED via IDA -- see DrGero.Boot.GameIntroVideo's class doc for
        // the full story, including a prior WRONG claim in this codebase that this was
        // the credits roll). Simple linear playback, no cross-fade/editable timing --
        // unlike the Intro tab's slideshow, this one really is a frame sequence.
        private readonly List<Bitmap> _attractFrames = [];
        private List<GameIntroVideo.DecodedFrame> _attractFrameMeta = [];
        private int _attractFrameIndex;
        private bool _attractPlaying;
        private bool _updatingAttractSeekFromCode;

        private void LoadAttractModeTab()
        {
            AttractAnimationTimer.Stop();
            _attractPlaying = false;
            Button_AttractPlayPause.Text = "Play";
            foreach (var frame in _attractFrames)
                frame.Dispose();
            _attractFrames.Clear();
            _attractFrameMeta = [];
            _attractFrameIndex = 0;
            PictureBox_AttractFrame.Image = null;
            Label_AttractFrameInfo.Text = "No ROM loaded.";

            if (_GBARom == null) return;

            var rom = Rom;
            _attractFrameMeta = GameIntroVideo.DecodeAllFramesRaw(rom); // expensive (181x JCALG1) -- done once; palette changes only re-render bitmaps, not re-decode
            RebuildAttractBitmaps();

            if (_attractFrames.Count > 0)
            {
                _updatingAttractSeekFromCode = true;
                TrackBar_AttractSeek.Minimum = 0;
                TrackBar_AttractSeek.Maximum = _attractFrames.Count - 1;
                TrackBar_AttractSeek.Value = 0;
                _updatingAttractSeekFromCode = false;

                UpdateAttractFrame();
                _attractPlaying = true;
                Button_AttractPlayPause.Text = "Pause";
                AttractAnimationTimer.Start();
            }
        }

        // Re-renders bitmaps from the already-decoded raw frame data with the currently
        // selected palette -- cheap (no re-decompression) so the combo box can swap
        // palettes live. See DrGero.Boot.GameIntroVideo class doc -- palette is
        // genuinely unconfirmed for this asset, this lets you try all 3 candidates.
        private void RebuildAttractBitmaps()
        {
            foreach (var frame in _attractFrames)
                frame.Dispose();
            _attractFrames.Clear();

            if (_GBARom == null || _attractFrameMeta.Count == 0) return;

            var source = (GameIntroVideo.PaletteSource)ComboBox_AttractPalette.SelectedIndex;
            var palette = GameIntroVideo.GetPalette(Rom, source);
            foreach (var frame in _attractFrameMeta)
                _attractFrames.Add(DrGero.Boot.DeveloperIntro.RenderIndexedTiled(frame.Data, GameIntroVideo.FrameWidth / 8, GameIntroVideo.FrameHeight / 8, palette));
        }

        private void ComboBox_AttractPalette_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_attractFrameMeta.Count == 0) return;

            RebuildAttractBitmaps();
            // UpdateAttractFrame renders _attractFrameIndex then advances it -- step back
            // first so this redisplays the SAME frame (just re-palettized) instead of skipping ahead.
            _attractFrameIndex = _attractFrameIndex == 0 ? _attractFrames.Count - 1 : _attractFrameIndex - 1;
            UpdateAttractFrame();
        }

        private void UpdateAttractFrame()
        {
            if (_attractFrames.Count == 0) return;

            var frame = _attractFrames[_attractFrameIndex];
            var meta = _attractFrameMeta[_attractFrameIndex];

            int displayScale = Math.Max(1, Math.Min(PictureBox_AttractFrame.Width / GameIntroVideo.FrameWidth, PictureBox_AttractFrame.Height / GameIntroVideo.FrameHeight));
            var scaled = new Bitmap(GameIntroVideo.FrameWidth * displayScale, GameIntroVideo.FrameHeight * displayScale);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(frame, 0, 0, scaled.Width, scaled.Height);
            }

            PictureBox_AttractFrame.Image?.Dispose();
            PictureBox_AttractFrame.Image = scaled;

            Label_AttractFrameInfo.Text =
                $"Frame {_attractFrameIndex + 1} / {_attractFrames.Count}\r\n\r\n" +
                $"Resource address: 0x{meta.ResourceAddress:X8}\r\n" +
                $"Format: {meta.Format} ({(meta.IsDelta ? "DELTA -- blended onto previous frame" : "keyframe -- replaces buffer outright")})\r\n\r\n" +
                $"Palette: {ComboBox_AttractPalette.Text} (unconfirmed -- see DrGero.Boot.GameIntroVideo class doc)";

            _updatingAttractSeekFromCode = true;
            TrackBar_AttractSeek.Value = _attractFrameIndex;
            _updatingAttractSeekFromCode = false;

            _attractFrameIndex = (_attractFrameIndex + 1) % _attractFrames.Count;
        }

        private void Button_AttractPlayPause_Click(object? sender, EventArgs e)
        {
            if (_attractFrames.Count == 0) return;

            _attractPlaying = !_attractPlaying;
            Button_AttractPlayPause.Text = _attractPlaying ? "Pause" : "Play";
            if (_attractPlaying) AttractAnimationTimer.Start();
            else AttractAnimationTimer.Stop();
        }

        private void TrackBar_AttractSeek_Scroll(object? sender, EventArgs e)
        {
            if (_updatingAttractSeekFromCode || _attractFrames.Count == 0) return;

            _attractFrameIndex = TrackBar_AttractSeek.Value;
            UpdateAttractFrame();
        }

        private void NumericUpDown_AttractSpeed_ValueChanged(object? sender, EventArgs e)
        {
            double speed = (double)NumericUpDown_AttractSpeed.Value;
            // 100ms = 6 vblanks @ 59.7Hz (real-time, speed=1.0), matching GameIntroVideo_Update_DecodeFrame's real cadence.
            AttractAnimationTimer.Interval = Math.Clamp((int)Math.Round(100.0 / speed), 1, 5000);
        }

        private static string AttractFrameBaseName(int index, GameIntroVideo.DecodedFrame frame) =>
            $"{index:D3}_byte_{frame.ResourceAddress:X8}_{(frame.IsDelta ? "delta" : "keyframe")}";

        private void Button_AttractExportFrame_Click(object? sender, EventArgs e)
        {
            if (_attractFrames.Count == 0) return;

            int index = _attractFrameIndex == 0 ? _attractFrames.Count - 1 : _attractFrameIndex - 1; // UpdateAttractFrame already advanced past the displayed frame
            using var dialog = new SaveFileDialog
            {
                Title = "Export current attract-mode frame",
                Filter = "PNG Image|*.png",
                FileName = AttractFrameBaseName(index, _attractFrameMeta[index])
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            _attractFrames[index].Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
            File.WriteAllBytes(Path.ChangeExtension(dialog.FileName, ".bin"), _attractFrameMeta[index].Data);
        }

        private void Button_AttractExportAll_Click(object? sender, EventArgs e)
        {
            if (_attractFrames.Count == 0) return;

            using var dialog = new FolderBrowserDialog { Description = "Export all attract-mode frames as PNG + BIN" };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            for (int i = 0; i < _attractFrames.Count; i++)
            {
                string baseName = AttractFrameBaseName(i, _attractFrameMeta[i]);
                _attractFrames[i].Save(Path.Combine(dialog.SelectedPath, baseName + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                File.WriteAllBytes(Path.Combine(dialog.SelectedPath, baseName + ".bin"), _attractFrameMeta[i].Data);
            }

            MessageBox.Show($"Exported {_attractFrames.Count} frame(s) (PNG + raw indexed BIN) to {dialog.SelectedPath}.",
                "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ~6 vblanks (~10fps @ 59.7Hz) per tick -- matches GameIntroVideo_Update_DecodeFrame's real cadence.
        private void AttractAnimationTimer_Tick(object? sender, EventArgs e) => UpdateAttractFrame();

        // "Title Screen" tab -- CONFIRMED via IDA (GameIntroVideo_OnFinished_StartFadeOut
        // -> unk_8024610 -> GameIntroVideo_FadeOut_ThenSwitchToTitleScreen ->
        // TitleScreen_Create): this plays directly after Attract Mode's video finishes
        // or is skipped. Credits is NOT part of this chain -- its real caller still
        // isn't confirmed (see DrGero.Boot.DeveloperCredits doc), do not conflate them.
        // while waiting for input. See DrGero.Boot.TitleScreenBall's class doc.
        private readonly List<Bitmap> _titleFrames = [];
        private int _titleFrameIndex;
        private bool _titlePlaying;

        private void LoadTitleScreenTab()
        {
            TitleAnimationTimer.Stop();
            _titlePlaying = false;
            Button_TitlePlayPause.Text = "Play";
            foreach (var frame in _titleFrames)
                frame.Dispose();
            _titleFrames.Clear();
            _titleFrameIndex = 0;
            PictureBox_TitleBall.Image = null;
            Label_TitleFrameInfo.Text = "No ROM loaded.";

            if (_GBARom == null) return;

            var rom = Rom;
            _titleFrames.AddRange(TitleScreenBall.GetFrames(rom, TitleScreenBall.GetPalette(rom)));

            if (_titleFrames.Count > 0)
            {
                UpdateTitleFrame();
                _titlePlaying = true;
                Button_TitlePlayPause.Text = "Pause";
                TitleAnimationTimer.Start();
            }
        }

        private void UpdateTitleFrame()
        {
            if (_titleFrames.Count == 0) return;

            var frame = _titleFrames[_titleFrameIndex];
            int displayScale = Math.Max(1, Math.Min(PictureBox_TitleBall.Width / frame.Width, PictureBox_TitleBall.Height / frame.Height));
            var scaled = new Bitmap(frame.Width * displayScale, frame.Height * displayScale);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(frame, 0, 0, scaled.Width, scaled.Height);
            }

            PictureBox_TitleBall.Image?.Dispose();
            PictureBox_TitleBall.Image = scaled;

            Label_TitleFrameInfo.Text =
                $"Frame {_titleFrameIndex + 1} / {_titleFrames.Count}\r\n\r\n" +
                $"Native size: {frame.Width}x{frame.Height}\r\n\r\n" +
                "Palette: OBJ palette 0x081DA6C8 (confirmed via traced DMA -- see DrGero.Boot.TitleScreenBall class doc)";

            _titleFrameIndex = (_titleFrameIndex + 1) % _titleFrames.Count;
        }

        private void Button_TitlePlayPause_Click(object? sender, EventArgs e)
        {
            if (_titleFrames.Count == 0) return;

            _titlePlaying = !_titlePlaying;
            Button_TitlePlayPause.Text = _titlePlaying ? "Pause" : "Play";
            if (_titlePlaying) TitleAnimationTimer.Start();
            else TitleAnimationTimer.Stop();
        }

        private void NumericUpDown_TitleSpeed_ValueChanged(object? sender, EventArgs e)
        {
            double speed = (double)NumericUpDown_TitleSpeed.Value;
            TitleAnimationTimer.Interval = Math.Clamp((int)Math.Round(67.0 / speed), 1, 2000);
        }

        private void Button_TitleExportFrame_Click(object? sender, EventArgs e)
        {
            if (_titleFrames.Count == 0) return;

            int index = _titleFrameIndex == 0 ? _titleFrames.Count - 1 : _titleFrameIndex - 1; // UpdateTitleFrame already advanced past the displayed frame
            using var dialog = new SaveFileDialog
            {
                Title = "Export current title-screen ball frame",
                Filter = "PNG Image|*.png",
                FileName = $"{index:D2}_TitleBall"
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            _titleFrames[index].Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
        }

        private void Button_TitleExportAll_Click(object? sender, EventArgs e)
        {
            if (_titleFrames.Count == 0) return;

            using var dialog = new FolderBrowserDialog { Description = "Export all title-screen ball frames as PNG" };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            for (int i = 0; i < _titleFrames.Count; i++)
                _titleFrames[i].Save(Path.Combine(dialog.SelectedPath, $"{i:D2}_TitleBall.png"), System.Drawing.Imaging.ImageFormat.Png);

            MessageBox.Show($"Exported {_titleFrames.Count} frame(s) to {dialog.SelectedPath}.", "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TitleAnimationTimer_Tick(object? sender, EventArgs e) => UpdateTitleFrame();

        // Dumps every asset this tool currently knows how to read into a chosen folder:
        //   <folder>\ROM\RAW\byte_<address>.bin    -- raw decompressed bytes, pre-palette/pre-blend
        //   <folder>\ROM\Assets\byte_<address>.png  -- rendered preview
        // Filenames are the ROM address ONLY (no index, no descriptive suffix) -- for
        // GameIntroVideo this means every one of its 181 frames (including deltas) is
        // dumped as its own independent resource, exactly as it exists in the ROM, not
        // the blended/composited playback result. Import back into the ROM isn't wired
        // up yet -- this is extraction only.
        private void DumpAllAssetsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (_GBARom == null)
            {
                MessageBox.Show("No ROM loaded.");
                return;
            }

            using var dialog = new FolderBrowserDialog { Description = "Choose a folder to dump ROM\\RAW and ROM\\Assets into" };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            string rawDir = Path.Combine(dialog.SelectedPath, "ROM", "RAW");
            string assetsDir = Path.Combine(dialog.SelectedPath, "ROM", "Assets");
            Directory.CreateDirectory(rawDir);
            Directory.CreateDirectory(assetsDir);

            var rom = Rom;
            int dumped = 0;

            void DumpOne(int address, byte[] raw, Bitmap? png)
            {
                string name = $"byte_{address:X8}";
                File.WriteAllBytes(Path.Combine(rawDir, name + ".bin"), raw);
                if (png != null)
                {
                    png.Save(Path.Combine(assetsDir, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                    png.Dispose();
                }
                dumped++;
            }

            // DeveloperIntro: pre-boot splash + 3 slideshow frames, flat 240x160.
            var devPalette = DeveloperIntro.GetPalette(rom);
            foreach (var slide in DeveloperIntro.Slides)
            {
                byte[] raw = DeveloperIntro.GetRawIndexedBytes(rom, slide);
                Bitmap? png = raw.Length >= DeveloperIntro.FrameWidth * DeveloperIntro.FrameHeight
                    ? DeveloperIntro.RenderIndexed(raw, DeveloperIntro.FrameWidth, DeveloperIntro.FrameHeight, devPalette)
                    : null;
                DumpOne(slide.DataAddress, raw, png);
            }

            // GameIntroVideo: all 181 frame resources, each dumped independently by its
            // own address -- deltas included as raw (unblended) data, tiled 240x160.
            var videoPalette = GameIntroVideo.GetPalette(rom, (GameIntroVideo.PaletteSource)ComboBox_AttractPalette.SelectedIndex);
            foreach (int addr in GameIntroVideo.GetFrameResourceAddresses(rom))
            {
                if (addr == 0) continue;
                try
                {
                    byte[] raw = GameIntroVideo.GetRawFrameBytes(rom, addr);
                    var png = DeveloperIntro.RenderIndexedTiled(raw, GameIntroVideo.FrameWidth / 8, GameIntroVideo.FrameHeight / 8, videoPalette);
                    DumpOne(addr, raw, png);
                }
                catch { /* corrupt/unexpected -- skip rather than fail the whole dump */ }
            }

            // TitleScreenBall: 8 frames, tiled 32x32.
            var ballPalette = TitleScreenBall.GetPalette(rom);
            foreach (int addr in TitleScreenBall.GetFrameResourceAddresses(rom))
            {
                if (addr == 0) continue;
                try
                {
                    byte[] raw = TitleScreenBall.GetRawFrameBytes(rom, addr);
                    var png = DeveloperIntro.RenderIndexedTiled(raw, TitleScreenBall.FrameWidth / 8, TitleScreenBall.FrameHeight / 8, ballPalette);
                    DumpOne(addr, raw, png);
                }
                catch { }
            }

            MessageBox.Show($"Dumped {dumped} asset(s) to:\n{rawDir}\n{assetsDir}", "Dump complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Credits: dump (export current ROM text to BIN+TXT), import (load a BIN or TXT
        // file into the editable textbox), save (export the CURRENTLY EDITED textbox
        // content -- same file formats, so an edit can round-trip through this tool).
        // BIN is the strings in the ROM's own encoding (null-terminated ASCII,
        // title then subtitle per entry, back-to-back) -- the format future ROM
        // re-import would need to match. TXT is plain human-readable text (one line per
        // title/subtitle, blank line between entries) -- exactly what the textbox shows.
        private void Button_CreditsDump_Click(object? sender, EventArgs e)
        {
            if (_GBARom == null) { MessageBox.Show("No ROM loaded."); return; }
            ExportCreditsAs(DeveloperCredits.GetEntries(Rom));
        }

        private void Button_CreditsSave_Click(object? sender, EventArgs e)
        {
            // Saves whatever is CURRENTLY in the textbox (possibly hand-edited), not a
            // fresh re-read from the ROM -- reconstructed as title/subtitle pairs by
            // the same "    " (4-space) subtitle-indent convention LoadIntroTab writes.
            var entries = new List<CreditsEntry>();
            string? pendingTitle = null;
            foreach (string line in ListBox_CreditsViewer.Lines)
            {
                if (line.StartsWith("    "))
                {
                    entries.Add(new CreditsEntry(pendingTitle ?? "", line[4..]));
                    pendingTitle = null;
                }
                else
                {
                    if (pendingTitle != null) entries.Add(new CreditsEntry(pendingTitle, ""));
                    pendingTitle = line;
                }
            }
            if (pendingTitle != null) entries.Add(new CreditsEntry(pendingTitle, ""));

            ExportCreditsAs(entries);
        }

        private void ExportCreditsAs(List<CreditsEntry> entries)
        {
            using var dialog = new SaveFileDialog { Title = "Save credits", Filter = "Text|*.txt", FileName = "Credits" };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            string txtPath = dialog.FileName;
            string binPath = Path.ChangeExtension(dialog.FileName, ".bin");

            var txtLines = new List<string>();
            using var bin = new MemoryStream();
            foreach (var entry in entries)
            {
                if (entry.Title.Length > 0) { txtLines.Add(entry.Title); WriteCString(bin, entry.Title); }
                if (entry.Subtitle.Length > 0) { txtLines.Add("    " + entry.Subtitle); WriteCString(bin, entry.Subtitle); }
                txtLines.Add("");
            }

            File.WriteAllLines(txtPath, txtLines);
            File.WriteAllBytes(binPath, bin.ToArray());
            MessageBox.Show($"Saved:\n{txtPath}\n{binPath}", "Credits saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void WriteCString(Stream s, string text)
        {
            foreach (byte b in System.Text.Encoding.ASCII.GetBytes(text)) s.WriteByte(b);
            s.WriteByte(0);
        }

        private void Button_CreditsImport_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog { Title = "Import credits", Filter = "Credits files|*.txt;*.bin|Text|*.txt|Binary|*.bin" };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            if (Path.GetExtension(dialog.FileName).Equals(".bin", StringComparison.OrdinalIgnoreCase))
            {
                byte[] data = File.ReadAllBytes(dialog.FileName);
                var lines = new List<string>();
                int start = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] != 0) continue;
                    lines.Add(System.Text.Encoding.ASCII.GetString(data, start, i - start));
                    start = i + 1;
                }
                ListBox_CreditsViewer.Lines = lines.ToArray();
            }
            else
            {
                ListBox_CreditsViewer.Lines = File.ReadAllLines(dialog.FileName);
            }
        }
    }
}
