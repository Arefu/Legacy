namespace Dragon_Radar
{
    partial class DragonRadarUI
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
            mapTreeView = new TreeView();
            mapPictureBox = new PictureBox();
            menuStrip1 = new MenuStrip();
            fIleToolStripMenuItem = new ToolStripMenuItem();
            toolStrip_OpenROM = new ToolStripMenuItem();
            listView1 = new ListView();
            ((System.ComponentModel.ISupportInitialize)mapPictureBox).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // mapTreeView
            // 
            mapTreeView.Dock = DockStyle.Left;
            mapTreeView.Location = new Point(0, 24);
            mapTreeView.Name = "mapTreeView";
            mapTreeView.Size = new Size(320, 657);
            mapTreeView.TabIndex = 1;
            mapTreeView.AfterSelect += mapTreeView_AfterSelect;
            // 
            // mapPictureBox
            // 
            mapPictureBox.BackColor = Color.Black;
            mapPictureBox.Location = new Point(326, 27);
            mapPictureBox.Name = "mapPictureBox";
            mapPictureBox.Size = new Size(720, 642);
            mapPictureBox.TabIndex = 3;
            mapPictureBox.TabStop = false;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fIleToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1264, 24);
            menuStrip1.TabIndex = 5;
            menuStrip1.Text = "menuStrip1";
            // 
            // fIleToolStripMenuItem
            // 
            fIleToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStrip_OpenROM });
            fIleToolStripMenuItem.Name = "fIleToolStripMenuItem";
            fIleToolStripMenuItem.Size = new Size(37, 20);
            fIleToolStripMenuItem.Text = "&FIle";
            // 
            // toolStrip_OpenROM
            // 
            toolStrip_OpenROM.Name = "toolStrip_OpenROM";
            toolStrip_OpenROM.ShortcutKeys = Keys.Control | Keys.O;
            toolStrip_OpenROM.Size = new Size(180, 22);
            toolStrip_OpenROM.Text = "&Open ROM";
            toolStrip_OpenROM.Click += toolStrip_OpenROM_Click;
            // 
            // listView1
            // 
            listView1.Location = new Point(1052, 27);
            listView1.Name = "listView1";
            listView1.Size = new Size(212, 642);
            listView1.TabIndex = 4;
            listView1.UseCompatibleStateImageBehavior = false;
            // 
            // DragonRadarUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1264, 681);
            Controls.Add(listView1);
            Controls.Add(mapPictureBox);
            Controls.Add(mapTreeView);
            Controls.Add(menuStrip1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MainMenuStrip = menuStrip1;
            MaximizeBox = false;
            Name = "DragonRadarUI";
            Text = "Dragon Radar";
            ((System.ComponentModel.ISupportInitialize)mapPictureBox).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private TreeView mapTreeView;
        private PictureBox mapPictureBox;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fIleToolStripMenuItem;
        private ToolStripMenuItem toolStrip_OpenROM;
        private ListView listView1;
    }
}
