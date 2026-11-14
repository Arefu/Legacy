using DrGero.IO;
using DrGero.Rendering;

namespace DrGero.Quests
{
    /// <summary>
    /// Reads a dialog sequence[] table as the set of separate conversations it really holds.
    ///
    /// One table is often several conversations, not one. A mode-0 (script) entry that ends with a value still on
    /// the VM stack sends the conversation to that slot instead of the entry's own Index, so a table usually opens
    /// with a script like "if story flag 0 is set, go to slot 3" and the lines for each case sit one after another,
    /// each run closed by its own end marker (a slot that is not a ROM pointer). Found from the original ROM
    /// 2026-11-15 (GitHub issue 3): Gohan in Zone 1 Area 1 (NPC record 0x3ED670, table 0x3B725C) is
    ///   slot 0  script  PushByte(0); TestStoryFlag; JumpIfFalse +2; PushByte(3); END     -> flag 0 set: slot 3
    ///   slot 1  text    "GOHAN: To use your melee attack, press the A Button."
    ///   slot 2  0       end
    ///   slot 3  script  the same test on flag 1, to slot 6
    ///   slot 4  text    "GOHAN: To activate your energy blast, use the B Button."
    ///   slot 5  3       end
    /// Reading only up to the first end marker (what every reader did before) shows the first conversation and
    /// hides the rest.
    ///
    /// All addresses are plain ROM file offsets (no 0x08 top byte), as everywhere else in this namespace.
    /// </summary>
    public sealed class DialogStep
    {
        /// <summary>File offset of the sequence[] table this step's slot is in (a mode-5 jump moves to another table).</summary>
        public int SequenceAddress { get; init; }
        public int Slot { get; init; }
        public int EntryAddress { get; init; }
        public byte Mode { get; init; }
        /// <summary>Speaker portrait id. Text steps only.</summary>
        public byte CharacterId { get; init; }
        /// <summary>The line shown (leading box-position / centre characters removed). Empty for script steps.</summary>
        public string Text { get; init; } = "";
        public bool IsScript => Mode == 0;
        /// <summary>Script steps: story flags it sets (true) or clears (false).</summary>
        public List<(int FlagId, bool IsSet)> FlagWrites { get; init; } = new();
        /// <summary>Script steps: story flags it tests.</summary>
        public List<int> FlagsTested { get; init; } = new();
    }

    /// <summary>One requirement for a conversation to play. Flag conditions carry the flag id; anything else only has Text.</summary>
    public readonly record struct DialogCondition(string Text, int FlagId, bool MustBeSet)
    {
        private const int NextTalkId = -2;
        public bool IsFlag => FlagId >= 0;
        /// <summary>Plays on a later talk: the conversation before it ended on a marker that points here.</summary>
        public bool IsNextTalk => FlagId == NextTalkId;
        public static DialogCondition NextTalk(int afterEndSlot) => new($"you talk to them again (after step {afterEndSlot})", NextTalkId, true);

        private const int RandomId = -3;
        /// <summary>The script picks this one at random (StackRand, opcode 17).</summary>
        public bool IsRandom => FlagId == RandomId;
        public static DialogCondition Random() => new("picked at random", RandomId, true);
        public override string ToString() => IsFlag ? $"story flag {FlagId} {(MustBeSet ? "is set" : "is not set")}" : Text;
    }

    /// <summary>One conversation: the steps played, in order, from where it starts to its end marker.</summary>
    public sealed class DialogBranch
    {
        public int SequenceAddress { get; init; }
        public int StartSlot { get; init; }
        /// <summary>Everything that has to hold for this conversation to be the one that plays. Empty = always.</summary>
        public List<DialogCondition> Conditions { get; init; } = new();
        public List<DialogStep> Steps { get; init; } = new();

        public IEnumerable<DialogStep> Lines => Steps.Where(s => !s.IsScript);
        public IEnumerable<(int FlagId, bool IsSet)> FlagWrites => Steps.SelectMany(s => s.FlagWrites);
        /// <summary>Story flags this conversation needs set, needs clear, tests or changes.</summary>
        public IEnumerable<int> FlagIds =>
            Conditions.Where(c => c.IsFlag).Select(c => c.FlagId).Concat(Steps.SelectMany(s => s.FlagsTested)).Concat(FlagWrites.Select(w => w.FlagId)).Distinct();

        /// <summary>
        /// True when this is what the character says with nothing going on: no story flag has to be SET for it to play
        /// and it changes no story flag. "Flag N is not set" alone still counts as idle -- that is the fallback line an
        /// NPC has until a quest reaches them.
        /// </summary>
        public bool IsIdle => !FlagWrites.Any() && !Conditions.Any(c => !c.IsNextTalk && !c.IsRandom && (!c.IsFlag || c.MustBeSet));

        public string ConditionText => Conditions.Count == 0 ? "always" : string.Join(" and ", Conditions.Select(c => c.ToString()));
    }

    public sealed class DialogTable
    {
        public int SequenceAddress { get; init; }
        /// <summary>How many slots the table has: one past the highest slot any conversation reaches (its end marker included).</summary>
        public int SlotCount { get; init; }
        public List<DialogBranch> Branches { get; init; } = new();
    }

    public static class DialogBranches
    {
        private const int MaxSlots = 512;
        private const int MaxBranches = 128;

        private static bool InBounds(byte[] d, int addr, int length) => addr >= 0 && length >= 0 && addr <= d.Length - length;

        /// <summary>
        /// An end marker's "next talk starts here" slot, or 0 when it is 0 / doesn't name a real entry of the table.
        /// Read from ROM data (every non-zero marker inside a table names an entry slot); not traced in IDA yet.
        /// </summary>
        public static int NextTalkSlot(byte[] d, int sequenceAddr, int endSlot)
        {
            int at = sequenceAddr + endSlot * 4;
            if (!InBounds(d, at, 4)) return 0;
            uint value = BitConverter.ToUInt32(d, at);
            return value is > 0 and < MaxSlots && EntryAt(d, sequenceAddr, (int)value) >= 0 ? (int)value : 0;
        }

        /// <summary>The slot's entry file offset, or -1 for an end marker / anything that is not a pointer into the ROM.</summary>
        private static int EntryAt(byte[] d, int sequenceAddr, int slot)
        {
            int at = sequenceAddr + slot * 4;
            if (!InBounds(d, at, 4)) return -1;
            uint v = BitConverter.ToUInt32(d, at);
            if (v >> 24 != 0x08) return -1;
            int entry = (int)(v & 0x00FFFFFF);
            return InBounds(d, entry, 4) && d[entry + 2] <= 5 ? entry : -1;
        }

        /// <summary>
        /// One past the highest slot reachable from slot 0, following each entry's Index and every script entry's
        /// "go to slot N". 0 when <paramref name="sequenceOffset"/> is not a dialog table. Takes raw ROM bytes so
        /// readers that only hold a byte[] (Legacy) can use it.
        /// </summary>
        public static int ReachableSlotCount(byte[] rom, int sequenceOffset) => Walk(rom, null, sequenceOffset, null);

        /// <inheritdoc cref="ReachableSlotCount(byte[], int)"/>
        public static int ReachableSlotCount(ROM rom, int sequenceAddr) => Walk(rom.RawBytes, null, sequenceAddr, null);

        /// <summary>Splits the table at <paramref name="sequenceAddr"/> into its conversations. Empty when it is not a dialog table.</summary>
        public static DialogTable Analyze(ROM rom, int sequenceAddr)
        {
            var branches = new List<DialogBranch>();
            int slots = Walk(rom.RawBytes, rom, sequenceAddr, branches);
            return new DialogTable { SequenceAddress = sequenceAddr, SlotCount = slots, Branches = branches };
        }

        /// <summary>Every story-flag write in every conversation of the table (opcode 27/28 in its script steps).</summary>
        public static List<(int FlagId, bool IsSet)> FindFlagWrites(ROM rom, int sequenceAddr) =>
            Analyze(rom, sequenceAddr).Branches.SelectMany(b => b.FlagWrites).Distinct().ToList();

        private readonly record struct Pending(int Sequence, int Slot, List<DialogCondition> Conditions);

        private static int Walk(byte[] d, ROM? rom, int sequenceAddr, List<DialogBranch>? output)
        {
            if (EntryAt(d, sequenceAddr, 0) < 0) return 0;

            int slotCount = 0;
            var queue = new Queue<Pending>();
            var started = new HashSet<(int, int)>();
            queue.Enqueue(new Pending(sequenceAddr, 0, new List<DialogCondition>()));
            started.Add((sequenceAddr, 0));
            int branchCount = 0;

            while (queue.Count > 0 && branchCount++ < MaxBranches)
            {
                var start = queue.Dequeue();
                var conditions = new List<DialogCondition>(start.Conditions);
                var steps = new List<DialogStep>();
                var seen = new HashSet<(int, int)>();
                int seq = start.Sequence, slot = start.Slot;

                while (slot >= 0 && slot < MaxSlots && seen.Add((seq, slot)))
                {
                    if (seq == sequenceAddr) slotCount = Math.Max(slotCount, slot + 1);
                    int entry = EntryAt(d, seq, slot);
                    if (entry < 0)
                    {
                        // End marker. Its value is the step the NEXT conversation with this character starts at
                        // (0 = from the top). That is how an NPC says a different idle line each time you talk to it
                        // (GitHub issue 3: Chi-Chi in Zone 2 Area 20, table 0x3B8A80 = line, 2, line, 4, line, 0).
                        int nextTalk = NextTalkSlot(d, seq, slot);
                        if (nextTalk > 0)
                        {
                            if (started.Add((seq, nextTalk)))
                                queue.Enqueue(new Pending(seq, nextTalk, conditions.Where(c => !c.IsNextTalk).Append(DialogCondition.NextTalk(slot)).ToList()));
                            if (seq == sequenceAddr) slotCount = Math.Max(slotCount, nextTalk + 1);
                        }
                        break;
                    }

                    int next = BitConverter.ToUInt16(d, entry);
                    byte mode = d[entry + 2];

                    if (mode == 5)
                    {
                        // DIALOG_JUMP: carries on in another table (target pointer at entry+4).
                        int target = InBounds(d, entry, 8) ? (int)(BitConverter.ToUInt32(d, entry + 4) & 0x00FFFFFF) : -1;
                        if (target <= 0 || EntryAt(d, target, 0) < 0) break;
                        seq = target;
                        slot = 0;
                        continue;
                    }

                    if (mode == 0)
                    {
                        var code = InstructionDecoder.Decode(d.AsSpan(entry + 3, Math.Min(256, d.Length - entry - 3)).ToArray());
                        var gotos = FindGotos(code);
                        steps.Add(new DialogStep
                        {
                            SequenceAddress = seq, Slot = slot, EntryAddress = entry, Mode = 0,
                            FlagWrites = InstructionDecoder.FindFlagWrites(code),
                            FlagsTested = FlagTests(code),
                        });

                        var always = gotos.FirstOrDefault(g => g.Condition == null);
                        if (always != null)
                        {
                            slot = always.Slot;
                            continue;
                        }
                        foreach (var g in gotos)
                        {
                            if (started.Add((seq, g.Slot)))
                                queue.Enqueue(new Pending(seq, g.Slot, new List<DialogCondition>(conditions) { g.Condition!.Value }));
                            else if (seq == sequenceAddr)
                                slotCount = Math.Max(slotCount, g.Slot + 1);
                        }
                        // A random pick always goes to one of its slots: nothing falls through past it.
                        if (gotos.Count > 0 && gotos.All(g => g.Condition is { IsRandom: true }))
                            break;
                        // Carrying on past the script means none of its "go to" cases applied.
                        foreach (var g in gotos)
                            conditions.Add(Negate(g.Condition!.Value));
                        slot = next;
                        continue;
                    }

                    steps.Add(new DialogStep
                    {
                        SequenceAddress = seq, Slot = slot, EntryAddress = entry, Mode = mode,
                        CharacterId = d[entry + 3],
                        Text = rom == null ? "" : ReadText(d, rom, entry, mode),
                    });
                    slot = next;
                }

                output?.Add(new DialogBranch { SequenceAddress = start.Sequence, StartSlot = start.Slot, Conditions = Simplify(conditions), Steps = steps });
            }

            return slotCount;
        }

        private sealed record Goto(int Slot, DialogCondition? Condition);

        /// <summary>
        /// "Go to slot N" = a constant pushed right before END. Its condition is the JumpIfFalse guarding it: pushed in
        /// the fall-through part means "condition true", being the jump's own target means "condition false".
        /// </summary>
        private static List<Goto> FindGotos(List<ScriptInstruction> code)
        {
            var result = new List<Goto>();
            var tests = new List<(int After, int Target, DialogCondition Condition)>();
            var stack = new List<DialogCondition>();

            for (int i = 0; i < code.Count; i++)
            {
                var ins = code[i];
                switch (ins.Opcode)
                {
                    case 0x00:
                    case 0x01:
                        if (i + 1 < code.Count && code[i + 1].Opcode == 0x11 && ins.Args[0] is >= 0 and < MaxSlots)
                        {
                            DialogCondition? condition = null;
                            foreach (var t in tests)
                            {
                                if (t.Target == ins.Offset) condition = Negate(t.Condition);
                                else if (ins.Offset >= t.After && ins.Offset < t.Target) condition = t.Condition;
                            }
                            // Past a check this simulation can't place (a talk counter bumped with a type-handler op, an
                            // else branch): still a condition, not "always" -- taking it as always skipped the other
                            // lines (Chi-Chi's Mathbook nag, table 0x3BB2B8).
                            if (condition == null && tests.Count > 0)
                                condition = new DialogCondition("a script check (counter or similar) picks it", -1, true);
                            result.Add(new Goto((int)ins.Args[0], condition));
                        }
                        stack.Add(new DialogCondition(ins.Args[0].ToString(), -1, true));
                        break;
                    case 0x02:
                        {
                            var top = stack.Count > 0 ? stack[^1] : new DialogCondition("?", -1, true);
                            if (ins.StepIndex is 19 or 20 && stack.Count > 0 && int.TryParse(top.Text, out int flag))
                                stack[^1] = new DialogCondition($"Flag({flag})", flag, ins.StepIndex == 19);
                            else
                                stack.Add(new DialogCondition($"opcode {ins.StepIndex}({top.Text})", -1, true));
                            break;
                        }
                    case 0x05: // StackNot
                        if (stack.Count > 0) stack[^1] = Negate(stack[^1]);
                        break;
                    case 0x11: // an END returning a computed value, e.g. 1 + 2 * StackRand(3) = slot 1, 3 or 5 (Zone 3 MAN, table 0x3C1624)
                        if (i > 0 && code[i - 1].Opcode is not (0x00 or 0x01) && PossibleResult(code, i) is { Count: > 1 } options)
                            foreach (long v in options.Where(v => v is >= 0 and < MaxSlots).OrderBy(v => v))
                                result.Add(new Goto((int)v, DialogCondition.Random()));
                        break;
                    case 0x13: // JumpIfFalse: 1 signed byte, relative to the next instruction
                        {
                            var cond = stack.Count > 0 ? stack[^1] : new DialogCondition("a script check passes", -1, true);
                            if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                            tests.Add((ins.Offset + 2, ins.Offset + 2 + (int)ins.Args[0], cond));
                            break;
                        }
                    case 0x14: // StackPop
                        if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                        break;
                    case 0x04: case 0x06: case 0x07: case 0x08: case 0x09: case 0x0A: case 0x0B:
                    case 0x0C: case 0x0D: case 0x0E: case 0x0F: case 0x10:
                        if (stack.Count >= 2)
                        {
                            var b = stack[^1]; var a = stack[^2];
                            stack.RemoveRange(stack.Count - 2, 2);
                            stack.Add(new DialogCondition($"({a.Text} {OperatorText(ins.Opcode)} {b.Text})", -1, true));
                        }
                        break;
                }
            }
            return result;
        }

        /// <summary>
        /// Every value the script can leave on the stack at its END (instruction <paramref name="endIndex"/>), when it
        /// is built from constants, StackRand and + - * / only; null otherwise. Straight-line only: good enough for the
        /// random line pickers, which have no jumps.
        /// </summary>
        private static HashSet<long>? PossibleResult(List<ScriptInstruction> code, int endIndex)
        {
            var st = new List<HashSet<long>?>();
            HashSet<long>? Pop() { if (st.Count == 0) return null; var v = st[^1]; st.RemoveAt(st.Count - 1); return v; }
            for (int i = 0; i < endIndex; i++)
            {
                var ins = code[i];
                switch (ins.Opcode)
                {
                    case 0x00: case 0x01: case 0x1D:
                        foreach (long a in ins.Args) st.Add(new HashSet<long> { a });
                        break;
                    case 0x02:
                        if (ins.StepIndex == 17 && st.Count > 0 && st[^1] is { Count: 1 } max && max.First() is > 0 and <= 64)
                            st[^1] = Enumerable.Range(0, (int)max.First()).Select(x => (long)x).ToHashSet(); // StackRand(n): 0..n-1
                        else if (ins.StepIndex is 17 or 18 or 19 or 20 && st.Count > 0)
                            st[^1] = ins.StepIndex == 17 ? null : new HashSet<long> { 0, 1 };
                        else
                            st.Add(null);
                        break;
                    case 0x05: case 0x06:
                        if (st.Count > 0) st[^1] = null;
                        break;
                    case >= 0x04 and <= 0x10:
                        {
                            var b = Pop(); var a = Pop();
                            st.Add(a != null && b != null && a.Count * b.Count <= 256 && ins.Opcode is >= 0x07 and <= 0x0A
                                ? a.SelectMany(x => b.Where(y => ins.Opcode != 0x0A || y != 0).Select(y => ins.Opcode switch
                                    { 0x07 => x + y, 0x08 => x - y, 0x09 => x * y, _ => x / y })).ToHashSet()
                                : null);
                            break;
                        }
                    case 0x12: case 0x13: case 0x1C:
                        return null; // jumps: not a straight-line pick
                    case 0x14:
                        Pop();
                        break;
                    default:
                        st.Add(null);
                        break;
                }
            }
            return st.Count > 0 ? st[^1] : null;
        }

        private static string OperatorText(byte op) => op switch
        {
            0x04 => "AND", 0x07 => "+", 0x08 => "-", 0x09 => "*", 0x0A => "/", 0x0B => "==", 0x0C => "!=",
            0x0D => ">", 0x0E => ">=", 0x0F => "<", 0x10 => "<=", _ => $"op{op:X2}",
        };

        private static DialogCondition Negate(DialogCondition c) =>
            c.IsNextTalk || c.IsRandom ? c : c.IsFlag ? c with { MustBeSet = !c.MustBeSet }
            : c.Text.StartsWith("not (") ? c with { Text = c.Text[5..^1] }
            : c with { Text = $"not ({c.Text})" };

        /// <summary>Drops repeats, keeping the order the checks happen in.</summary>
        private static List<DialogCondition> Simplify(List<DialogCondition> conditions) => conditions.Distinct().ToList();

        private static List<int> FlagTests(List<ScriptInstruction> code)
        {
            var flags = new List<int>();
            for (int i = 1; i < code.Count; i++)
                if (code[i].Opcode == 0x02 && code[i].StepIndex is 19 or 20 && code[i - 1].Opcode is 0x00 or 0x01 && code[i - 1].Args.Count > 0)
                    flags.Add((int)code[i - 1].Args[0]);
            return flags;
        }

        private static string ReadText(byte[] d, ROM rom, int entry, byte mode)
        {
            string raw = mode switch
            {
                1 => CString(d, entry + 4),
                2 => Compressed(rom, entry + 4),
                3 => InBounds(d, entry, 8) ? CString(d, (int)(BitConverter.ToUInt32(d, entry + 4) & 0x00FFFFFF)) : "",
                4 => Compressed(rom, entry + 8),
                _ => "",
            };
            // Leading box-position ('!' top, '@' middle, '#' bottom) and centre ('^') characters are not part of the line.
            if (raw.Length > 0 && raw[0] is '!' or '@' or '#') raw = raw[1..];
            if (raw.Length > 0 && raw[0] == '^') raw = raw[1..];
            return raw.Replace('\x0B', '\n');
        }

        private static string CString(byte[] d, int addr)
        {
            if (!InBounds(d, addr, 1)) return "";
            int end = addr;
            while (end < d.Length && d[end] != 0 && end - addr < 1024) end++;
            return System.Text.Encoding.ASCII.GetString(d, addr, end - addr);
        }

        private static string Compressed(ROM rom, int addr)
        {
            if (addr < 0 || addr >= rom.Length) return "";
            rom.PushPosition(addr);
            try
            {
                var data = JCALG1.DecompressSequential(rom, expectedOutputSize: 4096).Data;
                int nul = Array.IndexOf(data, (byte)0);
                return System.Text.Encoding.ASCII.GetString(data, 0, nul >= 0 ? nul : data.Length);
            }
            catch
            {
                return "[text did not decompress]";
            }
            finally
            {
                rom.PopPosition();
            }
        }
    }
}
