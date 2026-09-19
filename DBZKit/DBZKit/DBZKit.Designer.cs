namespace DBZKit
{
    partial class DBZKit
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            DBZKit_TabControl = new TabControl();
            TabPage_PortraitViewer = new TabPage();
            ListView_PortraitViewer = new ListView();
            TabPage_ItemViewer = new TabPage();
            ListView_ItemViewer = new ListView();
            TabPage_SpriteViewer = new TabPage();
            treeView2 = new TreeView();
            ListView_SpriteViewer = new ListView();
            TabPage_MiscAssetViewer = new TabPage();
            ListView_MiscSprites = new ListView();
            TreeView_MiscAssetList = new TreeView();
            TabPage_IntroViewer = new TabPage();
            PictureBox_IntroBall = new PictureBox();
            Label_IntroHint = new Label();
            TrackBar_IntroSeek = new TrackBar();
            Label_IntroSpeedCaption = new Label();
            NumericUpDown_IntroSpeed = new NumericUpDown();
            Label_IntroFrameInfo = new Label();
            Label_IntroTimingCaption = new Label();
            Panel_IntroTiming = new Panel();
            Button_IntroExportFrame = new Button();
            Button_IntroExportAll = new Button();
            Button_IntroPlayPause = new Button();
            TabPage_CreditsViewer = new TabPage();
            ListBox_CreditsViewer = new TextBox();
            Panel_CreditsToolbar = new Panel();
            Button_CreditsDump = new Button();
            Button_CreditsSave = new Button();
            Button_CreditsImport = new Button();
            TabPage_AttractMode = new TabPage();
            PictureBox_AttractFrame = new PictureBox();
            Label_AttractHint = new Label();
            TrackBar_AttractSeek = new TrackBar();
            Button_AttractPlayPause = new Button();
            Label_AttractSpeedCaption = new Label();
            NumericUpDown_AttractSpeed = new NumericUpDown();
            Label_AttractFrameInfo = new Label();
            Button_AttractExportFrame = new Button();
            Button_AttractExportAll = new Button();
            Label_AttractPaletteCaption = new Label();
            ComboBox_AttractPalette = new ComboBox();
            AttractAnimationTimer = new System.Windows.Forms.Timer(components);
            TabPage_TitleScreen = new TabPage();
            PictureBox_TitleBall = new PictureBox();
            Label_TitleScreenHint = new Label();
            Button_TitlePlayPause = new Button();
            Label_TitleSpeedCaption = new Label();
            NumericUpDown_TitleSpeed = new NumericUpDown();
            Label_TitleFrameInfo = new Label();
            Button_TitleExportFrame = new Button();
            Button_TitleExportAll = new Button();
            TitleAnimationTimer = new System.Windows.Forms.Timer(components);
            IntroAnimationTimer = new System.Windows.Forms.Timer(components);
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openRomToolStripMenuItem = new ToolStripMenuItem();
            dumpAllAssetsToolStripMenuItem = new ToolStripMenuItem();
            optionsToolStripMenuItem = new ToolStripMenuItem();
            setGameToolStripMenuItem = new ToolStripMenuItem();
            MenuEnableLOG1 = new ToolStripMenuItem();
            MenuEnableLOG2 = new ToolStripMenuItem();
            MenuEnableLOG3 = new ToolStripMenuItem();
            AssetContextMenu = new ContextMenuStrip(components);
            DBZKit_TabControl.SuspendLayout();
            TabPage_PortraitViewer.SuspendLayout();
            TabPage_ItemViewer.SuspendLayout();
            TabPage_SpriteViewer.SuspendLayout();
            TabPage_MiscAssetViewer.SuspendLayout();
            TabPage_IntroViewer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)PictureBox_IntroBall).BeginInit();
            ((System.ComponentModel.ISupportInitialize)TrackBar_IntroSeek).BeginInit();
            ((System.ComponentModel.ISupportInitialize)NumericUpDown_IntroSpeed).BeginInit();
            TabPage_CreditsViewer.SuspendLayout();
            TabPage_AttractMode.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)PictureBox_AttractFrame).BeginInit();
            ((System.ComponentModel.ISupportInitialize)TrackBar_AttractSeek).BeginInit();
            ((System.ComponentModel.ISupportInitialize)NumericUpDown_AttractSpeed).BeginInit();
            TabPage_TitleScreen.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)PictureBox_TitleBall).BeginInit();
            ((System.ComponentModel.ISupportInitialize)NumericUpDown_TitleSpeed).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // DBZKit_TabControl
            // 
            DBZKit_TabControl.Controls.Add(TabPage_PortraitViewer);
            DBZKit_TabControl.Controls.Add(TabPage_ItemViewer);
            DBZKit_TabControl.Controls.Add(TabPage_SpriteViewer);
            DBZKit_TabControl.Controls.Add(TabPage_MiscAssetViewer);
            DBZKit_TabControl.Controls.Add(TabPage_IntroViewer);
            DBZKit_TabControl.Controls.Add(TabPage_AttractMode);
            DBZKit_TabControl.Controls.Add(TabPage_TitleScreen);
            DBZKit_TabControl.Controls.Add(TabPage_CreditsViewer);
            DBZKit_TabControl.Dock = DockStyle.Fill;
            DBZKit_TabControl.Location = new Point(0, 24);
            DBZKit_TabControl.Name = "DBZKit_TabControl";
            DBZKit_TabControl.SelectedIndex = 0;
            DBZKit_TabControl.Size = new Size(1264, 657);
            DBZKit_TabControl.TabIndex = 0;
            // 
            // TabPage_PortraitViewer
            // 
            TabPage_PortraitViewer.Controls.Add(ListView_PortraitViewer);
            TabPage_PortraitViewer.Location = new Point(4, 24);
            TabPage_PortraitViewer.Name = "TabPage_PortraitViewer";
            TabPage_PortraitViewer.Padding = new Padding(3);
            TabPage_PortraitViewer.Size = new Size(1256, 629);
            TabPage_PortraitViewer.TabIndex = 1;
            TabPage_PortraitViewer.Text = "Portrait Viewer";
            TabPage_PortraitViewer.UseVisualStyleBackColor = true;
            // 
            // ListView_PortraitViewer
            // 
            ListView_PortraitViewer.Dock = DockStyle.Fill;
            ListView_PortraitViewer.Location = new Point(3, 3);
            ListView_PortraitViewer.Name = "ListView_PortraitViewer";
            ListView_PortraitViewer.Size = new Size(1250, 623);
            ListView_PortraitViewer.TabIndex = 0;
            ListView_PortraitViewer.UseCompatibleStateImageBehavior = false;
            // 
            // TabPage_ItemViewer
            // 
            TabPage_ItemViewer.Controls.Add(ListView_ItemViewer);
            TabPage_ItemViewer.Location = new Point(4, 24);
            TabPage_ItemViewer.Name = "TabPage_ItemViewer";
            TabPage_ItemViewer.Size = new Size(1256, 629);
            TabPage_ItemViewer.TabIndex = 3;
            TabPage_ItemViewer.Text = "Item Viewer";
            TabPage_ItemViewer.UseVisualStyleBackColor = true;
            // 
            // ListView_ItemViewer
            // 
            ListView_ItemViewer.Dock = DockStyle.Fill;
            ListView_ItemViewer.Location = new Point(0, 0);
            ListView_ItemViewer.Name = "ListView_ItemViewer";
            ListView_ItemViewer.Size = new Size(1256, 629);
            ListView_ItemViewer.TabIndex = 1;
            ListView_ItemViewer.UseCompatibleStateImageBehavior = false;
            // 
            // TabPage_SpriteViewer
            // 
            TabPage_SpriteViewer.Controls.Add(treeView2);
            TabPage_SpriteViewer.Controls.Add(ListView_SpriteViewer);
            TabPage_SpriteViewer.Location = new Point(4, 24);
            TabPage_SpriteViewer.Name = "TabPage_SpriteViewer";
            TabPage_SpriteViewer.Size = new Size(1256, 629);
            TabPage_SpriteViewer.TabIndex = 4;
            TabPage_SpriteViewer.Text = "Sprite Viewer";
            TabPage_SpriteViewer.UseVisualStyleBackColor = true;
            // 
            // treeView2
            // 
            treeView2.Dock = DockStyle.Left;
            treeView2.Location = new Point(0, 0);
            treeView2.Name = "treeView2";
            treeView2.Size = new Size(256, 629);
            treeView2.TabIndex = 1;
            treeView2.AfterSelect += treeView2_AfterSelect;
            // 
            // ListView_SpriteViewer
            // 
            ListView_SpriteViewer.Dock = DockStyle.Right;
            ListView_SpriteViewer.Location = new Point(262, 0);
            ListView_SpriteViewer.Name = "ListView_SpriteViewer";
            ListView_SpriteViewer.Size = new Size(994, 629);
            ListView_SpriteViewer.TabIndex = 0;
            ListView_SpriteViewer.UseCompatibleStateImageBehavior = false;
            //
            // TabPage_MiscAssetViewer
            // 
            TabPage_MiscAssetViewer.Controls.Add(ListView_MiscSprites);
            TabPage_MiscAssetViewer.Controls.Add(TreeView_MiscAssetList);
            TabPage_MiscAssetViewer.Location = new Point(4, 24);
            TabPage_MiscAssetViewer.Name = "TabPage_MiscAssetViewer";
            TabPage_MiscAssetViewer.Padding = new Padding(3);
            TabPage_MiscAssetViewer.Size = new Size(1256, 629);
            TabPage_MiscAssetViewer.TabIndex = 0;
            TabPage_MiscAssetViewer.Text = "Misc Assets";
            TabPage_MiscAssetViewer.UseVisualStyleBackColor = true;
            // 
            // ListView_MiscSprites
            // 
            ListView_MiscSprites.Dock = DockStyle.Fill;
            ListView_MiscSprites.Location = new Point(215, 3);
            ListView_MiscSprites.Name = "ListView_MiscSprites";
            ListView_MiscSprites.Size = new Size(1038, 623);
            ListView_MiscSprites.TabIndex = 3;
            ListView_MiscSprites.UseCompatibleStateImageBehavior = false;
            // 
            // TreeView_MiscAssetList
            // 
            TreeView_MiscAssetList.Dock = DockStyle.Left;
            TreeView_MiscAssetList.Location = new Point(3, 3);
            TreeView_MiscAssetList.Name = "TreeView_MiscAssetList";
            TreeView_MiscAssetList.Size = new Size(212, 623);
            TreeView_MiscAssetList.TabIndex = 1;
            //
            // TabPage_IntroViewer
            //
            // The real pre-title "intro video" -- a timed cross-fade slideshow of 4
            // full-screen bitmaps (see Assets/Intro.cs). Rendered onto a fixed-size canvas
            // matching the real GBA screen's 240x160 (3:2) aspect ratio and scaled up by
            // whole-pixel steps only, so it never looks stretched/warped -- see
            // DBZKit.UpdateIntroFrame.
            TabPage_IntroViewer.Controls.Add(Button_IntroPlayPause);
            TabPage_IntroViewer.Controls.Add(Button_IntroExportAll);
            TabPage_IntroViewer.Controls.Add(Button_IntroExportFrame);
            TabPage_IntroViewer.Controls.Add(Panel_IntroTiming);
            TabPage_IntroViewer.Controls.Add(Label_IntroTimingCaption);
            TabPage_IntroViewer.Controls.Add(Label_IntroFrameInfo);
            TabPage_IntroViewer.Controls.Add(NumericUpDown_IntroSpeed);
            TabPage_IntroViewer.Controls.Add(Label_IntroSpeedCaption);
            TabPage_IntroViewer.Controls.Add(TrackBar_IntroSeek);
            TabPage_IntroViewer.Controls.Add(PictureBox_IntroBall);
            TabPage_IntroViewer.Controls.Add(Label_IntroHint);
            TabPage_IntroViewer.Location = new Point(4, 24);
            TabPage_IntroViewer.Name = "TabPage_IntroViewer";
            TabPage_IntroViewer.Padding = new Padding(3);
            TabPage_IntroViewer.Size = new Size(1256, 629);
            TabPage_IntroViewer.TabIndex = 5;
            TabPage_IntroViewer.Text = "Intro";
            TabPage_IntroViewer.UseVisualStyleBackColor = true;
            //
            // PictureBox_IntroBall
            //
            // Native size is 3x the real GBA resolution (240x160) -- UpdateIntroFrame draws
            // into this at an integer scale and centers it, so the box's own size already
            // reflects the console's actual aspect ratio instead of stretching to fill an
            // arbitrary panel. BorderStyle marks the box's actual edge against the tab's
            // own background, since the canvas itself is often black too.
            PictureBox_IntroBall.BackColor = Color.Black;
            PictureBox_IntroBall.BorderStyle = BorderStyle.FixedSingle;
            PictureBox_IntroBall.Location = new Point(9, 46);
            PictureBox_IntroBall.Name = "PictureBox_IntroBall";
            PictureBox_IntroBall.Size = new Size(720, 480);
            PictureBox_IntroBall.SizeMode = PictureBoxSizeMode.CenterImage;
            PictureBox_IntroBall.TabIndex = 0;
            PictureBox_IntroBall.TabStop = false;
            //
            // Label_IntroHint
            //
            Label_IntroHint.Dock = DockStyle.Top;
            Label_IntroHint.Height = 40;
            Label_IntroHint.Name = "Label_IntroHint";
            Label_IntroHint.Padding = new Padding(4);
            Label_IntroHint.Text = "The real pre-title intro video: a cross-fade slideshow of 4 full-screen bitmaps (looped here for preview). See the Credits tab for the boot-sequence text roll.";
            //
            // TrackBar_IntroSeek
            //
            // Manual scrub bar across the whole slideshow -- range/value are in vblank
            // ticks (see Intro.TotalVblanks), kept in sync with playback both ways.
            TrackBar_IntroSeek.Location = new Point(75, 532);
            TrackBar_IntroSeek.Maximum = 100;
            TrackBar_IntroSeek.Name = "TrackBar_IntroSeek";
            TrackBar_IntroSeek.Size = new Size(656, 45);
            TrackBar_IntroSeek.TabIndex = 1;
            TrackBar_IntroSeek.TickStyle = TickStyle.None;
            TrackBar_IntroSeek.Scroll += TrackBar_IntroSeek_Scroll;
            //
            // Button_IntroPlayPause
            //
            Button_IntroPlayPause.Location = new Point(9, 532);
            Button_IntroPlayPause.Name = "Button_IntroPlayPause";
            Button_IntroPlayPause.Size = new Size(60, 28);
            Button_IntroPlayPause.Text = "Pause";
            Button_IntroPlayPause.UseVisualStyleBackColor = true;
            Button_IntroPlayPause.Click += Button_IntroPlayPause_Click;
            //
            // Label_IntroSpeedCaption
            //
            Label_IntroSpeedCaption.AutoSize = true;
            Label_IntroSpeedCaption.Location = new Point(9, 583);
            Label_IntroSpeedCaption.Name = "Label_IntroSpeedCaption";
            Label_IntroSpeedCaption.Size = new Size(88, 15);
            Label_IntroSpeedCaption.Text = "Playback speed:";
            //
            // NumericUpDown_IntroSpeed
            //
            // 1.00 = real-time (1 vblank/tick, ~59.7Hz); range is generous in both
            // directions for slow-motion frame inspection or fast-forwarding through holds.
            NumericUpDown_IntroSpeed.DecimalPlaces = 2;
            NumericUpDown_IntroSpeed.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            NumericUpDown_IntroSpeed.Location = new Point(103, 581);
            NumericUpDown_IntroSpeed.Maximum = new decimal(new int[] { 8, 0, 0, 0 });
            NumericUpDown_IntroSpeed.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            NumericUpDown_IntroSpeed.Name = "NumericUpDown_IntroSpeed";
            NumericUpDown_IntroSpeed.Size = new Size(70, 23);
            NumericUpDown_IntroSpeed.TabIndex = 2;
            NumericUpDown_IntroSpeed.Value = new decimal(new int[] { 1, 0, 0, 0 });
            NumericUpDown_IntroSpeed.ValueChanged += NumericUpDown_IntroSpeed_ValueChanged;
            //
            // Label_IntroFrameInfo
            //
            // Right-hand readout: current slide name/ROM address and tick position --
            // BorderStyle so it reads as a distinct panel, not stray text.
            Label_IntroFrameInfo.BorderStyle = BorderStyle.FixedSingle;
            Label_IntroFrameInfo.Location = new Point(737, 46);
            Label_IntroFrameInfo.Name = "Label_IntroFrameInfo";
            Label_IntroFrameInfo.Padding = new Padding(8);
            Label_IntroFrameInfo.Size = new Size(300, 220);
            Label_IntroFrameInfo.Text = "No ROM loaded.";
            //
            // Label_IntroTimingCaption
            //
            Label_IntroTimingCaption.AutoSize = true;
            Label_IntroTimingCaption.Location = new Point(737, 274);
            Label_IntroTimingCaption.Name = "Label_IntroTimingCaption";
            Label_IntroTimingCaption.Size = new Size(300, 30);
            Label_IntroTimingCaption.Text = "Timing (fade-in / hold, in vblanks) -- preview only, does not patch the ROM:";
            //
            // Panel_IntroTiming
            //
            // Rows built dynamically in code (DBZKit.BuildTimingEditor), one per slide --
            // not a fixed designer layout since the row count is data-driven.
            Panel_IntroTiming.AutoScroll = true;
            Panel_IntroTiming.BorderStyle = BorderStyle.FixedSingle;
            Panel_IntroTiming.Location = new Point(737, 306);
            Panel_IntroTiming.Name = "Panel_IntroTiming";
            Panel_IntroTiming.Size = new Size(300, 150);
            //
            // Button_IntroExportFrame
            //
            Button_IntroExportFrame.Location = new Point(737, 462);
            Button_IntroExportFrame.Name = "Button_IntroExportFrame";
            Button_IntroExportFrame.Size = new Size(145, 28);
            Button_IntroExportFrame.Text = "Export Current Frame (PNG)";
            Button_IntroExportFrame.UseVisualStyleBackColor = true;
            Button_IntroExportFrame.Click += Button_IntroExportFrame_Click;
            //
            // Button_IntroExportAll
            //
            Button_IntroExportAll.Location = new Point(892, 462);
            Button_IntroExportAll.Name = "Button_IntroExportAll";
            Button_IntroExportAll.Size = new Size(145, 28);
            Button_IntroExportAll.Text = "Export All Frames (PNG)";
            Button_IntroExportAll.UseVisualStyleBackColor = true;
            Button_IntroExportAll.Click += Button_IntroExportAll_Click;
            //
            // TabPage_CreditsViewer
            //
            // Split into its own tab (was sharing the Intro tab) so it can be read/edited
            // without the animation timer running alongside it.
            TabPage_CreditsViewer.Controls.Add(ListBox_CreditsViewer);
            TabPage_CreditsViewer.Controls.Add(Panel_CreditsToolbar);
            TabPage_CreditsViewer.Location = new Point(4, 24);
            TabPage_CreditsViewer.Name = "TabPage_CreditsViewer";
            TabPage_CreditsViewer.Padding = new Padding(3);
            TabPage_CreditsViewer.Size = new Size(1256, 629);
            TabPage_CreditsViewer.TabIndex = 6;
            TabPage_CreditsViewer.Text = "Credits";
            TabPage_CreditsViewer.UseVisualStyleBackColor = true;
            //
            // Panel_CreditsToolbar
            //
            Panel_CreditsToolbar.Controls.Add(Button_CreditsDump);
            Panel_CreditsToolbar.Controls.Add(Button_CreditsSave);
            Panel_CreditsToolbar.Controls.Add(Button_CreditsImport);
            Panel_CreditsToolbar.Dock = DockStyle.Top;
            Panel_CreditsToolbar.Height = 36;
            Panel_CreditsToolbar.Name = "Panel_CreditsToolbar";
            //
            // Button_CreditsDump
            //
            // "Dump" = export what's currently IN THE ROM (a fresh re-read), ignoring any
            // unsaved edits in the textbox -- for "what does the ROM actually say right now".
            Button_CreditsDump.Location = new Point(3, 6);
            Button_CreditsDump.Name = "Button_CreditsDump";
            Button_CreditsDump.Size = new Size(140, 24);
            Button_CreditsDump.Text = "Dump (from ROM)";
            Button_CreditsDump.UseVisualStyleBackColor = true;
            Button_CreditsDump.Click += Button_CreditsDump_Click;
            //
            // Button_CreditsSave
            //
            // "Save" = export whatever is CURRENTLY in the textbox, including edits.
            Button_CreditsSave.Location = new Point(149, 6);
            Button_CreditsSave.Name = "Button_CreditsSave";
            Button_CreditsSave.Size = new Size(140, 24);
            Button_CreditsSave.Text = "Save (edited text)";
            Button_CreditsSave.UseVisualStyleBackColor = true;
            Button_CreditsSave.Click += Button_CreditsSave_Click;
            //
            // Button_CreditsImport
            //
            Button_CreditsImport.Location = new Point(295, 6);
            Button_CreditsImport.Name = "Button_CreditsImport";
            Button_CreditsImport.Size = new Size(140, 24);
            Button_CreditsImport.Text = "Import (BIN or TXT)";
            Button_CreditsImport.UseVisualStyleBackColor = true;
            Button_CreditsImport.Click += Button_CreditsImport_Click;
            //
            // ListBox_CreditsViewer
            //
            // Freely editable/selectable (was a read-only ListBox) -- plain text, one
            // credit line per line, so selection highlights normally and you can type/
            // insert on any line. Not yet wired to write back into the ROM (the ROM's
            // real layout is a {titlePtr,subtitlePtr} table pointing at fixed-offset
            // strings -- editing text here doesn't relocate/repoint that yet).
            ListBox_CreditsViewer.Dock = DockStyle.Fill;
            ListBox_CreditsViewer.Font = new Font("Consolas", 9.75F);
            ListBox_CreditsViewer.Location = new Point(3, 3);
            ListBox_CreditsViewer.Multiline = true;
            ListBox_CreditsViewer.Name = "ListBox_CreditsViewer";
            ListBox_CreditsViewer.ScrollBars = ScrollBars.Vertical;
            ListBox_CreditsViewer.Size = new Size(1250, 623);
            ListBox_CreditsViewer.TabIndex = 0;
            //
            // IntroAnimationTimer
            //
            IntroAnimationTimer.Interval = 17; // ~1 vblank @ 59.7Hz, matching IntroSlideshow_Update_FadeAndAdvance's real cadence
            IntroAnimationTimer.Tick += IntroAnimationTimer_Tick;
            //
            // TabPage_AttractMode
            //
            // The REAL pre-title video -- 181 delta/keyframe-mixed frames, CONFIRMED via
            // IDA (GameIntroVideo_Update_DecodeFrame). See DrGero.Boot.GameIntroVideo's
            // class doc: this was misidentified as the credits roll in an earlier pass --
            // that was wrong and is corrected. Genuinely distinct from the "Intro" tab's
            // developer-intro slideshow (independent full frames, not delta-encoded).
            TabPage_AttractMode.Controls.Add(ComboBox_AttractPalette);
            TabPage_AttractMode.Controls.Add(Label_AttractPaletteCaption);
            TabPage_AttractMode.Controls.Add(Label_AttractFrameInfo);
            TabPage_AttractMode.Controls.Add(Button_AttractExportAll);
            TabPage_AttractMode.Controls.Add(Button_AttractExportFrame);
            TabPage_AttractMode.Controls.Add(NumericUpDown_AttractSpeed);
            TabPage_AttractMode.Controls.Add(Label_AttractSpeedCaption);
            TabPage_AttractMode.Controls.Add(Button_AttractPlayPause);
            TabPage_AttractMode.Controls.Add(TrackBar_AttractSeek);
            TabPage_AttractMode.Controls.Add(PictureBox_AttractFrame);
            TabPage_AttractMode.Controls.Add(Label_AttractHint);
            TabPage_AttractMode.Location = new Point(4, 24);
            TabPage_AttractMode.Name = "TabPage_AttractMode";
            TabPage_AttractMode.Padding = new Padding(3);
            TabPage_AttractMode.Size = new Size(1256, 629);
            TabPage_AttractMode.TabIndex = 7;
            TabPage_AttractMode.Text = "Attract Mode";
            TabPage_AttractMode.UseVisualStyleBackColor = true;
            //
            // PictureBox_AttractFrame
            //
            PictureBox_AttractFrame.BackColor = Color.Black;
            PictureBox_AttractFrame.BorderStyle = BorderStyle.FixedSingle;
            PictureBox_AttractFrame.Location = new Point(9, 46);
            PictureBox_AttractFrame.Name = "PictureBox_AttractFrame";
            PictureBox_AttractFrame.Size = new Size(720, 480);
            PictureBox_AttractFrame.SizeMode = PictureBoxSizeMode.CenterImage;
            PictureBox_AttractFrame.TabIndex = 0;
            PictureBox_AttractFrame.TabStop = false;
            //
            // Label_AttractHint
            //
            Label_AttractHint.Dock = DockStyle.Top;
            Label_AttractHint.Height = 40;
            Label_AttractHint.Name = "Label_AttractHint";
            Label_AttractHint.Padding = new Padding(4);
            Label_AttractHint.Text = "The real pre-title video -- 181 frames, genuinely delta-encoded (not independent images), ~18s at 10fps. Plays before the title screen and again after 30s idle there.";
            //
            // TrackBar_AttractSeek
            //
            TrackBar_AttractSeek.Location = new Point(75, 532);
            TrackBar_AttractSeek.Maximum = 180;
            TrackBar_AttractSeek.Name = "TrackBar_AttractSeek";
            TrackBar_AttractSeek.Size = new Size(656, 45);
            TrackBar_AttractSeek.TabIndex = 1;
            TrackBar_AttractSeek.TickStyle = TickStyle.None;
            TrackBar_AttractSeek.Scroll += TrackBar_AttractSeek_Scroll;
            //
            // Button_AttractPlayPause
            //
            Button_AttractPlayPause.Location = new Point(9, 532);
            Button_AttractPlayPause.Name = "Button_AttractPlayPause";
            Button_AttractPlayPause.Size = new Size(60, 28);
            Button_AttractPlayPause.Text = "Pause";
            Button_AttractPlayPause.UseVisualStyleBackColor = true;
            Button_AttractPlayPause.Click += Button_AttractPlayPause_Click;
            //
            // Label_AttractSpeedCaption
            //
            Label_AttractSpeedCaption.AutoSize = true;
            Label_AttractSpeedCaption.Location = new Point(9, 583);
            Label_AttractSpeedCaption.Name = "Label_AttractSpeedCaption";
            Label_AttractSpeedCaption.Size = new Size(88, 15);
            Label_AttractSpeedCaption.Text = "Playback speed:";
            //
            // NumericUpDown_AttractSpeed
            //
            NumericUpDown_AttractSpeed.DecimalPlaces = 2;
            NumericUpDown_AttractSpeed.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            NumericUpDown_AttractSpeed.Location = new Point(103, 581);
            NumericUpDown_AttractSpeed.Maximum = new decimal(new int[] { 8, 0, 0, 0 });
            NumericUpDown_AttractSpeed.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            NumericUpDown_AttractSpeed.Name = "NumericUpDown_AttractSpeed";
            NumericUpDown_AttractSpeed.Size = new Size(70, 23);
            NumericUpDown_AttractSpeed.TabIndex = 2;
            NumericUpDown_AttractSpeed.Value = new decimal(new int[] { 1, 0, 0, 0 });
            NumericUpDown_AttractSpeed.ValueChanged += NumericUpDown_AttractSpeed_ValueChanged;
            //
            // Label_AttractPaletteCaption
            //
            Label_AttractPaletteCaption.AutoSize = true;
            Label_AttractPaletteCaption.Location = new Point(737, 49);
            Label_AttractPaletteCaption.Name = "Label_AttractPaletteCaption";
            Label_AttractPaletteCaption.Size = new Size(46, 15);
            Label_AttractPaletteCaption.Text = "Palette:";
            //
            // ComboBox_AttractPalette
            //
            // Palette is NOT independently confirmed for this asset -- see
            // DrGero.Boot.GameIntroVideo's class doc. Lets you try all 3 candidates live
            // instead of guessing blind again.
            ComboBox_AttractPalette.DropDownStyle = ComboBoxStyle.DropDownList;
            ComboBox_AttractPalette.Items.AddRange(new object[] { "Embedded (frame table header)", "BG (0x081D4F50, DeveloperIntro's)", "OBJ (0x081DA6C8)" });
            ComboBox_AttractPalette.Location = new Point(789, 46);
            ComboBox_AttractPalette.Name = "ComboBox_AttractPalette";
            ComboBox_AttractPalette.Size = new Size(248, 23);
            ComboBox_AttractPalette.SelectedIndex = 0;
            ComboBox_AttractPalette.TabIndex = 3;
            ComboBox_AttractPalette.SelectedIndexChanged += ComboBox_AttractPalette_SelectedIndexChanged;
            //
            // Label_AttractFrameInfo
            //
            Label_AttractFrameInfo.BorderStyle = BorderStyle.FixedSingle;
            Label_AttractFrameInfo.Location = new Point(737, 76);
            Label_AttractFrameInfo.Name = "Label_AttractFrameInfo";
            Label_AttractFrameInfo.Padding = new Padding(8);
            Label_AttractFrameInfo.Size = new Size(300, 190);
            Label_AttractFrameInfo.Text = "No ROM loaded.";
            //
            // Button_AttractExportFrame
            //
            Button_AttractExportFrame.Location = new Point(737, 462);
            Button_AttractExportFrame.Name = "Button_AttractExportFrame";
            Button_AttractExportFrame.Size = new Size(145, 28);
            Button_AttractExportFrame.Text = "Export Current Frame (PNG)";
            Button_AttractExportFrame.UseVisualStyleBackColor = true;
            Button_AttractExportFrame.Click += Button_AttractExportFrame_Click;
            //
            // Button_AttractExportAll
            //
            Button_AttractExportAll.Location = new Point(892, 462);
            Button_AttractExportAll.Name = "Button_AttractExportAll";
            Button_AttractExportAll.Size = new Size(145, 28);
            Button_AttractExportAll.Text = "Export All Frames (PNG)";
            Button_AttractExportAll.UseVisualStyleBackColor = true;
            Button_AttractExportAll.Click += Button_AttractExportAll_Click;
            //
            // AttractAnimationTimer
            //
            AttractAnimationTimer.Interval = 100; // ~6 vblanks @ 59.7Hz, matching GameIntroVideo_Update_DecodeFrame's real cadence
            AttractAnimationTimer.Tick += AttractAnimationTimer_Tick;
            //
            // TabPage_TitleScreen
            //
            // The title screen's idle ball animation -- 8 JCALG1-compressed TILED
            // frames. CONFIRMED via IDA: plays directly after Attract Mode's video
            // finishes/is skipped (TitleScreen_Create, called from
            // GameIntroVideo_FadeOut_ThenSwitchToTitleScreen /
            // GameIntroVideo_OnSkip_SwitchToTitleScreen) -- Credits is NOT in this
            // chain, do not conflate the two. See
            // DrGero.Boot.TitleScreenBall's class doc. Distinct from Attract Mode (the
            // real pre-title video, plays before this).
            TabPage_TitleScreen.Controls.Add(Label_TitleFrameInfo);
            TabPage_TitleScreen.Controls.Add(Button_TitleExportAll);
            TabPage_TitleScreen.Controls.Add(Button_TitleExportFrame);
            TabPage_TitleScreen.Controls.Add(NumericUpDown_TitleSpeed);
            TabPage_TitleScreen.Controls.Add(Label_TitleSpeedCaption);
            TabPage_TitleScreen.Controls.Add(Button_TitlePlayPause);
            TabPage_TitleScreen.Controls.Add(PictureBox_TitleBall);
            TabPage_TitleScreen.Controls.Add(Label_TitleScreenHint);
            TabPage_TitleScreen.Location = new Point(4, 24);
            TabPage_TitleScreen.Name = "TabPage_TitleScreen";
            TabPage_TitleScreen.Padding = new Padding(3);
            TabPage_TitleScreen.Size = new Size(1256, 629);
            TabPage_TitleScreen.TabIndex = 8;
            TabPage_TitleScreen.Text = "Title Screen";
            TabPage_TitleScreen.UseVisualStyleBackColor = true;
            //
            // PictureBox_TitleBall
            //
            PictureBox_TitleBall.BackColor = Color.Black;
            PictureBox_TitleBall.BorderStyle = BorderStyle.FixedSingle;
            PictureBox_TitleBall.Location = new Point(9, 46);
            PictureBox_TitleBall.Name = "PictureBox_TitleBall";
            PictureBox_TitleBall.Size = new Size(480, 480);
            PictureBox_TitleBall.SizeMode = PictureBoxSizeMode.CenterImage;
            PictureBox_TitleBall.TabIndex = 0;
            PictureBox_TitleBall.TabStop = false;
            //
            // Label_TitleScreenHint
            //
            Label_TitleScreenHint.Dock = DockStyle.Top;
            Label_TitleScreenHint.Height = 40;
            Label_TitleScreenHint.Name = "Label_TitleScreenHint";
            Label_TitleScreenHint.Padding = new Padding(4);
            Label_TitleScreenHint.Text = "The title screen -- plays directly after Attract Mode's video finishes or is skipped (Credits is NOT part of this chain, confirmed via IDA).";
            //
            // Button_TitlePlayPause
            //
            Button_TitlePlayPause.Location = new Point(9, 532);
            Button_TitlePlayPause.Name = "Button_TitlePlayPause";
            Button_TitlePlayPause.Size = new Size(60, 28);
            Button_TitlePlayPause.Text = "Pause";
            Button_TitlePlayPause.UseVisualStyleBackColor = true;
            Button_TitlePlayPause.Click += Button_TitlePlayPause_Click;
            //
            // Label_TitleSpeedCaption
            //
            Label_TitleSpeedCaption.AutoSize = true;
            Label_TitleSpeedCaption.Location = new Point(9, 583);
            Label_TitleSpeedCaption.Name = "Label_TitleSpeedCaption";
            Label_TitleSpeedCaption.Size = new Size(88, 15);
            Label_TitleSpeedCaption.Text = "Playback speed:";
            //
            // NumericUpDown_TitleSpeed
            //
            NumericUpDown_TitleSpeed.DecimalPlaces = 2;
            NumericUpDown_TitleSpeed.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            NumericUpDown_TitleSpeed.Location = new Point(103, 581);
            NumericUpDown_TitleSpeed.Maximum = new decimal(new int[] { 8, 0, 0, 0 });
            NumericUpDown_TitleSpeed.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            NumericUpDown_TitleSpeed.Name = "NumericUpDown_TitleSpeed";
            NumericUpDown_TitleSpeed.Size = new Size(70, 23);
            NumericUpDown_TitleSpeed.TabIndex = 1;
            NumericUpDown_TitleSpeed.Value = new decimal(new int[] { 1, 0, 0, 0 });
            NumericUpDown_TitleSpeed.ValueChanged += NumericUpDown_TitleSpeed_ValueChanged;
            //
            // Label_TitleFrameInfo
            //
            Label_TitleFrameInfo.BorderStyle = BorderStyle.FixedSingle;
            Label_TitleFrameInfo.Location = new Point(497, 46);
            Label_TitleFrameInfo.Name = "Label_TitleFrameInfo";
            Label_TitleFrameInfo.Padding = new Padding(8);
            Label_TitleFrameInfo.Size = new Size(300, 220);
            Label_TitleFrameInfo.Text = "No ROM loaded.";
            //
            // Button_TitleExportFrame
            //
            Button_TitleExportFrame.Location = new Point(497, 462);
            Button_TitleExportFrame.Name = "Button_TitleExportFrame";
            Button_TitleExportFrame.Size = new Size(145, 28);
            Button_TitleExportFrame.Text = "Export Current Frame (PNG)";
            Button_TitleExportFrame.UseVisualStyleBackColor = true;
            Button_TitleExportFrame.Click += Button_TitleExportFrame_Click;
            //
            // Button_TitleExportAll
            //
            Button_TitleExportAll.Location = new Point(652, 462);
            Button_TitleExportAll.Name = "Button_TitleExportAll";
            Button_TitleExportAll.Size = new Size(145, 28);
            Button_TitleExportAll.Text = "Export All Frames (PNG)";
            Button_TitleExportAll.UseVisualStyleBackColor = true;
            Button_TitleExportAll.Click += Button_TitleExportAll_Click;
            //
            // TitleAnimationTimer
            //
            TitleAnimationTimer.Interval = 67; // ~4 vblanks @ 60Hz, matching TitleScreen_UpdateBallAnimation's real cadence
            TitleAnimationTimer.Tick += TitleAnimationTimer_Tick;
            //
            // menuStrip1
            //
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, optionsToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1264, 24);
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openRomToolStripMenuItem, dumpAllAssetsToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "&File";
            //
            // openRomToolStripMenuItem
            //
            openRomToolStripMenuItem.Name = "openRomToolStripMenuItem";
            openRomToolStripMenuItem.Size = new Size(210, 22);
            openRomToolStripMenuItem.Text = "&Open Rom";
            openRomToolStripMenuItem.Click += OpenRomToolStripMenuItem_Click;
            //
            // dumpAllAssetsToolStripMenuItem
            //
            // Extracts every asset this tool currently knows how to read (developer-intro
            // splash+slides, every Attract Mode video frame incl. deltas, title-screen ball
            // frames) into ROM\RAW (raw decompressed .bin, pre-palette/pre-blend) and
            // ROM\Assets (rendered .png) under a chosen folder, named by ROM address only.
            dumpAllAssetsToolStripMenuItem.Name = "dumpAllAssetsToolStripMenuItem";
            dumpAllAssetsToolStripMenuItem.Size = new Size(210, 22);
            dumpAllAssetsToolStripMenuItem.Text = "Dump All Known Assets...";
            dumpAllAssetsToolStripMenuItem.Click += DumpAllAssetsToolStripMenuItem_Click;
            // 
            // optionsToolStripMenuItem
            // 
            optionsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { setGameToolStripMenuItem });
            optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            optionsToolStripMenuItem.Size = new Size(61, 20);
            optionsToolStripMenuItem.Text = "Options";
            // 
            // setGameToolStripMenuItem
            // 
            setGameToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { MenuEnableLOG1, MenuEnableLOG2, MenuEnableLOG3 });
            setGameToolStripMenuItem.Name = "setGameToolStripMenuItem";
            setGameToolStripMenuItem.Size = new Size(124, 22);
            setGameToolStripMenuItem.Text = "Set Game";
            // 
            // MenuEnableLOG1
            // 
            MenuEnableLOG1.Enabled = false;
            MenuEnableLOG1.Name = "MenuEnableLOG1";
            MenuEnableLOG1.Size = new Size(165, 22);
            MenuEnableLOG1.Text = "Legacy of Goku";
            // 
            // MenuEnableLOG2
            // 
            MenuEnableLOG2.Checked = true;
            MenuEnableLOG2.CheckState = CheckState.Checked;
            MenuEnableLOG2.Name = "MenuEnableLOG2";
            MenuEnableLOG2.Size = new Size(165, 22);
            MenuEnableLOG2.Text = "Legacy of Goku II";
            // 
            // MenuEnableLOG3
            // 
            MenuEnableLOG3.Enabled = false;
            MenuEnableLOG3.Name = "MenuEnableLOG3";
            MenuEnableLOG3.Size = new Size(165, 22);
            MenuEnableLOG3.Text = "Buu's Fury";
            // 
            // AssetContextMenu
            // 
            AssetContextMenu.Name = "PortraitContextMenu";
            AssetContextMenu.Size = new Size(61, 4);
            // 
            // DBZKit
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1264, 681);
            Controls.Add(DBZKit_TabControl);
            Controls.Add(menuStrip1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "DBZKit";
            Text = "DBZ Kit";
            DBZKit_TabControl.ResumeLayout(false);
            TabPage_PortraitViewer.ResumeLayout(false);
            TabPage_ItemViewer.ResumeLayout(false);
            TabPage_SpriteViewer.ResumeLayout(false);
            TabPage_MiscAssetViewer.ResumeLayout(false);
            TabPage_IntroViewer.ResumeLayout(false);
            TabPage_IntroViewer.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)PictureBox_IntroBall).EndInit();
            ((System.ComponentModel.ISupportInitialize)TrackBar_IntroSeek).EndInit();
            ((System.ComponentModel.ISupportInitialize)NumericUpDown_IntroSpeed).EndInit();
            TabPage_CreditsViewer.ResumeLayout(false);
            TabPage_AttractMode.ResumeLayout(false);
            TabPage_AttractMode.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)PictureBox_AttractFrame).EndInit();
            ((System.ComponentModel.ISupportInitialize)TrackBar_AttractSeek).EndInit();
            ((System.ComponentModel.ISupportInitialize)NumericUpDown_AttractSpeed).EndInit();
            TabPage_TitleScreen.ResumeLayout(false);
            TabPage_TitleScreen.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)PictureBox_TitleBall).EndInit();
            ((System.ComponentModel.ISupportInitialize)NumericUpDown_TitleSpeed).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TabControl DBZKit_TabControl;
        private TabPage TabPage_MiscAssetViewer;
        private TabPage TabPage_PortraitViewer;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openRomToolStripMenuItem;
        private ToolStripMenuItem dumpAllAssetsToolStripMenuItem;
        private ToolStripMenuItem optionsToolStripMenuItem;
        private ToolStripMenuItem setGameToolStripMenuItem;
        private ToolStripMenuItem MenuEnableLOG1;
        private ToolStripMenuItem MenuEnableLOG2;
        private ToolStripMenuItem MenuEnableLOG3;
        private TreeView TreeView_MiscAssetList;
        private ListView ListView_PortraitViewer;
        private ContextMenuStrip AssetContextMenu;
        private TabPage TabPage_ItemViewer;
        private ListView ListView_ItemViewer;
        private TabPage TabPage_SpriteViewer;
        private ListView ListView_MiscSprites;
        private TreeView treeView2;
        private ListView ListView_SpriteViewer;
        private TabPage TabPage_IntroViewer;
        private PictureBox PictureBox_IntroBall;
        private Label Label_IntroHint;
        private TrackBar TrackBar_IntroSeek;
        private Label Label_IntroSpeedCaption;
        private NumericUpDown NumericUpDown_IntroSpeed;
        private Label Label_IntroFrameInfo;
        private Label Label_IntroTimingCaption;
        private Panel Panel_IntroTiming;
        private Button Button_IntroExportFrame;
        private Button Button_IntroExportAll;
        private Button Button_IntroPlayPause;
        private TabPage TabPage_CreditsViewer;
        private TextBox ListBox_CreditsViewer;
        private Panel Panel_CreditsToolbar;
        private Button Button_CreditsDump;
        private Button Button_CreditsSave;
        private Button Button_CreditsImport;
        private TabPage TabPage_AttractMode;
        private PictureBox PictureBox_AttractFrame;
        private Label Label_AttractHint;
        private TrackBar TrackBar_AttractSeek;
        private Button Button_AttractPlayPause;
        private Label Label_AttractSpeedCaption;
        private NumericUpDown NumericUpDown_AttractSpeed;
        private Label Label_AttractFrameInfo;
        private Button Button_AttractExportFrame;
        private Button Button_AttractExportAll;
        private Label Label_AttractPaletteCaption;
        private ComboBox ComboBox_AttractPalette;
        private System.Windows.Forms.Timer AttractAnimationTimer;
        private TabPage TabPage_TitleScreen;
        private PictureBox PictureBox_TitleBall;
        private Label Label_TitleScreenHint;
        private Button Button_TitlePlayPause;
        private Label Label_TitleSpeedCaption;
        private NumericUpDown NumericUpDown_TitleSpeed;
        private Label Label_TitleFrameInfo;
        private Button Button_TitleExportFrame;
        private Button Button_TitleExportAll;
        private System.Windows.Forms.Timer TitleAnimationTimer;
        private System.Windows.Forms.Timer IntroAnimationTimer;
    }
}
