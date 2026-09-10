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
            toolStrip_SaveROM = new ToolStripMenuItem();
            sidebarTabControl = new TabControl();
            tilesTabPage = new TabPage();
            listView1 = new ListView();
            itemsTabPage = new TabPage();
            itemsListView = new ListView();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            ((System.ComponentModel.ISupportInitialize)mapPictureBox).BeginInit();
            menuStrip1.SuspendLayout();
            sidebarTabControl.SuspendLayout();
            tilesTabPage.SuspendLayout();
            itemsTabPage.SuspendLayout();
            statusStrip1.SuspendLayout();
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
            fIleToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStrip_OpenROM, toolStrip_SaveROM });
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
            // toolStrip_SaveROM
            //
            toolStrip_SaveROM.Name = "toolStrip_SaveROM";
            toolStrip_SaveROM.ShortcutKeys = Keys.Control | Keys.S;
            toolStrip_SaveROM.Size = new Size(180, 22);
            toolStrip_SaveROM.Text = "&Save ROM As...";
            toolStrip_SaveROM.Click += toolStrip_SaveROM_Click;
            //
            // sidebarTabControl
            //
            sidebarTabControl.Controls.Add(tilesTabPage);
            sidebarTabControl.Controls.Add(itemsTabPage);
            sidebarTabControl.Location = new Point(1052, 27);
            sidebarTabControl.Name = "sidebarTabControl";
            sidebarTabControl.SelectedIndex = 0;
            sidebarTabControl.Size = new Size(212, 642);
            sidebarTabControl.TabIndex = 4;
            //
            // tilesTabPage
            //
            tilesTabPage.Controls.Add(listView1);
            tilesTabPage.Location = new Point(4, 24);
            tilesTabPage.Name = "tilesTabPage";
            tilesTabPage.Size = new Size(204, 614);
            tilesTabPage.TabIndex = 0;
            tilesTabPage.Text = "Tiles";
            //
            // listView1
            //
            listView1.Dock = DockStyle.Fill;
            listView1.Location = new Point(0, 0);
            listView1.Name = "listView1";
            listView1.Size = new Size(204, 614);
            listView1.TabIndex = 4;
            listView1.UseCompatibleStateImageBehavior = false;
            //
            // itemsTabPage
            //
            itemsTabPage.Controls.Add(itemsListView);
            itemsTabPage.Location = new Point(4, 24);
            itemsTabPage.Name = "itemsTabPage";
            itemsTabPage.Size = new Size(204, 614);
            itemsTabPage.TabIndex = 1;
            itemsTabPage.Text = "Items";
            //
            // itemsListView
            //
            itemsListView.Dock = DockStyle.Fill;
            itemsListView.Location = new Point(0, 0);
            itemsListView.Name = "itemsListView";
            itemsListView.Size = new Size(204, 614);
            itemsListView.TabIndex = 0;
            itemsListView.UseCompatibleStateImageBehavior = false;
            itemsListView.MouseDown += itemsListView_MouseDown;
            //
            // statusStrip1
            //
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel });
            statusStrip1.Location = new Point(0, 659);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1264, 22);
            statusStrip1.TabIndex = 6;
            //
            // statusLabel
            //
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(118, 17);
            statusLabel.Text = "No entity selected";
            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // DragonRadarUI
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1264, 681);
            Controls.Add(sidebarTabControl);
            Controls.Add(mapPictureBox);
            Controls.Add(mapTreeView);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MainMenuStrip = menuStrip1;
            MaximizeBox = false;
            Name = "DragonRadarUI";
            Text = "Dragon Radar";
            ((System.ComponentModel.ISupportInitialize)mapPictureBox).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            sidebarTabControl.ResumeLayout(false);
            tilesTabPage.ResumeLayout(false);
            itemsTabPage.ResumeLayout(false);
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private TreeView mapTreeView;
        private PictureBox mapPictureBox;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fIleToolStripMenuItem;
        private ToolStripMenuItem toolStrip_OpenROM;
        private ToolStripMenuItem toolStrip_SaveROM;
        private TabControl sidebarTabControl;
        private TabPage tilesTabPage;
        private ListView listView1;
        private TabPage itemsTabPage;
        private ListView itemsListView;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
    }
}
