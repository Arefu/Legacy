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
                MessageBox.Show("--micro-edit needs --rom=<existing file>, --kind=<trigger|object|npc|enemy> and --field=0x<offset>.", "Legacy");
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

            if (kind == "npc")
            {
                rawScript = true;
                rom.PushPosition(field);
                uint cond = (uint)rom.ReadInt();
                rom.PopPosition();

                uint type = cond >> 16;
                string text;
                if (cond == 0)
                    text = "// No spawn condition: this NPC always appears.\r\n// To gate it, end with a value that is true when it should appear, e.g.:\r\n//   StackTestStoryFlag(191);\r\n";
                else if (type == 1)
                    text = $"StackTestStoryFlag({cond & 0xFFFF});";
                else if (type == 2)
                    text = $"StackTestStoryFlagNot({cond & 0xFFFF});";
                else if (type == 3)
                {
                    text = "// (one-shot flag test: shows once, then clears its flag)";
                    warning = $"This NPC uses a one-shot flag test on flag {cond & 0xFFFF}, which has no script form here. Applying replaces it.";
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

                bool isSequence = kind == "object" || address <= 0
                    || (kind == "enemy" ? enemyFunc == DrGero.EnemyRecords.RunDialogFunc : DialogScanner.LooksLikeDialogSequence(rom, address));
                rawScript = !isSequence;

                if (address <= 0)
                {
                    steps.Add(new PayloadEditorForm.Step { IsScript = true }); // nothing yet -- start with an empty script
                }
                else if (isSequence)
                {
                    var read = DialogReader.Read(rom, address);
                    bool lossy = false;
                    foreach (var line in read.Lines)
                    {
                        if (line.Kind == DialogLineKind.Script)
                            steps.Add(new PayloadEditorForm.Step { IsScript = true, Text = Disassemble(line.ScriptBytes) });
                        else
                        {
                            steps.Add(new PayloadEditorForm.Step { Character = line.CharacterId, Text = line.Text });
                            lossy |= line.Kind == DialogLineKind.Interpolated;
                        }
                    }
                    if (!read.FullyUnderstood)
                        warning = $"{read.Note ?? "Part of this conversation can't be represented here."} Applying replaces everything from that point on.";
                    else if (lossy)
                        warning = "Some lines fill in values (%s/%d) -- they're shown as plain text and applying drops the filling.";
                }
                else
                {
                    steps.Add(new PayloadEditorForm.Step { IsScript = true, Text = Disassemble(rom.ReadBytesAt(address, Math.Min(512, rom.Length - address))) });
                }
            }

            string? Commit(List<DialogLine>? lines, byte[]? script)
            {
                try
                {
                    if (lines != null)
                    {
                        rom.PatchInt32(field, DialogWriter.WriteSequence(rom, lines));
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
                    return null;
                }
                catch (Exception ex)
                {
                    return $"Couldn't write to the ROM: {ex.Message}";
                }
            }

            Application.Run(new PayloadEditorForm(title, steps, rawScript, warning, Commit));
        }
    }
}
