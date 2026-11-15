using DrGero.IO;
using DrGero.Quests;
using Legacy.Zenkai;

namespace Legacy
{
    /// <summary>
    /// The micro editor entry point: Legacy.exe --micro-edit --rom=&lt;temp copy&gt; --kind=&lt;trigger|object|npc&gt;
    /// --field=0x&lt;file offset&gt; [--title="..."]. Launched by Dragon Radar (Edit Script, Edit Dialogue,
    /// right-click "Edit X code...", Custom pickup) instead of the full IDE.
    ///
    /// --field is the FILE OFFSET of the 4-byte field being edited:
    ///   trigger / object: the payload pointer (a trigger's dataPtr+0x10, an object's collectionMsg
    ///     +0x14). Points at a dialog sequence[] (objects always; triggers when it looks like one) or,
    ///     for some triggers, plain bytecode.
    ///   dialog: a plain pointer to a dialog sequence[] (a talkable NPC record's +0xC); same handling as object.
    ///   enemy: the payload pointer of the enemy's action object {func, payload} (field-4 is func).
    ///     func 0x080106B3 = payload is a dialog sequence[]; 0x0801069B = payload is plain bytecode.
    ///     Applying sets func to match what was authored (EnemyAction_RunDialog / _RunScript).
    ///   npc: the spawn-condition value (record+4) -- null = always spawns, 0x0001xxxx / 0x0002xxxx =
    ///     story flag xxxx set / not set, otherwise a pointer to a bytecode script (confirmed via IDA,
    ///     Condition_Evaluate @0x8003F06).
    /// Every Apply writes the new data to free space, repoints the field, and saves the ROM file --
    /// Dragon Radar watches that file and reloads, so nothing needs closing.
    /// </summary>
    internal static class MicroEditor
    {
        public static void Run(string[] args)
        {
            string? Arg(string name) => args.FirstOrDefault(a => a.StartsWith(name + "="))?[(name.Length + 1)..].Trim('"');

            string? romPath = Arg("--rom"), kind = Arg("--kind"), fieldText = Arg("--field");
            string title = Arg("--title") ?? "Edit";

            if (romPath == null || kind == null || fieldText == null || !File.Exists(romPath)
                || !int.TryParse(fieldText.Replace("0x", "", StringComparison.OrdinalIgnoreCase), System.Globalization.NumberStyles.HexNumber, null, out int field))
            {
                MessageBox.Show("--micro-edit needs --rom=<existing file>, --kind=<trigger|object|npc|enemy|dialog> and --field=0x<offset>.", "Legacy");
                return;
            }

            var rom = ROM.FromFile(romPath);
            var steps = new List<PayloadEditorForm.Step>();
            string? warning = null;
            bool rawScript;

            static string Disassemble(byte[] bytes)
            {
                try { return ZenkaiDisassembler.Disassemble(bytes); }
                catch (Exception ex) { return $"// couldn't disassemble: {ex.Message}"; }
            }

            // True when the script text says everything the bytes do: assembling it gives the same instructions in the
            // same order (jumps and ENDs aside -- `if (...) { return 3; }` assembles with an END of its own). Scripts
            // that fail this (sums of flag tests, type-handler ops: no Zenkai syntax yet) are shown read-only.
            static bool IsFaithful(byte[] bytes, string source)
            {
                static string Shape(byte[] b) => string.Join(";", InstructionDecoder.Decode(b)
                    .Where(i => i.Opcode is not (0x11 or 0x12 or 0x13 or 0x1C))
                    .SelectMany(i => i.Opcode is 0x00 or 0x01 or 0x1D ? i.Args.Select(a => $"push {a}") : new[] { $"{i.Opcode:X2}.{i.StepIndex}.{string.Join(",", i.Args)}" }));
                try { return Shape(ZenkaiAssembler.Assemble(source)) == Shape(bytes); }
                catch { return false; }
            }

            if (kind == "npc")
            {
                rawScript = true;
                rom.PushPosition(field);
                uint cond = (uint)rom.ReadInt();
                rom.PopPosition();

                uint type = cond >> 16;
                string text;
                if (cond == 0)
                    text = "// No condition: this always appears.\r\n// To gate it, end with a value that is true when it should appear, e.g.:\r\n//   StackTestStoryFlag(191);\r\n";
                else if (type == 1)
                    text = $"StackTestStoryFlag({cond & 0xFFFF});";
                else if (type == 2)
                    text = $"StackTestStoryFlagNot({cond & 0xFFFF});";
                else if (type == 3)
                {
                    text = "// (one-shot flag test: shows once, then clears its flag)";
                    warning = $"This uses a one-shot flag test on flag {cond & 0xFFFF}, which has no script form here. Applying replaces it.";
                }
                else if ((cond & 0xFF000000) == 0x08000000 && (int)(cond & 0x00FFFFFF) < rom.Length)
                {
                    int scriptAddress = (int)(cond & 0x00FFFFFF);
                    text = Disassemble(rom.ReadBytesAt(scriptAddress, Math.Min(512, rom.Length - scriptAddress)));
                }
                else
                {
                    text = $"// Unrecognised spawn condition value 0x{cond:X8}.";
                    warning = "This condition value isn't a flag test or a ROM script pointer. Applying replaces it.";
                }
                steps.Add(new PayloadEditorForm.Step { IsScript = true, Text = text });
            }
            else
            {
                rom.PushPosition(field);
                int address = rom.ReadPointer(); // plain file offset (0 = nothing there yet)
                rom.PopPosition();
                if (address >= rom.Length)
                {
                    MessageBox.Show($"The value at 0x{field:X} doesn't point inside the ROM (0x{address:X}), so there is no script or dialogue here to edit.", title);
                    return;
                }

                int enemyFunc = 0;
                if (kind == "enemy")
                {
                    rom.PushPosition(field - 4);
                    enemyFunc = rom.ReadInt();
                    rom.PopPosition();
                    // Only the run-dialog and run-script actions carry a payload; anything else (the
                    // do-nothing action) starts as an empty script.
                    if (enemyFunc != DrGero.EnemyRecords.RunDialogFunc && enemyFunc != DrGero.EnemyRecords.RunScriptFunc)
                        address = 0;
                }

                bool isSequence = kind == "object" || kind == "dialog" || address <= 0
                    || (kind == "enemy" ? enemyFunc == DrGero.EnemyRecords.RunDialogFunc : DialogScanner.LooksLikeDialogSequence(rom, address));
                rawScript = !isSequence;

                string? tableProblem = null;
                if (address <= 0)
                {
                    steps.Add(new PayloadEditorForm.Step { IsScript = true }); // nothing yet -- start with an empty script
                }
                else if (isSequence && DialogTableReader.Read(rom, address, out tableProblem) is { } table)
                {
                    // The whole table: every conversation in it, end markers included (see DialogBranches). Each step
                    // remembers where its entry is, so applying only rewrites the steps that changed or moved.
                    foreach (var slot in table)
                    {
                        if (slot.IsEnd) { steps.Add(new PayloadEditorForm.Step { IsEnd = true, EndRaw = slot.EndValue }); continue; }

                        PayloadEditorForm.Step step;
                        if (slot.RawEntry != null)
                            step = new PayloadEditorForm.Step { RawEntry = slot.RawEntry, JumpTarget = slot.JumpTarget };
                        else if (slot.Line!.Kind == DialogLineKind.Script)
                        {
                            string source = Disassemble(slot.Line.ScriptBytes);
                            step = new PayloadEditorForm.Step
                            {
                                IsScript = true, Text = source, BaselineText = source,
                                OriginalBytes = (byte[])slot.Line.ScriptBytes.Clone(),
                                ScriptReadOnly = !IsFaithful(slot.Line.ScriptBytes, source),
                            };
                        }
                        else
                        {
                            var (plain, position, center) = DialogPreview.ExtractFormat(slot.Line.Text);
                            step = new PayloadEditorForm.Step
                            {
                                Character = slot.Line.CharacterId, Text = plain.Replace("\n", "\r\n"), Position = position, Center = center,
                                ArgScript = slot.Line.Kind == DialogLineKind.Interpolated ? slot.Line.ScriptBytes : null,
                            };
                        }
                        step.OriginalEntryOffset = slot.EntryOffset;
                        step.OriginalKey = step.Key();
                        steps.Add(step);
                    }
                    // An end marker naming a step: the next talk starts there (follows it when steps move).
                    foreach (var end in steps.Where(s => s.IsEnd && s.EndRaw > 0 && s.EndRaw < steps.Count && !steps[(int)s.EndRaw].IsEnd))
                        end.NextTalk = steps[(int)end.EndRaw];
                    // An entry that does not simply run on to the next step (2 in the whole US ROM) keeps where it goes.
                    for (int i = 0; i < table.Count; i++)
                        if (!table[i].IsEnd && table[i].Index != i + 1 && table[i].Index < steps.Count)
                            steps[i].Next = steps[table[i].Index];

                    // --focus=quest|idle (Dragon Radar's Quest / Idle Dialogue): mark that kind's steps; the editor
                    // opens on the first of them and greys out the rest. One step per slot, so slot = step index.
                    if (Arg("--focus") is "quest" or "idle")
                    {
                        bool wantIdle = Arg("--focus") == "idle";
                        foreach (var branch in DialogBranches.Analyze(rom, address).Branches.Where(b => b.IsIdle == wantIdle))
                            foreach (var s in branch.Steps.Where(s => s.SequenceAddress == address && s.Slot < steps.Count))
                                steps[s.Slot].Highlighted = true;
                    }
                }
                else if (isSequence)
                {
                    // Couldn't be read as a whole table: fall back to the plain run of steps up to the first end marker.
                    var read = DialogReader.Read(rom, address);
                    bool lossy = false;
                    foreach (var line in read.Lines)
                    {
                        if (line.Kind == DialogLineKind.Script)
                            steps.Add(new PayloadEditorForm.Step { IsScript = true, Text = Disassemble(line.ScriptBytes) });
                        else
                        {
                        {
                            var (plain, position, center) = DialogPreview.ExtractFormat(line.Text);
                            steps.Add(new PayloadEditorForm.Step { Character = line.CharacterId, Text = plain, Position = position, Center = center });
                        }
                            lossy |= line.Kind == DialogLineKind.Interpolated;
                        }
                    }
                    if (!read.FullyUnderstood)
                        warning = $"{read.Note ?? "Part of this conversation can't be represented here."} Applying replaces everything from that point on.";
                    else if (lossy)
                        warning = "Some lines fill in values (%s/%d) -- they're shown as plain text and applying drops the filling.";

                    // Only the first conversation got read here; a table with more of them would lose the rest on Apply.
                    int conversations = DialogBranches.Analyze(rom, address).Branches.Count(b => b.Steps.Count > 0);
                    if (conversations > 1)
                        warning = $"This holds {conversations} separate conversations but could only be read up to the first end ({tableProblem}). "
                            + "Applying replaces ALL of them with what is shown. To change one line and keep the rest, edit it in Legacy's main tree instead."
                            + (warning == null ? "" : " " + warning);
                }
                else
                {
                    steps.Add(new PayloadEditorForm.Step { IsScript = true, Text = Disassemble(rom.ReadBytesAt(address, Math.Min(512, rom.Length - address))) });
                }
            }

            (string? Error, int[]? EntryOffsets) Commit(List<DialogSlot>? slots, byte[]? script)
            {
                try
                {
                    int[]? entryOffsets = null;
                    if (slots != null)
                    {
                        var written = DialogWriter.WriteTable(rom, slots);
                        entryOffsets = written.EntryOffsets;
                        rom.PatchInt32(field, written.TableAddress);
                        if (kind == "enemy") rom.PatchInt32(field - 4, DrGero.EnemyRecords.RunDialogFunc);
                    }
                    else if (script != null)
                    {
                        // An NPC condition that's just END means "no condition" -- clear it rather
                        // than store a script that would never evaluate true.
                        if (kind == "npc" && script.Length <= 1)
                        {
                            rom.PatchInt32(field, 0);
                        }
                        else
                        {
                            int offset = rom.AllocateFreeSpace(script.Length);
                            rom.WriteBytesAt(offset, script);
                            rom.PatchInt32(field, 0x08000000 | offset);
                            if (kind == "enemy") rom.PatchInt32(field - 4, DrGero.EnemyRecords.RunScriptFunc);
                        }
                    }
                    File.WriteAllBytes(romPath, rom.ToArray()); // Dragon Radar's file watcher reloads from this
                    return (null, entryOffsets);
                }
                catch (Exception ex)
                {
                    return ($"Couldn't write to the ROM: {ex.Message}", null);
                }
            }

            Application.Run(new PayloadEditorForm(title, steps, rawScript, warning, Commit, rom.ToArray()));
        }
    }
}
