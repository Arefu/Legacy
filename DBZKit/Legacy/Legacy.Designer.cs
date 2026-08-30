namespace Legacy
{
    partial class Legacy
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Legacy));
            Legacy_MenuStrip = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            Legacy_OpenROM = new ToolStripMenuItem();
            Legacy_SaveROM = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            Legacy_QuitEditor = new ToolStripMenuItem();
            toolsToolStripMenuItem = new ToolStripMenuItem();
            ToolStripMenuItem_ScriptVisualizer = new ToolStripMenuItem();
            ToolStripMenuItem_StringDecompressor = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            statViewToolStripMenuItem = new ToolStripMenuItem();
            Legacy_AppContainer = new SplitContainer();
            Legacy_ScriptFunctions = new TreeView();
            Legacy_CharacterUpDown = new NumericUpDown();
            Legacy_CharacterLabel = new Label();
            Legacy_CharacterPreview = new PictureBox();
            Legacy_TextBox = new TextBox();
            toolStrip1 = new ToolStrip();
            toolStripButton1 = new ToolStripButton();
            toolStripButton2 = new ToolStripButton();
            Legacy_IDE = new ScintillaNET.Scintilla();
            Legacy_MenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)Legacy_AppContainer).BeginInit();
            Legacy_AppContainer.Panel1.SuspendLayout();
            Legacy_AppContainer.Panel2.SuspendLayout();
            Legacy_AppContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)Legacy_CharacterUpDown).BeginInit();
            ((System.ComponentModel.ISupportInitialize)Legacy_CharacterPreview).BeginInit();
            toolStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // Legacy_MenuStrip
            // 
            Legacy_MenuStrip.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, toolsToolStripMenuItem });
            Legacy_MenuStrip.Location = new Point(0, 0);
            Legacy_MenuStrip.Name = "Legacy_MenuStrip";
            Legacy_MenuStrip.Size = new Size(1008, 24);
            Legacy_MenuStrip.TabIndex = 2;
            Legacy_MenuStrip.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { Legacy_OpenROM, Legacy_SaveROM, toolStripSeparator1, Legacy_QuitEditor });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "&File";
            // 
            // Legacy_OpenROM
            // 
            Legacy_OpenROM.Name = "Legacy_OpenROM";
            Legacy_OpenROM.ShortcutKeys = Keys.Control | Keys.O;
            Legacy_OpenROM.Size = new Size(146, 22);
            Legacy_OpenROM.Text = "&Open";
            Legacy_OpenROM.Click += Legacy_OpenROM_Click;
            // 
            // Legacy_SaveROM
            // 
            Legacy_SaveROM.Name = "Legacy_SaveROM";
            Legacy_SaveROM.ShortcutKeys = Keys.Control | Keys.S;
            Legacy_SaveROM.Size = new Size(146, 22);
            Legacy_SaveROM.Text = "&Save";
            Legacy_SaveROM.Click += Legacy_SaveROM_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(143, 6);
            // 
            // Legacy_QuitEditor
            // 
            Legacy_QuitEditor.Name = "Legacy_QuitEditor";
            Legacy_QuitEditor.Size = new Size(146, 22);
            Legacy_QuitEditor.Text = "E&xit";
            Legacy_QuitEditor.Click += Legacy_QuitEditor_Click;
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { ToolStripMenuItem_ScriptVisualizer, ToolStripMenuItem_StringDecompressor, toolStripSeparator2, statViewToolStripMenuItem });
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            toolsToolStripMenuItem.Size = new Size(47, 20);
            toolsToolStripMenuItem.Text = "Tools";
            // 
            // ToolStripMenuItem_ScriptVisualizer
            // 
            ToolStripMenuItem_ScriptVisualizer.Name = "ToolStripMenuItem_ScriptVisualizer";
            ToolStripMenuItem_ScriptVisualizer.Size = new Size(184, 22);
            ToolStripMenuItem_ScriptVisualizer.Text = "Script Visualizer";
            ToolStripMenuItem_ScriptVisualizer.Click += ToolStripMenuItem_ScriptVisualizer_Click;
            // 
            // ToolStripMenuItem_StringDecompressor
            // 
            ToolStripMenuItem_StringDecompressor.Name = "ToolStripMenuItem_StringDecompressor";
            ToolStripMenuItem_StringDecompressor.Size = new Size(184, 22);
            ToolStripMenuItem_StringDecompressor.Text = "String Decompressor";
            ToolStripMenuItem_StringDecompressor.Click += ToolStripMenuItem_StringDecompressor_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(181, 6);
            // 
            // statViewToolStripMenuItem
            // 
            statViewToolStripMenuItem.Name = "statViewToolStripMenuItem";
            statViewToolStripMenuItem.Size = new Size(184, 22);
            statViewToolStripMenuItem.Text = "Stat View";
            statViewToolStripMenuItem.Click += statViewToolStripMenuItem_Click;
            // 
            // Legacy_AppContainer
            // 
            Legacy_AppContainer.Dock = DockStyle.Fill;
            Legacy_AppContainer.Location = new Point(0, 24);
            Legacy_AppContainer.Name = "Legacy_AppContainer";
            // 
            // Legacy_AppContainer.Panel1
            // 
            Legacy_AppContainer.Panel1.Controls.Add(Legacy_ScriptFunctions);
            // 
            // Legacy_AppContainer.Panel2
            // 
            Legacy_AppContainer.Panel2.Controls.Add(Legacy_CharacterUpDown);
            Legacy_AppContainer.Panel2.Controls.Add(Legacy_CharacterLabel);
            Legacy_AppContainer.Panel2.Controls.Add(Legacy_CharacterPreview);
            Legacy_AppContainer.Panel2.Controls.Add(Legacy_TextBox);
            Legacy_AppContainer.Panel2.Controls.Add(toolStrip1);
            Legacy_AppContainer.Panel2.Controls.Add(Legacy_IDE);
            Legacy_AppContainer.Size = new Size(1008, 705);
            Legacy_AppContainer.SplitterDistance = 336;
            Legacy_AppContainer.TabIndex = 3;
            // 
            // Legacy_ScriptFunctions
            // 
            Legacy_ScriptFunctions.Dock = DockStyle.Fill;
            Legacy_ScriptFunctions.Location = new Point(0, 0);
            Legacy_ScriptFunctions.Name = "Legacy_ScriptFunctions";
            Legacy_ScriptFunctions.Size = new Size(336, 705);
            Legacy_ScriptFunctions.TabIndex = 0;
            Legacy_ScriptFunctions.AfterSelect += Legacy_ScriptFunctions_AfterSelect;
            // 
            // Legacy_CharacterUpDown
            // 
            Legacy_CharacterUpDown.Location = new Point(137, 679);
            Legacy_CharacterUpDown.Name = "Legacy_CharacterUpDown";
            Legacy_CharacterUpDown.Size = new Size(120, 23);
            Legacy_CharacterUpDown.TabIndex = 4;
            Legacy_CharacterUpDown.ValueChanged += Legacy_CharacterUpDown_ValueChanged;
            // 
            // Legacy_CharacterLabel
            // 
            Legacy_CharacterLabel.AutoSize = true;
            Legacy_CharacterLabel.Location = new Point(3, 681);
            Legacy_CharacterLabel.Name = "Legacy_CharacterLabel";
            Legacy_CharacterLabel.Size = new Size(38, 15);
            Legacy_CharacterLabel.TabIndex = 3;
            Legacy_CharacterLabel.Text = "label1";
            // 
            // Legacy_CharacterPreview
            // 
            Legacy_CharacterPreview.Location = new Point(3, 545);
            Legacy_CharacterPreview.Name = "Legacy_CharacterPreview";
            Legacy_CharacterPreview.Size = new Size(128, 128);
            Legacy_CharacterPreview.SizeMode = PictureBoxSizeMode.StretchImage;
            Legacy_CharacterPreview.TabIndex = 0;
            Legacy_CharacterPreview.TabStop = false;
            // 
            // Legacy_TextBox
            // 
            Legacy_TextBox.Location = new Point(137, 545);
            Legacy_TextBox.Multiline = true;
            Legacy_TextBox.Name = "Legacy_TextBox";
            Legacy_TextBox.Size = new Size(256, 128);
            Legacy_TextBox.TabIndex = 2;
            // 
            // toolStrip1
            // 
            toolStrip1.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip1.Items.AddRange(new ToolStripItem[] { toolStripButton1, toolStripButton2 });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(668, 25);
            toolStrip1.TabIndex = 1;
            toolStrip1.Text = "toolStrip1";
            // 
            // toolStripButton1
            // 
            toolStripButton1.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripButton1.Image = (Image)resources.GetObject("toolStripButton1.Image");
            toolStripButton1.ImageTransparentColor = Color.Magenta;
            toolStripButton1.Name = "toolStripButton1";
            toolStripButton1.Size = new Size(23, 22);
            toolStripButton1.Text = "toolStripButton1";
            // 
            // toolStripButton2
            // 
            toolStripButton2.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripButton2.Image = (Image)resources.GetObject("toolStripButton2.Image");
            toolStripButton2.ImageTransparentColor = Color.Magenta;
            toolStripButton2.Name = "toolStripButton2";
            toolStripButton2.Size = new Size(23, 22);
            toolStripButton2.Text = "toolStripButton2";
            // 
            // Legacy_IDE
            // 
            Legacy_IDE.AutoCMaxHeight = 9;
            Legacy_IDE.Location = new Point(3, 28);
            Legacy_IDE.Name = "Legacy_IDE";
            Legacy_IDE.ScrollWidth = 256;
            Legacy_IDE.Size = new Size(662, 516);
            Legacy_IDE.TabIndex = 0;
            Legacy_IDE.WrapMode = ScintillaNET.WrapMode.Word;
            // 
            // Legacy
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1008, 729);
            Controls.Add(Legacy_AppContainer);
            Controls.Add(Legacy_MenuStrip);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MainMenuStrip = Legacy_MenuStrip;
            MaximizeBox = false;
            Name = "Legacy";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Legacy";
            Load += Legacy_Load;
            Legacy_MenuStrip.ResumeLayout(false);
            Legacy_MenuStrip.PerformLayout();
            Legacy_AppContainer.Panel1.ResumeLayout(false);
            Legacy_AppContainer.Panel2.ResumeLayout(false);
            Legacy_AppContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)Legacy_AppContainer).EndInit();
            Legacy_AppContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)Legacy_CharacterUpDown).EndInit();
            ((System.ComponentModel.ISupportInitialize)Legacy_CharacterPreview).EndInit();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip Legacy_MenuStrip;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem Legacy_OpenROM;
        private ToolStripMenuItem Legacy_SaveROM;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem Legacy_QuitEditor;
        private SplitContainer Legacy_AppContainer;
        private TreeView Legacy_ScriptFunctions;
        private ScintillaNET.Scintilla Legacy_IDE;
        private ToolStripMenuItem toolsToolStripMenuItem;
        private ToolStripMenuItem ToolStripMenuItem_ScriptVisualizer;
        private ToolStripMenuItem ToolStripMenuItem_StringDecompressor;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem statViewToolStripMenuItem;
        private ToolStrip toolStrip1;
        private ToolStripButton toolStripButton1;
        private ToolStripButton toolStripButton2;
        private TextBox Legacy_TextBox;
        private PictureBox Legacy_CharacterPreview;
        private Label Legacy_CharacterLabel;
        private NumericUpDown Legacy_CharacterUpDown;
    }
}
