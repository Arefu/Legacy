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
            mainViewTabControl = new TabControl();
            mapViewerTabPage = new TabPage();
            mapTreeView = new TreeView();
            mapScrollPanel = new Panel();
            mapPictureBox = new PictureBox();
            menuStrip1 = new MenuStrip();
            fIleToolStripMenuItem = new ToolStripMenuItem();
            toolStrip_OpenROM = new ToolStripMenuItem();
            toolStrip_SaveROM = new ToolStripMenuItem();
            toolStrip_SaveROMAs = new ToolStripMenuItem();
            viewportToolStrip = new ToolStrip();
            toolStrip_ShowCollision = new ToolStripButton();
            toolStrip_RefreshMap = new ToolStripButton();
            toolStrip_AddTrigger = new ToolStripButton();
            toolStrip_VariantLabel = new ToolStripLabel();
            toolStrip_VariantCombo = new ToolStripComboBox();
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
            editScriptButton = new Button();
            editDialogueButton = new Button();
            duplicateTriggerButton = new Button();
            addDialogueTriggerButton = new Button();
            npcPlaceModeCombo = new ComboBox();
            deleteTriggerButton = new Button();
            questsTabPage = new TabPage();
            questListView = new ListView();
            questDetailsPanel = new Panel();
            questNameLabel = new Label();
            questNameTextBox = new TextBox();
            questPriorityLabel = new Label();
            questPriorityCombo = new ComboBox();
            questAvailableFlagLabel = new Label();
            questAvailableFlagCombo = new ComboBox();
            questCompleteFlagLabel = new Label();
            questCompleteFlagCombo = new ComboBox();
            questFindFlagUsageButton = new Button();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            mainViewTabControl.SuspendLayout();
            mapViewerTabPage.SuspendLayout();
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
            questsTabPage.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            //
            // mainViewTabControl
            //
            // Top-level tab control alongside the Map Viewer -- added 2026-09-19 so the
            // Quests tab (needs room for name/priority/available/complete/flags columns)
            // isn't squeezed into the narrow right-hand sidebar with everything else.
            mainViewTabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            mainViewTabControl.Controls.Add(mapViewerTabPage);
            mainViewTabControl.Controls.Add(questsTabPage);
            mainViewTabControl.Location = new Point(0, 52);
            mainViewTabControl.Name = "mainViewTabControl";
            mainViewTabControl.SelectedIndex = 0;
            mainViewTabControl.Size = new Size(1500, 826);
            mainViewTabControl.TabIndex = 9;
            //
            // mapViewerTabPage
            //
            // Dock.Fill must be added BEFORE its Dock.Left/Dock.Right siblings -- added
            // last (as it originally was here), it sizes itself to the whole tab page
            // and ignores them, which is exactly the "map renders behind the tree view"
            // bug this fixes. mapScrollPanel (Fill) goes first; mapTreeView (Left) and
            // sidebarTabControl (Right) after it so their edges get carved out of it.
            mapViewerTabPage.Controls.Add(mapScrollPanel);
            mapViewerTabPage.Controls.Add(mapTreeView);
            mapViewerTabPage.Controls.Add(sidebarTabControl);
            mapViewerTabPage.Location = new Point(4, 24);
            mapViewerTabPage.Name = "mapViewerTabPage";
            mapViewerTabPage.Size = new Size(1492, 798);
            mapViewerTabPage.TabIndex = 0;
            mapViewerTabPage.Text = "Map Viewer";
            //
            // mapTreeView
            //
            mapTreeView.Dock = DockStyle.Left;
            mapTreeView.Location = new Point(0, 0);
            mapTreeView.Name = "mapTreeView";
            mapTreeView.Size = new Size(320, 798);
            mapTreeView.TabIndex = 1;
            mapTreeView.AfterSelect += mapTreeView_AfterSelect;
            mapTreeView.BeforeSelect += mapTreeView_BeforeSelect;
            //
            // mapScrollPanel
            //
            mapScrollPanel.Dock = DockStyle.Fill;
            mapScrollPanel.AutoScroll = true;
            mapScrollPanel.BackColor = Color.Black;
            mapScrollPanel.Controls.Add(mapPictureBox);
            mapScrollPanel.Location = new Point(320, 0);
            mapScrollPanel.Name = "mapScrollPanel";
            mapScrollPanel.Size = new Size(960, 798);
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
            fIleToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStrip_OpenROM, toolStrip_SaveROM, toolStrip_SaveROMAs });
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
            toolStrip_SaveROM.Text = "&Save ROM";
            toolStrip_SaveROM.Click += toolStrip_SaveROM_Click;
            //
            // toolStrip_SaveROMAs
            //
            toolStrip_SaveROMAs.Name = "toolStrip_SaveROMAs";
            toolStrip_SaveROMAs.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            toolStrip_SaveROMAs.Size = new Size(193, 22);
            toolStrip_SaveROMAs.Text = "Save ROM &As...";
            toolStrip_SaveROMAs.Click += toolStrip_SaveROMAs_Click;
            //
            // viewportToolStrip
            //
            viewportToolStrip.Items.AddRange(new ToolStripItem[] { toolStrip_ShowCollision, toolStrip_RefreshMap, toolStrip_AddTrigger, new ToolStripSeparator(), toolStrip_VariantLabel, toolStrip_VariantCombo });
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
            toolStrip_ShowCollision.ToolTipText = "Overlay collision (red) on the map: red cells are where you ARE allowed to walk; everything not red is barred";
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
            // toolStrip_AddTrigger
            //
            // CREATE, genuinely from scratch (works even on a map with zero existing
            // triggers, unlike Duplicate) -- confirmed via IDA (2026-09-19) that the two
            // fixed function-pointer values used here are NOT per-trigger; Legacy.cs
            // independently discovered and hardcoded the exact same pair as a filter
            // that finds every dialog/script-capable trigger across all 327 maps, so
            // they're the real, universal condition/function values this trigger style
            // uses -- not a guess. See addTriggerButton... _Click's own comment.
            toolStrip_AddTrigger.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStrip_AddTrigger.Name = "toolStrip_AddTrigger";
            toolStrip_AddTrigger.Size = new Size(97, 25);
            toolStrip_AddTrigger.Text = "Add Trigger";
            toolStrip_AddTrigger.ToolTipText = "Add a brand new trigger to this map, from scratch (no existing trigger needed)";
            toolStrip_AddTrigger.Click += toolStrip_AddTrigger_Click;
            //
            // toolStrip_VariantLabel / toolStrip_VariantCombo
            //
            // Lists every variant (MapEntry.Variation) of the open map's Zone/Area so they can be
            // previewed without hunting through the tree; picking one selects that tree node.
            toolStrip_VariantLabel.Name = "toolStrip_VariantLabel";
            toolStrip_VariantLabel.Text = "Variant:";
            toolStrip_VariantCombo.Name = "toolStrip_VariantCombo";
            toolStrip_VariantCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            toolStrip_VariantCombo.Size = new Size(220, 28);
            toolStrip_VariantCombo.ToolTipText = "Preview the other variants of this map";
            toolStrip_VariantCombo.Enabled = false;
            toolStrip_VariantCombo.SelectedIndexChanged += toolStrip_VariantCombo_SelectedIndexChanged;
            //
            //
            // sidebarTabControl
            //
            sidebarTabControl.Dock = DockStyle.Right;
            sidebarTabControl.Controls.Add(tilesTabPage);
            sidebarTabControl.Controls.Add(itemsTabPage);
            sidebarTabControl.Controls.Add(npcsTabPage);
            sidebarTabControl.Controls.Add(propertiesTabPage);
            sidebarTabControl.Location = new Point(1280, 0);
            sidebarTabControl.Name = "sidebarTabControl";
            sidebarTabControl.SelectedIndex = 0;
            sidebarTabControl.Size = new Size(212, 798);
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
            npcsTabPage.Controls.Add(npcPlaceModeCombo);
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
            npcHintLabel.Text = "Click a sprite, then click the map to place it. Esc cancels.\r\n\r\nStanding/Wandering NPCs are solid and start a conversation when you talk to them (Edit Dialogue changes it). Script actors are for cutscenes: not solid, not talkable. Enemies use a stat/sprite pair the game already ships.";
            //
            // propertiesTabPage
            //
            propertiesTabPage.Controls.Add(deleteTriggerButton);
            propertiesTabPage.Controls.Add(duplicateTriggerButton);
            propertiesTabPage.Controls.Add(addDialogueTriggerButton);
            propertiesTabPage.Controls.Add(editDialogueButton);
            propertiesTabPage.Controls.Add(editScriptButton);
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
            // editScriptButton
            //
            // Only enabled for a selected trigger whose payload resolves to a real
            // script address -- see UpdatePropertiesPanel. Launches Legacy.exe (the real
            // Zenkai IDE) against a temp copy of the current in-memory ROM, auto-focused
            // on this trigger's Zone/Area/conversation -- see LaunchLegacyForSelectedTrigger.
            editScriptButton.Dock = DockStyle.Top;
            editScriptButton.Height = 32;
            editScriptButton.Name = "editScriptButton";
            editScriptButton.Text = "Edit Script...";
            editScriptButton.Enabled = false;
            editScriptButton.Click += editScriptButton_Click;
            //
            // editDialogueButton
            //
            // Same trigger, same Legacy launch as Edit Script above -- Legacy shows it as
            // a conversation or a script depending on which tab you land on, since the
            // underlying trigger is a valid target for either. Enabled under the same
            // condition as Edit Script (any resolvable payload).
            editDialogueButton.Dock = DockStyle.Top;
            editDialogueButton.Height = 32;
            editDialogueButton.Name = "editDialogueButton";
            editDialogueButton.Text = "Edit Dialogue...";
            editDialogueButton.Enabled = false;
            editDialogueButton.Click += editDialogueButton_Click;
            //
            // addDialogueTriggerButton
            //
            // The ONE confirmed way an NPC/enemy gets dialogue (IDA 2026-09-19: no interact
            // handler on any character entity reaches a conversation -- see
            // NPC-Dialog-and-Quests.md): a Dialog trigger zone placed over it. This builds that
            // trigger around the selected NPC/enemy and opens its dialogue editor.
            addDialogueTriggerButton.Dock = DockStyle.Top;
            addDialogueTriggerButton.Height = 32;
            addDialogueTriggerButton.Name = "addDialogueTriggerButton";
            addDialogueTriggerButton.Text = "Add Dialogue Trigger Here";
            addDialogueTriggerButton.Enabled = false;
            addDialogueTriggerButton.Click += addDialogueTriggerButton_Click;
            //
            // npcPlaceModeCombo
            //
            npcPlaceModeCombo.Dock = DockStyle.Top;
            npcPlaceModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            npcPlaceModeCombo.Items.AddRange(new object[] { "Place: Standing NPCs (talk)", "Place: Wandering NPCs (talk)", "Place: Script actors", "Place: Enemies" });
            npcPlaceModeCombo.Name = "npcPlaceModeCombo";
            npcPlaceModeCombo.SelectedIndex = 0;
            npcPlaceModeCombo.SelectedIndexChanged += npcPlaceModeCombo_SelectedIndexChanged;
            //
            // duplicateTriggerButton
            //
            // CREATE: clones the selected trigger's full record (condition/function
            // pointers copied VERBATIM -- see DuplicateTriggerButton_Click for why: we
            // don't need to understand what they do to reuse a known-working pair) into
            // free space, appends it as a new slot in this map's trigger array, and
            // selects it so you can move it and Edit Script its payload.
            duplicateTriggerButton.Dock = DockStyle.Top;
            duplicateTriggerButton.Height = 32;
            duplicateTriggerButton.Name = "duplicateTriggerButton";
            duplicateTriggerButton.Text = "Duplicate Trigger (New)";
            duplicateTriggerButton.Enabled = false;
            duplicateTriggerButton.Click += duplicateTriggerButton_Click;
            //
            // deleteTriggerButton
            //
            // DELETE: removes the selected trigger's slot from this map's trigger array
            // (the array is rebuilt one slot shorter in free space; the old array and the
            // deleted trigger's own record are just left as unreachable bytes, same as
            // every other "delete" in this codebase -- no reclaim).
            deleteTriggerButton.Dock = DockStyle.Top;
            deleteTriggerButton.Height = 32;
            deleteTriggerButton.Name = "deleteTriggerButton";
            deleteTriggerButton.Text = "Delete Trigger";
            deleteTriggerButton.Enabled = false;
            deleteTriggerButton.Click += deleteTriggerButton_Click;
            //
            // questsTabPage
            //
            // Top-level tab (peer of Map Viewer, in mainViewTabControl) -- moved out of
            // the narrow sidebarTabControl 2026-09-19 so the quest list has room to show
            // its name/available/complete/flags columns without truncation.
            //
            // Fill (questListView) added BEFORE the Right-docked details panel -- a
            // Dock.Fill control must be added first or it sizes itself to the whole parent
            // and paints over its Right/Left siblings (the exact bug fixed earlier this
            // session in the map viewer's own tab).
            questsTabPage.Controls.Add(questListView);
            questsTabPage.Controls.Add(questDetailsPanel);
            questsTabPage.Location = new Point(4, 24);
            questsTabPage.Name = "questsTabPage";
            questsTabPage.Size = new Size(1492, 798);
            questsTabPage.TabIndex = 1;
            questsTabPage.Text = "Quests";
            //
            // questListView
            //
            // The game's full quest log (43 entries -- see DrGero.Quests.QuestReader),
            // decoded straight from the live ROM: name, priority icon index, and the
            // "available"/"complete" condition each entry checks, expressed as flag
            // tests (e.g. "Flag(191)") rather than raw bytecode.
            questListView.Dock = DockStyle.Fill;
            questListView.View = View.Details;
            questListView.FullRowSelect = true;
            questListView.GridLines = true;
            questListView.Columns.Add("#", 40);
            questListView.Columns.Add("Priority", 60);
            questListView.Columns.Add("Name", 480);
            questListView.Columns.Add("Available when", 400);
            questListView.Columns.Add("Complete when", 400);
            questListView.Columns.Add("Flag ids", 180);
            questListView.Location = new Point(0, 0);
            questListView.Name = "questListView";
            questListView.Size = new Size(1492, 798);
            questListView.TabIndex = 0;
            questListView.UseCompatibleStateImageBehavior = false;
            questListView.SelectedIndexChanged += questListView_SelectedIndexChanged;
            //
            // questDetailsPanel
            //
            // Editor for the currently-selected quest -- Name/Priority write back
            // immediately (no selection required elsewhere, matches every other edit in
            // this app: no "saved!" popup, it just does it). The two flag editors are only
            // enabled when that quest's available/complete condition is the bare single-
            // flag "Flag(N)" shape QuestWriter.WriteSingleFlagTest can safely regenerate --
            // anything more complex (multi-flag AND, NOT) shows read-only instead of
            // silently destroying it.
            questDetailsPanel.Dock = DockStyle.Right;
            questDetailsPanel.Width = 320;
            questDetailsPanel.Name = "questDetailsPanel";
            questDetailsPanel.Padding = new Padding(8);
            questDetailsPanel.Controls.Add(questFindFlagUsageButton);
            questDetailsPanel.Controls.Add(questCompleteFlagCombo);
            questDetailsPanel.Controls.Add(questCompleteFlagLabel);
            questDetailsPanel.Controls.Add(questAvailableFlagCombo);
            questDetailsPanel.Controls.Add(questAvailableFlagLabel);
            questDetailsPanel.Controls.Add(questPriorityCombo);
            questDetailsPanel.Controls.Add(questPriorityLabel);
            questDetailsPanel.Controls.Add(questNameTextBox);
            questDetailsPanel.Controls.Add(questNameLabel);
            //
            // questNameLabel / questNameTextBox
            //
            questNameLabel.Dock = DockStyle.Top;
            questNameLabel.Text = "Name";
            questNameLabel.Height = 20;
            questNameTextBox.Dock = DockStyle.Top;
            questNameTextBox.Name = "questNameTextBox";
            questNameTextBox.Enabled = false;
            questNameTextBox.Leave += questNameTextBox_Leave;
            //
            // questPriorityLabel / questPriorityCombo
            //
            questPriorityLabel.Dock = DockStyle.Top;
            questPriorityLabel.Text = "Priority";
            questPriorityLabel.Height = 20;
            questPriorityCombo.Dock = DockStyle.Top;
            questPriorityCombo.Name = "questPriorityCombo";
            questPriorityCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            questPriorityCombo.Items.AddRange(new object[] { "0", "1", "2" });
            questPriorityCombo.Enabled = false;
            questPriorityCombo.SelectedIndexChanged += questPriorityCombo_SelectedIndexChanged;
            //
            // questAvailableFlagLabel / questAvailableFlagCombo
            //
            questAvailableFlagLabel.Dock = DockStyle.Top;
            questAvailableFlagLabel.Text = "Available when flag =";
            questAvailableFlagLabel.Height = 20;
            questAvailableFlagCombo.Dock = DockStyle.Top;
            questAvailableFlagCombo.Name = "questAvailableFlagCombo";
            questAvailableFlagCombo.DropDownStyle = ComboBoxStyle.DropDown;
            questAvailableFlagCombo.Enabled = false;
            questAvailableFlagCombo.Leave += questAvailableFlagCombo_Leave;
            //
            // questCompleteFlagLabel / questCompleteFlagCombo
            //
            questCompleteFlagLabel.Dock = DockStyle.Top;
            questCompleteFlagLabel.Text = "Complete when flag =";
            questCompleteFlagLabel.Height = 20;
            questCompleteFlagCombo.Dock = DockStyle.Top;
            questCompleteFlagCombo.Name = "questCompleteFlagCombo";
            questCompleteFlagCombo.DropDownStyle = ComboBoxStyle.DropDown;
            questCompleteFlagCombo.Enabled = false;
            questCompleteFlagCombo.Leave += questCompleteFlagCombo_Leave;
            //
            // questFindFlagUsageButton
            //
            questFindFlagUsageButton.Dock = DockStyle.Top;
            questFindFlagUsageButton.Height = 32;
            questFindFlagUsageButton.Text = "Find Flag Usage...";
            questFindFlagUsageButton.Enabled = false;
            questFindFlagUsageButton.Click += questFindFlagUsageButton_Click;
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
            Controls.Add(mainViewTabControl);
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
            questsTabPage.ResumeLayout(false);
            mapViewerTabPage.ResumeLayout(false);
            mainViewTabControl.ResumeLayout(false);
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private TabControl mainViewTabControl;
        private TabPage mapViewerTabPage;
        private TreeView mapTreeView;
        private Panel mapScrollPanel;
        private PictureBox mapPictureBox;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fIleToolStripMenuItem;
        private ToolStripMenuItem toolStrip_OpenROM;
        private ToolStripMenuItem toolStrip_SaveROM;
        private ToolStripMenuItem toolStrip_SaveROMAs;
        private ToolStrip viewportToolStrip;
        private ToolStripButton toolStrip_ShowCollision;
        private ToolStripButton toolStrip_RefreshMap;
        private ToolStripButton toolStrip_AddTrigger;
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
        private Button editScriptButton;
        private Button editDialogueButton;
        private Button duplicateTriggerButton;
        private Button deleteTriggerButton;
        private TabPage questsTabPage;
        private ListView questListView;
        private ToolStripLabel toolStrip_VariantLabel;
        private ToolStripComboBox toolStrip_VariantCombo;
        private Button addDialogueTriggerButton;
        private ComboBox npcPlaceModeCombo;
        private Panel questDetailsPanel;
        private Label questNameLabel;
        private TextBox questNameTextBox;
        private Label questPriorityLabel;
        private ComboBox questPriorityCombo;
        private Label questAvailableFlagLabel;
        private ComboBox questAvailableFlagCombo;
        private Label questCompleteFlagLabel;
        private ComboBox questCompleteFlagCombo;
        private Button questFindFlagUsageButton;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
    }
}
