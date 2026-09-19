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
            mapScrollPanel = new Panel();
            mapPictureBox = new PictureBox();
            menuStrip1 = new MenuStrip();
            fIleToolStripMenuItem = new ToolStripMenuItem();
            toolStrip_OpenROM = new ToolStripMenuItem();
            toolStrip_SaveROM = new ToolStripMenuItem();
            viewportToolStrip = new ToolStrip();
            toolStrip_ShowCollision = new ToolStripButton();
            toolStrip_RefreshMap = new ToolStripButton();
            sidebarTabControl = new TabControl();
            tilesTabPage = new TabPage();
            listView1 = new ListView();
            itemsTabPage = new TabPage();
            itemsListView = new ListView();
            npcsTabPage = new TabPage();
            npcListView = new ListView();
            npcHintLabel = new Label();
            propertiesTabPage = new TabPage();
            propertiesGroupBox = new GroupBox();
            propertiesLabel = new Label();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            mapScrollPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)mapPictureBox).BeginInit();
            menuStrip1.SuspendLayout();
            viewportToolStrip.SuspendLayout();
            sidebarTabControl.SuspendLayout();
            tilesTabPage.SuspendLayout();
            itemsTabPage.SuspendLayout();
            npcsTabPage.SuspendLayout();
            propertiesTabPage.SuspendLayout();
            propertiesGroupBox.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            //
            // mapTreeView
            //
            mapTreeView.Dock = DockStyle.Left;
            mapTreeView.Location = new Point(0, 52);
            mapTreeView.Name = "mapTreeView";
            mapTreeView.Size = new Size(320, 826);
            mapTreeView.TabIndex = 1;
            mapTreeView.AfterSelect += mapTreeView_AfterSelect;
            mapTreeView.BeforeSelect += mapTreeView_BeforeSelect;
            //
            // mapScrollPanel
            //
            mapScrollPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            mapScrollPanel.AutoScroll = true;
            mapScrollPanel.BackColor = Color.Black;
            mapScrollPanel.Controls.Add(mapPictureBox);
            mapScrollPanel.Location = new Point(326, 55);
            mapScrollPanel.Name = "mapScrollPanel";
            mapScrollPanel.Size = new Size(944, 816);
            mapScrollPanel.TabIndex = 7;
            // 
            // mapPictureBox
            // 
            mapPictureBox.BackColor = Color.Black;
            mapPictureBox.Location = new Point(0, 0);
            mapPictureBox.Name = "mapPictureBox";
            mapPictureBox.Size = new Size(944, 844);
            mapPictureBox.TabIndex = 3;
            mapPictureBox.TabStop = false;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fIleToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1500, 24);
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
            toolStrip_OpenROM.Size = new Size(193, 22);
            toolStrip_OpenROM.Text = "&Open ROM";
            toolStrip_OpenROM.Click += toolStrip_OpenROM_Click;
            // 
            // toolStrip_SaveROM
            // 
            toolStrip_SaveROM.Name = "toolStrip_SaveROM";
            toolStrip_SaveROM.ShortcutKeys = Keys.Control | Keys.S;
            toolStrip_SaveROM.Size = new Size(193, 22);
            toolStrip_SaveROM.Text = "&Save ROM As...";
            toolStrip_SaveROM.Click += toolStrip_SaveROM_Click;
            //
            // viewportToolStrip
            //
            viewportToolStrip.Items.AddRange(new ToolStripItem[] { toolStrip_ShowCollision, toolStrip_RefreshMap });
            viewportToolStrip.Location = new Point(0, 24);
            viewportToolStrip.Name = "viewportToolStrip";
            viewportToolStrip.Size = new Size(1500, 28);
            viewportToolStrip.TabIndex = 8;
            viewportToolStrip.Text = "viewportToolStrip";
            //
            // toolStrip_ShowCollision
            //
            toolStrip_ShowCollision.CheckOnClick = true;
            toolStrip_ShowCollision.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStrip_ShowCollision.Name = "toolStrip_ShowCollision";
            toolStrip_ShowCollision.Size = new Size(97, 25);
            toolStrip_ShowCollision.Text = "Show Collision";
            toolStrip_ShowCollision.ToolTipText = "Overlay solid/blocked tiles (red) on the map";
            toolStrip_ShowCollision.CheckedChanged += toolStrip_ShowCollision_CheckedChanged;
            //
            // toolStrip_RefreshMap
            //
            // WinForms TreeView doesn't fire AfterSelect when you click a node that's
            // already selected, so there was previously no way to force a reload without
            // selecting a different node and clicking back. This does it directly.
            toolStrip_RefreshMap.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStrip_RefreshMap.Name = "toolStrip_RefreshMap";
            toolStrip_RefreshMap.Size = new Size(97, 25);
            toolStrip_RefreshMap.Text = "Refresh Map";
            toolStrip_RefreshMap.ToolTipText = "Reload the current map from the live ROM";
            toolStrip_RefreshMap.Click += toolStrip_RefreshMap_Click;
            //
            // sidebarTabControl
            //
            sidebarTabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            sidebarTabControl.Controls.Add(tilesTabPage);
            sidebarTabControl.Controls.Add(itemsTabPage);
            sidebarTabControl.Controls.Add(npcsTabPage);
            sidebarTabControl.Controls.Add(propertiesTabPage);
            sidebarTabControl.Location = new Point(1276, 55);
            sidebarTabControl.Name = "sidebarTabControl";
            sidebarTabControl.SelectedIndex = 0;
            sidebarTabControl.Size = new Size(212, 820);
            sidebarTabControl.TabIndex = 4;
            // 
            // tilesTabPage
            // 
            tilesTabPage.Controls.Add(listView1);
            tilesTabPage.Location = new Point(4, 24);
            tilesTabPage.Name = "tilesTabPage";
            tilesTabPage.Size = new Size(204, 820);
            tilesTabPage.TabIndex = 0;
            tilesTabPage.Text = "Tiles";
            // 
            // listView1
            // 
            listView1.Dock = DockStyle.Fill;
            listView1.Location = new Point(0, 0);
            listView1.Name = "listView1";
            listView1.Size = new Size(204, 820);
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
            // npcsTabPage
            //
            npcsTabPage.Controls.Add(npcListView);
            npcsTabPage.Controls.Add(npcHintLabel);
            npcsTabPage.Location = new Point(4, 24);
            npcsTabPage.Name = "npcsTabPage";
            npcsTabPage.Size = new Size(204, 614);
            npcsTabPage.TabIndex = 4;
            npcsTabPage.Text = "NPCs";
            //
            // npcListView
            //
            // Real sprite previews from the "Character IDs" folder (extracted separately,
            // filenames are the hex sprite id -- see PopulateNpcList) instead of a bare
            // numeric id input, so picking a character is recognition, not guessing.
            npcListView.Dock = DockStyle.Fill;
            npcListView.Location = new Point(0, 0);
            npcListView.Name = "npcListView";
            npcListView.Size = new Size(204, 514);
            npcListView.TabIndex = 0;
            npcListView.UseCompatibleStateImageBehavior = false;
            npcListView.MouseDown += npcListView_MouseDown;
            //
            // npcHintLabel
            //
            npcHintLabel.Dock = DockStyle.Bottom;
            npcHintLabel.Height = 100;
            npcHintLabel.Name = "npcHintLabel";
            npcHintLabel.Text = "Click a sprite, then click the map to place it. Esc cancels.\r\n\r\nOnly saves on maps that already have at least one character -- new spawn records need an existing one as a template for unconfirmed fields.";
            //
            // propertiesTabPage
            //
            propertiesTabPage.Controls.Add(propertiesGroupBox);
            propertiesTabPage.Location = new Point(4, 24);
            propertiesTabPage.Name = "propertiesTabPage";
            propertiesTabPage.Size = new Size(204, 614);
            propertiesTabPage.TabIndex = 2;
            propertiesTabPage.Text = "Properties";
            //
            // propertiesGroupBox
            //
            propertiesGroupBox.Controls.Add(propertiesLabel);
            propertiesGroupBox.Dock = DockStyle.Top;
            propertiesGroupBox.Height = 300;
            propertiesGroupBox.Name = "propertiesGroupBox";
            propertiesGroupBox.Text = "Properties";
            //
            // propertiesLabel
            //
            propertiesLabel.Dock = DockStyle.Fill;
            propertiesLabel.Name = "propertiesLabel";
            propertiesLabel.Padding = new Padding(6);
            propertiesLabel.Text = "No selection.";
            //
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel });
            statusStrip1.Location = new Point(0, 878);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1500, 22);
            statusStrip1.TabIndex = 6;
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(1485, 17);
            statusLabel.Spring = true;
            statusLabel.Text = "No entity selected";
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // DragonRadarUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1500, 900);
            Controls.Add(sidebarTabControl);
            Controls.Add(mapScrollPanel);
            Controls.Add(mapTreeView);
            Controls.Add(statusStrip1);
            Controls.Add(viewportToolStrip);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            MinimumSize = new Size(900, 500);
            Name = "DragonRadarUI";
            Text = "Dragon Radar";
            mapScrollPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)mapPictureBox).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            viewportToolStrip.ResumeLayout(false);
            viewportToolStrip.PerformLayout();
            sidebarTabControl.ResumeLayout(false);
            tilesTabPage.ResumeLayout(false);
            itemsTabPage.ResumeLayout(false);
            npcsTabPage.ResumeLayout(false);
            propertiesTabPage.ResumeLayout(false);
            propertiesGroupBox.ResumeLayout(false);
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private TreeView mapTreeView;
        private Panel mapScrollPanel;
        private PictureBox mapPictureBox;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fIleToolStripMenuItem;
        private ToolStripMenuItem toolStrip_OpenROM;
        private ToolStripMenuItem toolStrip_SaveROM;
        private ToolStrip viewportToolStrip;
        private ToolStripButton toolStrip_ShowCollision;
        private ToolStripButton toolStrip_RefreshMap;
        private TabControl sidebarTabControl;
        private TabPage tilesTabPage;
        private ListView listView1;
        private TabPage itemsTabPage;
        private ListView itemsListView;
        private TabPage npcsTabPage;
        private ListView npcListView;
        private Label npcHintLabel;
        private TabPage propertiesTabPage;
        private GroupBox propertiesGroupBox;
        private Label propertiesLabel;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
    }
}
