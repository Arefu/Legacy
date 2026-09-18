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
            sidebarTabControl = new TabControl();
            tilesTabPage = new TabPage();
            listView1 = new ListView();
            itemsTabPage = new TabPage();
            itemsListView = new ListView();
            objectsTabPage = new TabPage();
            objectsListView = new ListView();
            colObjZone = new ColumnHeader();
            colObjArea = new ColumnHeader();
            colObjMap = new ColumnHeader();
            colObjItemId = new ColumnHeader();
            colObjX = new ColumnHeader();
            colObjY = new ColumnHeader();
            objectsButtonPanel = new Panel();
            objectsGoToButton = new Button();
            objectsPlaceNewButton = new Button();
            npcsTabPage = new TabPage();
            npcSpriteIdLabel = new Label();
            npcSpriteIdInput = new NumericUpDown();
            npcPlaceButton = new Button();
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
            objectsTabPage.SuspendLayout();
            objectsButtonPanel.SuspendLayout();
            npcsTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)npcSpriteIdInput).BeginInit();
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
            viewportToolStrip.Items.AddRange(new ToolStripItem[] { toolStrip_ShowCollision });
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
            // sidebarTabControl
            //
            sidebarTabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            sidebarTabControl.Controls.Add(tilesTabPage);
            sidebarTabControl.Controls.Add(itemsTabPage);
            sidebarTabControl.Controls.Add(objectsTabPage);
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
            // objectsTabPage
            //
            objectsTabPage.Controls.Add(objectsListView);
            objectsTabPage.Controls.Add(objectsButtonPanel);
            objectsTabPage.Location = new Point(4, 24);
            objectsTabPage.Name = "objectsTabPage";
            objectsTabPage.Size = new Size(204, 614);
            objectsTabPage.TabIndex = 3;
            objectsTabPage.Text = "Objects";
            //
            // objectsListView
            //
            // Every EntityKind.Object across every map, scanned up front -- see
            // PopulateObjectsList -- not just whatever's on the currently-open map.
            objectsListView.Columns.AddRange(new ColumnHeader[] { colObjZone, colObjArea, colObjMap, colObjItemId, colObjX, colObjY });
            objectsListView.Dock = DockStyle.Fill;
            objectsListView.FullRowSelect = true;
            objectsListView.GridLines = true;
            objectsListView.Location = new Point(0, 0);
            objectsListView.MultiSelect = false;
            objectsListView.Name = "objectsListView";
            objectsListView.Size = new Size(204, 574);
            objectsListView.TabIndex = 0;
            objectsListView.UseCompatibleStateImageBehavior = false;
            objectsListView.View = View.Details;
            objectsListView.MouseDoubleClick += objectsListView_MouseDoubleClick;
            //
            // colObjZone
            //
            colObjZone.Text = "Zn";
            colObjZone.Width = 30;
            //
            // colObjArea
            //
            colObjArea.Text = "Ar";
            colObjArea.Width = 30;
            //
            // colObjMap
            //
            colObjMap.Text = "Map";
            colObjMap.Width = 70;
            //
            // colObjItemId
            //
            colObjItemId.Text = "Item";
            colObjItemId.Width = 34;
            //
            // colObjX
            //
            colObjX.Text = "X";
            colObjX.Width = 40;
            //
            // colObjY
            //
            colObjY.Text = "Y";
            colObjY.Width = 40;
            //
            // objectsButtonPanel
            //
            objectsButtonPanel.Controls.Add(objectsGoToButton);
            objectsButtonPanel.Controls.Add(objectsPlaceNewButton);
            objectsButtonPanel.Dock = DockStyle.Bottom;
            objectsButtonPanel.Height = 40;
            objectsButtonPanel.Name = "objectsButtonPanel";
            //
            // objectsGoToButton
            //
            objectsGoToButton.Location = new Point(4, 6);
            objectsGoToButton.Name = "objectsGoToButton";
            objectsGoToButton.Size = new Size(95, 28);
            objectsGoToButton.Text = "Go To";
            objectsGoToButton.UseVisualStyleBackColor = true;
            objectsGoToButton.Click += objectsGoToButton_Click;
            //
            // objectsPlaceNewButton
            //
            objectsPlaceNewButton.Location = new Point(104, 6);
            objectsPlaceNewButton.Name = "objectsPlaceNewButton";
            objectsPlaceNewButton.Size = new Size(95, 28);
            objectsPlaceNewButton.Text = "Place New";
            objectsPlaceNewButton.UseVisualStyleBackColor = true;
            objectsPlaceNewButton.Click += objectsPlaceNewButton_Click;
            //
            // npcsTabPage
            //
            npcsTabPage.Controls.Add(npcHintLabel);
            npcsTabPage.Controls.Add(npcPlaceButton);
            npcsTabPage.Controls.Add(npcSpriteIdInput);
            npcsTabPage.Controls.Add(npcSpriteIdLabel);
            npcsTabPage.Location = new Point(4, 24);
            npcsTabPage.Name = "npcsTabPage";
            npcsTabPage.Size = new Size(204, 614);
            npcsTabPage.TabIndex = 4;
            npcsTabPage.Text = "NPCs";
            //
            // npcSpriteIdLabel
            //
            npcSpriteIdLabel.AutoSize = true;
            npcSpriteIdLabel.Location = new Point(8, 12);
            npcSpriteIdLabel.Name = "npcSpriteIdLabel";
            npcSpriteIdLabel.Size = new Size(60, 15);
            npcSpriteIdLabel.Text = "Sprite ID:";
            //
            // npcSpriteIdInput
            //
            npcSpriteIdInput.Location = new Point(8, 30);
            npcSpriteIdInput.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
            npcSpriteIdInput.Name = "npcSpriteIdInput";
            npcSpriteIdInput.Size = new Size(120, 23);
            //
            // npcPlaceButton
            //
            npcPlaceButton.Location = new Point(8, 60);
            npcPlaceButton.Name = "npcPlaceButton";
            npcPlaceButton.Size = new Size(120, 28);
            npcPlaceButton.Text = "Place NPC";
            npcPlaceButton.UseVisualStyleBackColor = true;
          //  npcPlaceButton.Click += npcPlaceButton_Click;
            //
            // npcHintLabel
            //
            npcHintLabel.Location = new Point(8, 96);
            npcHintLabel.MaximumSize = new Size(188, 0);
            npcHintLabel.Name = "npcHintLabel";
            npcHintLabel.Size = new Size(188, 100);
            npcHintLabel.Text = "Click the map to place a new NPC with this sprite ID. Esc cancels.\r\n\r\nOnly saves on maps that already have at least one character -- new spawn records need an existing one as a template for unconfirmed fields.";
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
            objectsTabPage.ResumeLayout(false);
            objectsButtonPanel.ResumeLayout(false);
            npcsTabPage.ResumeLayout(false);
            npcsTabPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)npcSpriteIdInput).EndInit();
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
        private TabControl sidebarTabControl;
        private TabPage tilesTabPage;
        private ListView listView1;
        private TabPage itemsTabPage;
        private ListView itemsListView;
        private TabPage objectsTabPage;
        private ListView objectsListView;
        private ColumnHeader colObjZone;
        private ColumnHeader colObjArea;
        private ColumnHeader colObjMap;
        private ColumnHeader colObjItemId;
        private ColumnHeader colObjX;
        private ColumnHeader colObjY;
        private Panel objectsButtonPanel;
        private Button objectsGoToButton;
        private Button objectsPlaceNewButton;
        private TabPage npcsTabPage;
        private Label npcSpriteIdLabel;
        private NumericUpDown npcSpriteIdInput;
        private Button npcPlaceButton;
        private Label npcHintLabel;
        private TabPage propertiesTabPage;
        private GroupBox propertiesGroupBox;
        private Label propertiesLabel;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
    }
}
