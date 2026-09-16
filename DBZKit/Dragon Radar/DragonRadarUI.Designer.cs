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
            toolStrip_ShowPreviewTab = new ToolStripButton();
            sidebarTabControl = new TabControl();
            tilesTabPage = new TabPage();
            listView1 = new ListView();
            itemsTabPage = new TabPage();
            itemsListView = new ListView();
            previewTabPage = new TabPage();
            previewPictureBox = new PictureBox();
            previewCoordLabel = new Label();
            previewCollisionCheckBox = new CheckBox();
            previewHintLabel = new Label();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            mapScrollPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)mapPictureBox).BeginInit();
            menuStrip1.SuspendLayout();
            viewportToolStrip.SuspendLayout();
            sidebarTabControl.SuspendLayout();
            tilesTabPage.SuspendLayout();
            itemsTabPage.SuspendLayout();
            previewTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).BeginInit();
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
            viewportToolStrip.Items.AddRange(new ToolStripItem[] { toolStrip_ShowCollision, toolStrip_ShowPreviewTab });
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
            toolStrip_ShowCollision.ToolTipText = "Overlay solid/blocked tiles (red) on the map and GBA viewport preview";
            toolStrip_ShowCollision.CheckedChanged += toolStrip_ShowCollision_CheckedChanged;
            //
            // toolStrip_ShowPreviewTab
            //
            toolStrip_ShowPreviewTab.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStrip_ShowPreviewTab.Name = "toolStrip_ShowPreviewTab";
            toolStrip_ShowPreviewTab.Size = new Size(100, 25);
            toolStrip_ShowPreviewTab.Text = "GBA Preview >>";
            toolStrip_ShowPreviewTab.ToolTipText = "Jump to the GBA-screen viewport preview tab";
            toolStrip_ShowPreviewTab.Click += toolStrip_ShowPreviewTab_Click;
            //
            // sidebarTabControl
            //
            sidebarTabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            sidebarTabControl.Controls.Add(tilesTabPage);
            sidebarTabControl.Controls.Add(itemsTabPage);
            sidebarTabControl.Controls.Add(previewTabPage);
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
            // previewTabPage
            //
            previewTabPage.Controls.Add(previewHintLabel);
            previewTabPage.Controls.Add(previewCollisionCheckBox);
            previewTabPage.Controls.Add(previewCoordLabel);
            previewTabPage.Controls.Add(previewPictureBox);
            previewTabPage.Location = new Point(4, 24);
            previewTabPage.Name = "previewTabPage";
            previewTabPage.Size = new Size(204, 614);
            previewTabPage.TabIndex = 2;
            previewTabPage.Text = "GBA Preview";
            //
            // previewPictureBox
            //
            // Shows a 240x160 crop (the real GBA screen resolution) of the fully-rendered map,
            // at the position the draggable viewport rectangle on the main map view is
            // currently over -- i.e. what you'd actually see on hardware at that scroll
            // position. SizeMode=Zoom fits it into the sidebar's available width while keeping
            // the real 240:160 aspect ratio (no distortion), even though it's necessarily shown
            // smaller than 1:1 pixels in this panel.
            previewPictureBox.BackColor = Color.Black;
            previewPictureBox.BorderStyle = BorderStyle.FixedSingle;
            previewPictureBox.Dock = DockStyle.Top;
            previewPictureBox.Height = 200;
            previewPictureBox.Name = "previewPictureBox";
            previewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewPictureBox.TabStop = false;
            //
            // previewCoordLabel
            //
            previewCoordLabel.Dock = DockStyle.Top;
            previewCoordLabel.Height = 20;
            previewCoordLabel.Name = "previewCoordLabel";
            previewCoordLabel.Text = "Viewport: (0, 0)";
            previewCoordLabel.TextAlign = ContentAlignment.MiddleCenter;
            //
            // previewCollisionCheckBox
            //
            previewCollisionCheckBox.Dock = DockStyle.Top;
            previewCollisionCheckBox.Height = 24;
            previewCollisionCheckBox.Name = "previewCollisionCheckBox";
            previewCollisionCheckBox.Text = "Show Collision";
            previewCollisionCheckBox.CheckedChanged += previewCollisionCheckBox_CheckedChanged;
            //
            // previewHintLabel
            //
            previewHintLabel.Dock = DockStyle.Top;
            previewHintLabel.Height = 60;
            previewHintLabel.Name = "previewHintLabel";
            previewHintLabel.Text = "Drag on the map with the right mouse button to move the GBA-screen (240x160) viewport shown here.";
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
            previewTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).EndInit();
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
        private ToolStripButton toolStrip_ShowPreviewTab;
        private TabControl sidebarTabControl;
        private TabPage tilesTabPage;
        private ListView listView1;
        private TabPage itemsTabPage;
        private ListView itemsListView;
        private TabPage previewTabPage;
        private PictureBox previewPictureBox;
        private Label previewCoordLabel;
        private CheckBox previewCollisionCheckBox;
        private Label previewHintLabel;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
    }
}
