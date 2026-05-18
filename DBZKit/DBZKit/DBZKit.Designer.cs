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
            TreeNode treeNode1 = new TreeNode("Area 1");
            TreeNode treeNode2 = new TreeNode("Area 2");
            TreeNode treeNode3 = new TreeNode("Area 3");
            TreeNode treeNode4 = new TreeNode("Area 4");
            TreeNode treeNode5 = new TreeNode("Area 5");
            TreeNode treeNode6 = new TreeNode("Area 6");
            TreeNode treeNode7 = new TreeNode("Area 7");
            TreeNode treeNode8 = new TreeNode("Zone 1", new TreeNode[] { treeNode1, treeNode2, treeNode3, treeNode4, treeNode5, treeNode6, treeNode7 });
            TreeNode treeNode9 = new TreeNode("Zone 2");
            TreeNode treeNode10 = new TreeNode("Zone 3");
            TreeNode treeNode11 = new TreeNode("Zone 4");
            TreeNode treeNode12 = new TreeNode("Zones", new TreeNode[] { treeNode8, treeNode9, treeNode10, treeNode11 });
            DBZKit_TabControl = new TabControl();
            TabPage_PortraitViewer = new TabPage();
            ListView_PortraitViewer = new ListView();
            TapPage_MapViewer = new TabPage();
            TabPage_MiscAssetViewer = new TabPage();
            TreeView_MiscAssetList = new TreeView();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openRomToolStripMenuItem = new ToolStripMenuItem();
            optionsToolStripMenuItem = new ToolStripMenuItem();
            setGameToolStripMenuItem = new ToolStripMenuItem();
            MenuEnableLOG1 = new ToolStripMenuItem();
            MenuEnableLOG2 = new ToolStripMenuItem();
            MenuEnableLOG3 = new ToolStripMenuItem();
            PortraitContextMenu = new ContextMenuStrip(components);
            treeView1 = new TreeView();
            DBZKit_TabControl.SuspendLayout();
            TabPage_PortraitViewer.SuspendLayout();
            TapPage_MapViewer.SuspendLayout();
            TabPage_MiscAssetViewer.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // DBZKit_TabControl
            // 
            DBZKit_TabControl.Controls.Add(TabPage_PortraitViewer);
            DBZKit_TabControl.Controls.Add(TapPage_MapViewer);
            DBZKit_TabControl.Controls.Add(TabPage_MiscAssetViewer);
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
            // TapPage_MapViewer
            // 
            TapPage_MapViewer.Controls.Add(treeView1);
            TapPage_MapViewer.Location = new Point(4, 24);
            TapPage_MapViewer.Name = "TapPage_MapViewer";
            TapPage_MapViewer.Size = new Size(1256, 629);
            TapPage_MapViewer.TabIndex = 2;
            TapPage_MapViewer.Text = "Map Viewer";
            TapPage_MapViewer.UseVisualStyleBackColor = true;
            // 
            // TabPage_MiscAssetViewer
            // 
            TabPage_MiscAssetViewer.Controls.Add(TreeView_MiscAssetList);
            TabPage_MiscAssetViewer.Location = new Point(4, 24);
            TabPage_MiscAssetViewer.Name = "TabPage_MiscAssetViewer";
            TabPage_MiscAssetViewer.Padding = new Padding(3);
            TabPage_MiscAssetViewer.Size = new Size(1256, 629);
            TabPage_MiscAssetViewer.TabIndex = 0;
            TabPage_MiscAssetViewer.Text = "Misc Assets";
            TabPage_MiscAssetViewer.UseVisualStyleBackColor = true;
            // 
            // TreeView_MiscAssetList
            // 
            TreeView_MiscAssetList.Dock = DockStyle.Left;
            TreeView_MiscAssetList.Location = new Point(3, 3);
            TreeView_MiscAssetList.Name = "TreeView_MiscAssetList";
            TreeView_MiscAssetList.Size = new Size(212, 623);
            TreeView_MiscAssetList.TabIndex = 1;
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
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openRomToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "&File";
            // 
            // openRomToolStripMenuItem
            // 
            openRomToolStripMenuItem.Name = "openRomToolStripMenuItem";
            openRomToolStripMenuItem.Size = new Size(131, 22);
            openRomToolStripMenuItem.Text = "&Open Rom";
            openRomToolStripMenuItem.Click += openRomToolStripMenuItem_Click;
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
            // PortraitContextMenu
            // 
            PortraitContextMenu.Name = "PortraitContextMenu";
            PortraitContextMenu.Size = new Size(61, 4);
            // 
            // treeView1
            // 
            treeView1.Dock = DockStyle.Left;
            treeView1.Location = new Point(0, 0);
            treeView1.Name = "treeView1";
            treeNode1.Name = "Area 1";
            treeNode1.Text = "Area 1";
            treeNode2.Name = "Area 2";
            treeNode2.Text = "Area 2";
            treeNode3.Name = "Area 3";
            treeNode3.Text = "Area 3";
            treeNode4.Name = "Area 4";
            treeNode4.Text = "Area 4";
            treeNode5.Name = "Area 5";
            treeNode5.Text = "Area 5";
            treeNode6.Name = "Area 6";
            treeNode6.Text = "Area 6";
            treeNode7.Name = "Area 7";
            treeNode7.Text = "Area 7";
            treeNode8.Name = "Zone 1";
            treeNode8.Text = "Zone 1";
            treeNode9.Name = "Zone 2";
            treeNode9.Text = "Zone 2";
            treeNode10.Name = "Zone 3";
            treeNode10.Text = "Zone 3";
            treeNode11.Name = "Zone 4";
            treeNode11.Text = "Zone 4";
            treeNode12.Name = "Zones";
            treeNode12.Text = "Zones";
            treeView1.Nodes.AddRange(new TreeNode[] { treeNode12 });
            treeView1.Size = new Size(192, 629);
            treeView1.TabIndex = 2;
            // 
            // DBZKit
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1264, 681);
            Controls.Add(DBZKit_TabControl);
            Controls.Add(menuStrip1);
            Name = "DBZKit";
            Text = "DBZ Kit";
            DBZKit_TabControl.ResumeLayout(false);
            TabPage_PortraitViewer.ResumeLayout(false);
            TapPage_MapViewer.ResumeLayout(false);
            TabPage_MiscAssetViewer.ResumeLayout(false);
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
        private ToolStripMenuItem optionsToolStripMenuItem;
        private ToolStripMenuItem setGameToolStripMenuItem;
        private ToolStripMenuItem MenuEnableLOG1;
        private ToolStripMenuItem MenuEnableLOG2;
        private ToolStripMenuItem MenuEnableLOG3;
        private TreeView TreeView_MiscAssetList;
        private ListView ListView_PortraitViewer;
        private ContextMenuStrip PortraitContextMenu;
        private TabPage TapPage_MapViewer;
        private TreeView treeView1;
    }
}
