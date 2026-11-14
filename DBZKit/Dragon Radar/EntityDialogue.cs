using DrGero;
using DrGero.Quests;
using System.Text;
using static DrGero.Types.MapEntry;

namespace Dragon_Radar
{
    // Right-click > "View ... dialogue (quest / idle)": everything a sprite can say, read from the ROM and split into
    // the conversations tied to story/quest progress and the ones it says with nothing going on.
    //
    // One dialogue table usually holds several conversations (DrGero.Quests.DialogBranches): a script step at the
    // start tests story flags and sends the conversation to a different run of lines. Each run is listed on its own
    // with the condition that selects it. Sources looked at for one entity:
    //   - its own table: a talkable NPC's record+0xC, an enemy's run-dialog action, an item's dialogSeq, an object's
    //     collectionMsg, a trigger's payload;
    //   - every dialogue trigger zone placed over it. That is how script actors and enemies (which cannot be talked
    //     to themselves) get dialogue, and how cutscenes around an NPC are started.
    public partial class DragonRadarUI
    {
        private sealed record DialogueSource(string Title, int EntityIndex, int SequenceAddress);

        private int ReadPointerAt(int address)
        {
            if (_rom == null || address <= 0 || address + 4 > _rom.Length) return 0;
            _rom.PushPosition(address);
            int value = _rom.ReadPointer();
            _rom.PopPosition();
            return value > 0 && value < _rom.Length ? value : 0;
        }

        /// <summary>File offset of the dialogue table the entity itself owns, or 0.</summary>
        private int OwnDialogueTable(Entity entity)
        {
            if (_rom == null || entity.SourceAddress == 0) return 0;
            int table = 0;
            switch (entity.Kind)
            {
                case EntityKind.Character:
                    if (IsTalkableNpc(entity)) table = ReadPointerAt(entity.SourceAddress + 0xC);
                    break;
                case EntityKind.Enemy:
                    {
                        int action = EnemyRecords.ActionObject(_rom, entity.SourceAddress);
                        if (action > 0 && action + 8 <= _rom.Length && BitConverter.ToInt32(_rom.ReadBytesAt(action, 4), 0) == EnemyRecords.RunDialogFunc)
                            table = ReadPointerAt(action + 4);
                        break;
                    }
                case EntityKind.Item:
                    table = ReadPointerAt(entity.SourceAddress + 0xC); // MapItem.dialogSeq
                    break;
                case EntityKind.Object:
                    table = ResolveObjectScriptPayload(entity)?.ScriptAddress ?? 0;
                    break;
                case EntityKind.Trigger:
                    table = ResolveTriggerScriptPayload(entity)?.ScriptAddress ?? 0;
                    break;
            }
            return table > 0 && DialogBranches.ReachableSlotCount(_rom, table) > 0 ? table : 0;
        }

        private List<DialogueSource> DialogueSourcesFor(int index)
        {
            var sources = new List<DialogueSource>();
            if (_rom == null || index < 0 || index >= _currentEntities.Count) return sources;
            var entity = _currentEntities[index];

            int own = OwnDialogueTable(entity);
            if (own > 0)
            {
                string how = entity.Kind switch
                {
                    EntityKind.Character => "said when you talk to it",
                    EntityKind.Enemy => "its own action",
                    EntityKind.Item => "plays when you interact with it",
                    EntityKind.Object => "plays when you pick it up",
                    _ => "plays when you step in it",
                };
                sources.Add(new DialogueSource($"{DescribeEntityAt(index)} -- {how}", index, own));
            }

            if (entity.Kind is not (EntityKind.Character or EntityKind.Enemy)) return sources;

            // Dialogue triggers over the sprite. Point entities get a body-sized box so a zone drawn around their feet counts.
            // Only zones about the size of a sprite: a cutscene zone covering half the map (Zone 1 Area 1's opening scene
            // is 399x775) is not this sprite's dialogue, it just happens to contain it.
            const int MaxTalkZone = 96;
            var body = entity.Width > 0 || entity.Height > 0
                ? new Rectangle(entity.X, entity.Y, entity.Width, entity.Height)
                : new Rectangle(entity.X - 16, entity.Y - 16, 32, 32);
            for (int i = 0; i < _currentEntities.Count; i++)
            {
                var trigger = _currentEntities[i];
                if (trigger.Kind != EntityKind.Trigger || trigger.Width > MaxTalkZone || trigger.Height > MaxTalkZone) continue;
                if (!new Rectangle(trigger.X, trigger.Y, trigger.Width, trigger.Height).IntersectsWith(body)) continue;
                int table = OwnDialogueTable(trigger);
                if (table > 0 && table != own)
                    sources.Add(new DialogueSource($"{DescribeEntityAt(i)} -- a trigger zone over it", i, table));
            }
            return sources;
        }

        private string QuestNamesFor(IEnumerable<int> flagIds)
        {
            var flags = flagIds.ToHashSet();
            var names = _quests.Where(q => q.FlagIds.Any(flags.Contains))
                .Select(q => $"\"{q.Name}\" (flag {string.Join("/", q.FlagIds.Where(flags.Contains))})").ToList();
            return string.Join(", ", names);
        }

        private string DescribeConversation(DialogBranch branch)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Plays when: {branch.ConditionText}");
            var flags = branch.FlagIds.ToList();
            string quests = QuestNamesFor(flags);
            if (quests.Length > 0) sb.AppendLine($"Quest Log: {quests}");
            else if (flags.Count > 0) sb.AppendLine($"Story flags only ({string.Join(", ", flags)}) -- no Quest Log entry uses them.");
            sb.AppendLine($"Table 0x{0x08000000 | branch.SequenceAddress:X8}, from step {branch.StartSlot + 1}");
            sb.AppendLine();

            if (!branch.Lines.Any()) sb.AppendLine("(no text -- this case only runs script steps)");
            foreach (var step in branch.Steps)
            {
                if (step.IsScript)
                {
                    foreach (var (flag, isSet) in step.FlagWrites)
                    {
                        string q = QuestNamesFor(new[] { flag });
                        sb.AppendLine($"    [script: {(isSet ? "sets" : "clears")} story flag {flag}{(q.Length > 0 ? " -- " + q : "")}]");
                    }
                    continue;
                }
                sb.AppendLine($"Speaker {step.CharacterId}:");
                foreach (string line in step.Text.Split('\n'))
                    sb.AppendLine("    " + line.TrimEnd('\r'));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string ConversationLabel(DialogBranch branch)
        {
            string first = branch.Lines.FirstOrDefault()?.Text.Replace('\n', ' ').Trim() ?? "(script only)";
            if (first.Length > 48) first = first[..48] + "...";
            return branch.Conditions.Count == 0 ? first : $"{first}   [{branch.ConditionText}]";
        }

        /// <summary>
        /// Opens the dialogue editor (portrait preview and all) on the first of the entity's dialogue tables that has
        /// conversations of that kind -- its own, or a dialogue zone over it -- focused on them.
        /// </summary>
        private void OpenDialogueEditor(int index, bool quest)
        {
            if (_rom == null) return;
            foreach (var source in DialogueSourcesFor(index))
            {
                if (!DialogBranches.Analyze(_rom, source.SequenceAddress).Branches.Any(b => b.Lines.Any() && b.IsIdle != quest)) continue;
                SelectEntity(source.EntityIndex);
                var kind = _currentEntities[source.EntityIndex].Kind;
                EditSelectedPayload(dialogue: kind is EntityKind.Character or EntityKind.Enemy, focus: quest ? "quest" : "idle");
                return;
            }
        }

        /// <summary>How many quest and idle conversations the entity has, over all its dialogue sources.</summary>
        private (int Quest, int Idle) CountDialogue(int index)
        {
            int quest = 0, idle = 0;
            if (_rom == null) return (0, 0);
            foreach (var source in DialogueSourcesFor(index))
                foreach (var branch in DialogBranches.Analyze(_rom, source.SequenceAddress).Branches.Where(b => b.Lines.Any()))
                    if (branch.IsIdle) idle++; else quest++;
            return (quest, idle);
        }

        /// <param name="questOnly">true = only quest/story conversations, false = only idle ones, null = both.</param>
        private void ShowEntityDialogue(int index, bool? questOnly = null)
        {
            if (_rom == null) return;
            var sources = DialogueSourcesFor(index);
            if (sources.Count == 0) return;

            string heading = questOnly switch { true => "Quest Dialogue", false => "Idle Dialogue", _ => "Dialogue" };
            using var form = new Form
            {
                Text = $"{heading} -- {DescribeEntityAt(index)}",
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(980, 560),
                MinimumSize = new Size(640, 360),
                ShowIcon = false,
                MinimizeBox = false,
            };
            var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
            form.Load += (_, _) => split.SplitterDistance = 420; // only sticks once the container has its real width
            var tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
            var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10f), BackColor = SystemColors.Window };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var close = new Button { Text = "Close", Width = 90, DialogResult = DialogResult.Cancel };
            var edit = new Button { Text = "Edit all of it...", Width = 130, Enabled = false };
            buttons.Controls.Add(close);
            buttons.Controls.Add(edit);
            split.Panel1.Controls.Add(tree);
            split.Panel2.Controls.Add(text);
            form.Controls.Add(split);
            form.Controls.Add(buttons);
            form.CancelButton = close;

            // Node.Tag: the text to show. Node.Name: the source's entity index (for Edit in Legacy).
            foreach (var source in sources)
            {
                var branches = DialogBranches.Analyze(_rom, source.SequenceAddress).Branches.Where(b => b.Lines.Any()).ToList();
                var quest = branches.Where(b => !b.IsIdle).ToList();
                var idle = branches.Where(b => b.IsIdle).ToList();
                if (questOnly != null && (questOnly.Value ? quest : idle).Count == 0) continue;

                var sourceNode = tree.Nodes.Add(source.EntityIndex.ToString(), source.Title);
                foreach (var (title, list, blurb) in new[]
                {
                    ("Quest dialogue", quest, "Conversations that need a story flag to be set, or that change one."),
                    ("Idle dialogue", idle, "What it says when no story flag is needed and nothing changes."),
                }.Where(g => questOnly == null || ReferenceEquals(g.Item2, quest) == questOnly.Value))
                {
                    var group = sourceNode.Nodes.Add(source.EntityIndex.ToString(), $"{title} ({list.Count})");
                    var all = new StringBuilder($"{title} -- {blurb}\r\n\r\n");
                    if (list.Count == 0) all.AppendLine("None.");
                    foreach (var branch in list)
                    {
                        string described = DescribeConversation(branch);
                        var node = group.Nodes.Add(source.EntityIndex.ToString(), ConversationLabel(branch));
                        node.Tag = described;
                        node.ToolTipText = branch.ConditionText;
                        all.AppendLine(described);
                        all.AppendLine(new string('-', 60));
                    }
                    group.Tag = all.ToString();
                }
                sourceNode.Tag = string.Join("\r\n", sourceNode.Nodes.Cast<TreeNode>().Select(n => (string)n.Tag!));
            }
            tree.ExpandAll();

            int editIndex = -1;
            tree.AfterSelect += (_, e) =>
            {
                text.Text = (e.Node?.Tag as string ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
                editIndex = int.TryParse(e.Node?.Name, out int i) ? i : -1;
                edit.Enabled = editIndex >= 0;
            };
            edit.Click += (_, _) =>
            {
                if (editIndex < 0 || editIndex >= _currentEntities.Count) return;
                var kind = _currentEntities[editIndex].Kind;
                form.Close();
                SelectEntity(editIndex);
                EditSelectedPayload(dialogue: kind is EntityKind.Character or EntityKind.Enemy);
            };
            tree.SelectedNode = tree.Nodes[0];

            form.ShowDialog(this);
        }
    }
}
