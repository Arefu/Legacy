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
            Legacy_MenuStrip = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            Legacy_OpenROM = new ToolStripMenuItem();
            Legacy_SaveROM = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            Legacy_QuitEditor = new ToolStripMenuItem();
            Legacy_AppContainer = new SplitContainer();
            Legacy_ScriptFunctions = new TreeView();
            Legacy_IDE = new ScintillaNET.Scintilla();
            toolsToolStripMenuItem = new ToolStripMenuItem();
            ToolStripMenuItem_ScriptVisualizer = new ToolStripMenuItem();
            Legacy_MenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)Legacy_AppContainer).BeginInit();
            Legacy_AppContainer.Panel1.SuspendLayout();
            Legacy_AppContainer.Panel2.SuspendLayout();
            Legacy_AppContainer.SuspendLayout();
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
            // 
            // Legacy_IDE
            // 
            Legacy_IDE.AutoCMaxHeight = 9;
            Legacy_IDE.Dock = DockStyle.Fill;
            Legacy_IDE.Location = new Point(0, 0);
            Legacy_IDE.Name = "Legacy_IDE";
            Legacy_IDE.Size = new Size(668, 705);
            Legacy_IDE.TabIndex = 0;
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { ToolStripMenuItem_ScriptVisualizer });
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            toolsToolStripMenuItem.Size = new Size(47, 20);
            toolsToolStripMenuItem.Text = "Tools";
            // 
            // ToolStripMenuItem_ScriptVisualizer
            // 
            ToolStripMenuItem_ScriptVisualizer.Name = "ToolStripMenuItem_ScriptVisualizer";
            ToolStripMenuItem_ScriptVisualizer.Size = new Size(180, 22);
            ToolStripMenuItem_ScriptVisualizer.Text = "Script Visualizer";
            ToolStripMenuItem_ScriptVisualizer.Click += ToolStripMenuItem_ScriptVisualizer_Click;
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
            Text = "Legacy";
            Legacy_MenuStrip.ResumeLayout(false);
            Legacy_MenuStrip.PerformLayout();
            Legacy_AppContainer.Panel1.ResumeLayout(false);
            Legacy_AppContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)Legacy_AppContainer).EndInit();
            Legacy_AppContainer.ResumeLayout(false);
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
    }
}
