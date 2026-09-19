using DrGero.Config;
using DrGero.IO;

namespace DrGero.Quests
{
    /// <summary>
    /// Reads and decodes the game's Quest Log system.
    ///
    /// CONFIRMED via IDA 2026-09 (QuestLog_Build @0x80049FC, PartyState_SetMemberFlag/
    /// PartyState_TestMemberFlag @0x80040EE/0x80041A6, BytecodeVM_StackTestFlag_Raw
    /// @0x8003E42, BytecodeVM_SetFlag_Raw/ClearFlag_Raw @0x8003E5E/0x8003E74):
    ///
    /// The in-game pause-menu Quest Log is built from a fixed table of 43
    /// <see cref="QuestEntry"/> records (<c>g_QuestEntries</c>, Game.QuestTableOffset).
    /// Each record has a display name and TWO tiny pieces of VM bytecode: one that
    /// decides whether the quest should currently show as "in progress", and one that
    /// decides whether it should show as "complete". These are not simple flag IDs
    /// stored in the table -- they are actual mini bytecode scripts (almost always just
    /// "push a flag id, then test it"), executed fresh every time the log is opened.
    ///
    /// The flags these scripts test live in a flat, plain bit array inside the save
    /// state (g_PartyState + 0x180 onward -- confirmed at least 300 bits/38 bytes used).
    /// A flag is set/cleared by ordinary Zenkai opcodes 27 (SetStoryFlag) and 28
    /// (ClearStoryFlag), and tested by opcodes 19 (StackTestStoryFlag) and 20
    /// (StackTestStoryFlagNot) -- RENAMED 2026-09-19 from StackSetPartyFlag/
    /// StackTestQuestFlag, names that made these look related to the unrelated system
    /// below. These are the SAME four opcodes any map/NPC script can use, so any script
    /// anywhere in the game can be the one that actually advances a quest. There is no
    /// separate "quest flag setter" opcode.
    ///
    /// IMPORTANT: opcode 128 (SetPartyMemberFlag, renamed from the misleading
    /// SetQuestFlag) and the functions PartyState_SetMemberFlag/PartyState_TestMemberFlag
    /// do NOT feed the quest log. They use a completely different two-part
    /// (category, sub-id) key resolved through a 68-category lookup table
    /// (g_PartyMemberFlagCategories) into a separate bit region at g_PartyState+0x231.
    /// Opcode 23 (StackTestPartyMemberFlag) uses the same mechanism. This looks like a
    /// per-character/per-encounter status system, unrelated to the quest log -- don't
    /// confuse the two when reading scripts. The quest log ONLY cares about opcodes
    /// 19/20/27/28 and the flat array.
    /// </summary>
    public sealed class QuestEntry
    {
        public int Index { get; init; }
        public int Priority { get; init; }
        public string Name { get; init; } = "";
        public int AvailableScriptAddress { get; init; }
        public int CompleteScriptAddress { get; init; }
        public string AvailableCondition { get; init; } = "";
        public string CompleteCondition { get; init; } = "";

        /// <summary>Every flag id tested (via opcode 19/20) by either condition script.</summary>
        public List<int> FlagIds { get; init; } = new();

        /// <summary>Flag ids tested by ONLY the available-condition script (see FlagIds for both combined).</summary>
        public List<int> AvailableFlagIds { get; init; } = new();

        /// <summary>Flag ids tested by ONLY the complete-condition script.</summary>
        public List<int> CompleteFlagIds { get; init; } = new();

        /// <summary>File offset of this quest's own 16-byte table record -- what QuestWriter patches.</summary>
        public int RecordAddress { get; init; }
    }

    public static class QuestReader
    {
        private const int RecordSize = 16;

        /// <summary>
        /// Reads and decodes every entry in the quest table, in table order (this is also
        /// display order -- QuestLog_Build walks the table 0..QuestCount-1 and appends to
        /// the in-progress/complete lists in that order, it does not sort by anything).
        /// </summary>
        public static List<QuestEntry> ReadAll(ROM rom, Game game)
        {
            var results = new List<QuestEntry>();
            if (game.QuestTableOffset == 0 || game.QuestCount == 0) return results;

            for (int i = 0; i < game.QuestCount; i++)
            {
                int recordAddr = game.QuestTableOffset + i * RecordSize;

                rom.PushPosition(recordAddr);
                int priority = rom.ReadInt();
                int availPtr = rom.ReadPointer();
                int completePtr = rom.ReadPointer();
                int namePtr = rom.ReadPointer();
                rom.PopPosition();

                string name = ReadCString(rom, namePtr);
                var (availText, availFlags) = ConditionDecoder.Decode(rom, availPtr);
                var (completeText, completeFlags) = ConditionDecoder.Decode(rom, completePtr);

                var flagIds = availFlags.Concat(completeFlags).Distinct().OrderBy(f => f).ToList();

                results.Add(new QuestEntry
                {
                    Index = i,
                    Priority = priority,
                    Name = name,
                    AvailableScriptAddress = availPtr,
                    CompleteScriptAddress = completePtr,
                    AvailableCondition = availText,
                    CompleteCondition = completeText,
                    FlagIds = flagIds,
                    AvailableFlagIds = availFlags,
                    CompleteFlagIds = completeFlags,
                    RecordAddress = recordAddr,
                });
            }

            return results;
        }

        private static string ReadCString(ROM rom, int addr)
        {
            if (addr == 0) return "";
            rom.PushPosition(addr & 0x00FFFFFF);
            var sb = new System.Text.StringBuilder();
            try
            {
                for (int i = 0; i < 256; i++)
                {
                    byte b = (byte)rom.ReadByte();
                    if (b == 0) break;
                    sb.Append((char)b);
                }
            }
            finally
            {
                rom.PopPosition();
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Writes back edits to a quest's table record (Name/Priority) and, for the common
    /// case, its condition scripts. A quest's Available/Complete condition is a real tiny
    /// VM script, not a stored flag id (see QuestReader's class doc) -- WriteSingleFlagTest
    /// only supports the overwhelmingly common shape actually seen in this game's own quest
    /// table ("push a flag id, then test it", i.e. exactly one flag, no AND/OR/NOT
    /// wrapping). Callers should only offer this editor when
    /// QuestEntry.Available/CompleteFlagIds has exactly one entry AND the decoded
    /// condition text is the bare "Flag(N)" form (see DragonRadarUI's quest panel) --
    /// anything more complex (multi-flag AND, NOT) isn't safely regenerable without a
    /// real script editor, so this makes no attempt to preserve or edit those.
    /// </summary>
    public static class QuestWriter
    {
        private const int PriorityOffset = 0;
        private const int AvailablePtrOffset = 4;
        private const int CompletePtrOffset = 8;
        private const int NamePtrOffset = 12;

        public static void WritePriority(ROM rom, QuestEntry quest, int priority) =>
            rom.PatchInt32(quest.RecordAddress + PriorityOffset, priority);

        public static void WriteName(ROM rom, QuestEntry quest, string newName)
        {
            byte[] bytes = new byte[System.Text.Encoding.ASCII.GetByteCount(newName) + 1];
            System.Text.Encoding.ASCII.GetBytes(newName, 0, newName.Length, bytes, 0);
            // final byte stays 0 -- NUL terminator
            int off = rom.AllocateFreeSpace(bytes.Length);
            rom.WriteBytesAt(off, bytes);
            rom.PatchInt32(quest.RecordAddress + NamePtrOffset, 0x08000000 | off);
        }

        /// <summary>Rewrites the available-condition script to a bare "test flag N" and repoints the quest at it.</summary>
        public static void WriteAvailableFlag(ROM rom, QuestEntry quest, int flagId) =>
            rom.PatchInt32(quest.RecordAddress + AvailablePtrOffset, 0x08000000 | WriteSingleFlagTest(rom, flagId));

        /// <summary>Rewrites the complete-condition script to a bare "test flag N" and repoints the quest at it.</summary>
        public static void WriteCompleteFlag(ROM rom, QuestEntry quest, int flagId) =>
            rom.PatchInt32(quest.RecordAddress + CompletePtrOffset, 0x08000000 | WriteSingleFlagTest(rom, flagId));

        /// <summary>
        /// Writes [PushByte/PushVarint flagId][Step 19 (StackTestStoryFlag)][END] to fresh
        /// ROM space and returns its file offset. Always allocates new space rather than
        /// overwriting in place -- same "don't try to reuse the old bytes" convention as
        /// every other write-back in this codebase (Duplicate Trigger, Add Trigger, etc).
        /// </summary>
        private static int WriteSingleFlagTest(ROM rom, int flagId)
        {
            var bytes = new List<byte>();
            if (flagId is >= 0 and <= 255)
            {
                bytes.Add(0x00); // PushByte
                bytes.Add((byte)flagId);
            }
            else
            {
                bytes.Add(0x01); // PushVarint
                bytes.AddRange(EncodeVarintZigZag(flagId));
            }
            bytes.Add(0x02); // Step
            bytes.Add(19);   // StackTestStoryFlag
            bytes.Add(0x11); // END

            int off = rom.AllocateFreeSpace(bytes.Count);
            rom.WriteBytesAt(off, bytes.ToArray());
            return off;
        }

        // Inverse of ConditionDecoder.ReadVarintZigZag: zigzag-encode, then split into 7-bit
        // groups MOST-significant group first (matching the decoder's `(raw<<7)|(b&0x7F)`
        // left-to-right accumulation), continuation bit set on every group but the last.
        private static byte[] EncodeVarintZigZag(int value)
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));
            var groups = new List<byte>();
            do
            {
                groups.Add((byte)(zigzag & 0x7F));
                zigzag >>= 7;
            } while (zigzag != 0);
            groups.Reverse();
            for (int i = 0; i < groups.Count - 1; i++)
                groups[i] |= 0x80;
            return groups.ToArray();
        }
    }

    /// <summary>
    /// Decodes a small VM condition script (as used by QuestEntry.isAvailableScript /
    /// isCompleteScript) into a readable boolean expression, e.g. "Flag(191)" or
    /// "(Flag(97) AND Flag(112) AND Flag(123))". Understands exactly the instructions
    /// actually seen in these condition scripts across the real ROM (PushByte, PushVarint,
    /// Step 19/20/21, StackAnd, StackNot, StackCmpEq, END) -- see
    /// <see cref="InstructionDecoder"/> for the FULL opcode set used by general map/NPC
    /// scripts, which this intentionally does not duplicate.
    /// </summary>
    internal static class ConditionDecoder
    {
        public static (string Text, List<int> FlagIds) Decode(ROM rom, int addr)
        {
            var flagIds = new List<int>();
            if (addr == 0) return ("(none)", flagIds);

            var stack = new List<string>();

            rom.PushPosition(addr & 0x00FFFFFF);
            try
            {
                for (int guard = 0; guard < 512; guard++)
                {
                    byte op = (byte)rom.ReadByte();
                    switch (op)
                    {
                        case 0x00: // PushByte
                            stack.Add(((byte)rom.ReadByte()).ToString());
                            break;
                        case 0x01: // PushVarint (LEB128 + zigzag)
                            stack.Add(ReadVarintZigZag(rom).ToString());
                            break;
                        case 0x02: // Step
                            {
                                byte opcodeIndex = (byte)rom.ReadByte();
                                string top = stack.Count > 0 ? stack[^1] : "?";
                                if (opcodeIndex == 19 && stack.Count > 0)
                                {
                                    stack[^1] = $"Flag({top})";
                                    if (int.TryParse(top, out int fid)) flagIds.Add(fid);
                                }
                                else if (opcodeIndex == 20 && stack.Count > 0)
                                {
                                    stack[^1] = $"!Flag({top})";
                                    if (int.TryParse(top, out int fid)) flagIds.Add(fid);
                                }
                                else
                                {
                                    stack.Add($"Opcode{opcodeIndex}({top})");
                                }
                                break;
                            }
                        case 0x03: // type handler dispatch -- one operand byte, no stack effect here
                            rom.ReadByte();
                            break;
                        case 0x04: // StackAnd
                            BinaryOp(stack, "AND");
                            break;
                        case 0x05: // StackNot
                            UnaryOp(stack, "NOT");
                            break;
                        case 0x0B: // StackCmpEq
                            BinaryOp(stack, "==");
                            break;
                        case 0x0C: // StackCmpNe
                            BinaryOp(stack, "!=");
                            break;
                        case 0x11: // END
                            return (stack.Count > 0 ? stack[^1] : "(empty)", flagIds);
                        case 0x12: // Jump -- 1 signed byte, not seen branching in real condition scripts
                        case 0x13: // JumpIfFalse
                            rom.ReadByte();
                            break;
                        case 0x14: // StackPop
                            if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                            break;
                        default:
                            return ($"(unsupported op 0x{op:X2} in condition script @0x{addr:X8})", flagIds);
                    }
                }
                return ("(guard limit hit)", flagIds);
            }
            finally
            {
                rom.PopPosition();
            }
        }

        private static void BinaryOp(List<string> stack, string symbol)
        {
            string b = Pop(stack);
            string a = Pop(stack);
            stack.Add($"({a} {symbol} {b})");
        }

        private static void UnaryOp(List<string> stack, string name)
        {
            string a = Pop(stack);
            stack.Add($"{name}({a})");
        }

        private static string Pop(List<string> stack)
        {
            if (stack.Count == 0) return "?";
            string v = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            return v;
        }

        private static long ReadVarintZigZag(ROM rom)
        {
            int raw = 0;
            while (true)
            {
                byte b = (byte)rom.ReadByte();
                raw = (raw << 7) | (b & 0x7F);
                if ((b & 0x80) == 0) break;
            }
            return (raw >> 1) ^ -(raw & 1);
        }
    }

    /// <summary>
    /// One decoded VM instruction from a general script (map load scripts, NPC/entity
    /// interaction scripts, trigger scripts -- anything run by BytecodeVM_ExecuteScript).
    /// Ported from Legacy.SView_Decoder (kept independent here so DrGero has no dependency
    /// on the Legacy project) so DrGero.Quests can scan ANY script -- not just the tiny
    /// quest condition scripts -- to find where a quest's flag is actually set/cleared.
    /// </summary>
    public sealed class ScriptInstruction
    {
        public int Offset { get; set; }
        public byte Opcode { get; set; }
        public byte? StepIndex { get; set; }
        public List<long> Args { get; set; } = new();
    }

    public static class InstructionDecoder
    {
        public static List<ScriptInstruction> Decode(byte[] data, int maxInstructions = 4096)
        {
            var result = new List<ScriptInstruction>();
            int offset = 0;

            while (offset < data.Length && result.Count < maxInstructions)
            {
                int start = offset;
                byte op = data[offset++];

                switch (op)
                {
                    case 0x00: // PushByte
                        if (offset >= data.Length) return result;
                        result.Add(new ScriptInstruction { Offset = start, Opcode = op, Args = new List<long> { data[offset++] } });
                        break;
                    case 0x01: // PushVarint
                        {
                            int raw = ReadVarint(data, ref offset);
                            long v = (raw >> 1) ^ -(raw & 1);
                            result.Add(new ScriptInstruction { Offset = start, Opcode = op, Args = new List<long> { v } });
                            break;
                        }
                    case 0x02: // Step
                        if (offset >= data.Length) return result;
                        result.Add(new ScriptInstruction { Offset = start, Opcode = op, StepIndex = data[offset++] });
                        break;
                    case 0x03: // type handler dispatch
                        if (offset >= data.Length) return result;
                        result.Add(new ScriptInstruction { Offset = start, Opcode = op, Args = new List<long> { data[offset++] } });
                        break;
                    case 0x04: case 0x05: case 0x06: case 0x07: case 0x08: case 0x09: case 0x0A:
                    case 0x0B: case 0x0C: case 0x0D: case 0x0E: case 0x0F: case 0x10: case 0x14:
                        result.Add(new ScriptInstruction { Offset = start, Opcode = op });
                        break;
                    case 0x11: // END
                        result.Add(new ScriptInstruction { Offset = start, Opcode = op });
                        return result;
                    case 0x12: case 0x13: case 0x1C: // Jump / JumpIfFalse / LoopOrJump -- 1 signed byte
                        if (offset >= data.Length) return result;
                        result.Add(new ScriptInstruction { Offset = start, Opcode = op, Args = new List<long> { unchecked((sbyte)data[offset++]) } });
                        break;
                    case 0x15: case 0x16: case 0x17: case 0x18: case 0x19: case 0x1A: // StepTypePost_*
                        if (offset >= data.Length) return result;
                        result.Add(new ScriptInstruction { Offset = start, Opcode = op, Args = new List<long> { data[offset++] } });
                        break;
                    case 0x1B: // PushToAltStack
                        result.Add(new ScriptInstruction { Offset = start, Opcode = op });
                        break;
                    case 0x1D: // PushMultiVarint
                        {
                            if (offset >= data.Length) return result;
                            byte count = data[offset++];
                            var values = new List<long>();
                            for (int i = 0; i < count; i++)
                            {
                                int raw = ReadVarint(data, ref offset);
                                values.Add((raw >> 1) ^ -(raw & 1));
                            }
                            result.Add(new ScriptInstruction { Offset = start, Opcode = op, Args = values });
                            break;
                        }
                    default:
                        return result; // unknown opcode -- stop rather than desync further
                }
            }
            return result;
        }

        private static int ReadVarint(byte[] data, ref int offset)
        {
            int value = 0;
            while (offset < data.Length)
            {
                byte b = data[offset++];
                value = (value << 7) | (b & 0x7F);
                if ((b & 0x80) == 0) break;
            }
            return value;
        }

        /// <summary>
        /// Scans a decoded instruction list for opcode 27/28 (SetStoryFlag /
        /// ClearStoryFlag) calls and returns the flag id each one operates on, read
        /// from the PushByte/PushVarint instruction immediately before it (scripts always
        /// push the id right before stepping the opcode that consumes it -- confirmed
        /// across every quest condition script and every set/clear call sampled).
        /// </summary>
        public static List<(int FlagId, bool IsSet)> FindFlagWrites(List<ScriptInstruction> instructions)
        {
            var results = new List<(int FlagId, bool IsSet)>();
            for (int i = 1; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                if (instr.Opcode != 0x02 || instr.StepIndex is not (27 or 28)) continue;

                var prev = instructions[i - 1];
                if ((prev.Opcode == 0x00 || prev.Opcode == 0x01) && prev.Args.Count > 0)
                    results.Add(((int)prev.Args[0], instr.StepIndex == 27));
            }
            return results;
        }
    }

    /// <summary>
    /// Walks a dialog conversation's sequence[] array (see DBZKit/Dialog_Format.md) looking
    /// for mode-0 "DIALOG_SCRIPT" entries -- steps that run a bytecode script with no text
    /// box, chained in between the lines of a normal conversation via each entry's own
    /// Index field. CONFIRMED real example (Dialog_Format.md, the YAJIROBE Senzu Bean
    /// conversation): a mode-0 entry mid-conversation runs PushByte(itemId); Step(26=
    /// PickUpItem); END to hand the player an item between two lines of dialogue -- the
    /// same mechanism a quest-advancing NPC would use to run SetStoryFlag (opcode 27) once
    /// its dialogue reaches the right line, which is genuinely how "talk to an NPC to
    /// advance a quest" is wired in this game (see Quest-System.md's trigger-side note --
    /// there is no other hook point; regular NPC-collision "talk" dispatch is dead code).
    ///
    /// A dialog trigger's payload slot (Map_VariationEntry-style trigger record, +0x10) can
    /// hold either a real sequence[] pointer (Dialog-type triggers) or raw bytecode directly
    /// (other trigger kinds reusing the same generic scaffolding -- confirmed in IDA,
    /// Z1A1's one mapTrigger is bytecode, not a conversation). This class only attempts the
    /// sequence[] interpretation; callers should also try <see cref="InstructionDecoder"/>
    /// directly on the same address for the bytecode case.
    /// </summary>
    public static class DialogScanner
    {
        /// <summary>
        /// Returns true if <paramref name="addr"/> plausibly looks like a real sequence[]
        /// array (each of the first few non-null slots is a real ROM pointer whose target
        /// has a Mode byte in 0-5), so callers can tell dialog from raw bytecode before
        /// trusting either interpretation.
        ///
        /// FIXED 2026-09-19: addresses here are plain ROM file offsets (0 to ~0x7FFFFF),
        /// NOT 0x08xxxxxx-prefixed values -- ROM.ReadPointer() already strips the top byte
        /// before returning, so a "(x &amp; 0xFF000000) == 0x08000000" check on anything
        /// that came from it is dead code that's always false (the top byte is already
        /// gone), which silently made this always report "not a dialog sequence" for every
        /// real one. Every address in/out of this class is a plain offset now; only
        /// explicit 0x08000000-prefixed values (e.g. what you'd patch into a ROM pointer
        /// field so the GAME reads it correctly) use the full form.
        /// </summary>
        // CRASH FIX 2026-09-19: LooksLikeDialogSequence only sanity-checks the first few
        // entries (maxCheck, default 4) before saying "yes" -- a real crash report showed
        // this was a false positive on some OTHER data that happened to look dialog-shaped
        // for that many entries, and the caller (FindFlagWritesInDialog / DialogReader.Read,
        // which walk up to 64 entries) then ran off into garbage addresses and threw
        // ArgumentOutOfRangeException instead of gracefully saying "not a dialog after
        // all". Every raw address anywhere in this class now goes through InBounds()
        // before it's ever handed to PushPosition/ReadBytesAt, so this can no longer
        // throw -- worst case it now correctly stops and reports "not a dialog"/"stopped
        // early" instead of crashing the whole app.
        private static bool InBounds(ROM rom, int addr, int length) =>
            addr >= 0 && length >= 0 && addr <= rom.Length - length;

        public static bool LooksLikeDialogSequence(ROM rom, int addr, int maxCheck = 4)
        {
            if (!InBounds(rom, addr, 4)) return false;

            int checked_ = 0;
            rom.PushPosition(addr);
            try
            {
                for (int i = 0; i < 16 && checked_ < maxCheck; i++)
                {
                    if (!InBounds(rom, rom.Position, 4)) break;
                    int entryPtr = rom.ReadPointer();
                    if (entryPtr == 0 || (entryPtr >>> 16) == 0) break; // sentinel: end of sequence
                    if (!InBounds(rom, entryPtr, 3)) return false; // garbage pointer -- definitely not a real sequence[]

                    rom.PushPosition(entryPtr);
                    rom.Skip(2); // Index
                    byte mode = (byte)rom.ReadByte();
                    rom.PopPosition();

                    if (mode > 5) return false;
                    checked_++;
                }
            }
            finally
            {
                rom.PopPosition();
            }

            return checked_ > 0;
        }

        /// <summary>
        /// Walks the sequence[] array and decodes every mode-0 (DIALOG_SCRIPT) entry's
        /// embedded script (starting at entry+3, same instruction set as any other script),
        /// returning every opcode 27/28 flag write found across all of them. Same plain-
        /// file-offset convention as LooksLikeDialogSequence -- see its doc comment.
        /// </summary>
        public static List<(int FlagId, bool IsSet)> FindFlagWritesInDialog(ROM rom, int sequenceAddr, int maxEntries = 64)
        {
            var results = new List<(int FlagId, bool IsSet)>();
            WalkSequence(rom, sequenceAddr, maxEntries, results, new HashSet<int>(), 0);
            return results;
        }

        // Mode-5 (DIALOG_JUMP, DialogEntryJump: target ptr at entry+4) entries hand off to
        // ANOTHER sequence[] -- confirmed via IDA 2026-09-19: Mathbook's completion flag
        // (SetStoryFlag(2), seq 0x83BB5F4) is only reachable through a jump entry at
        // 0x83B7AD4, so not following these made "Find Flag Usage" miss it. `visited`
        // guards against jump cycles; depth caps runaway chains.
        private static void WalkSequence(ROM rom, int sequenceAddr, int maxEntries, List<(int FlagId, bool IsSet)> results, HashSet<int> visited, int depth)
        {
            if (depth > 8 || !visited.Add(sequenceAddr)) return;
            if (!InBounds(rom, sequenceAddr, 4)) return;

            rom.PushPosition(sequenceAddr);
            try
            {
                for (int i = 0; i < maxEntries; i++)
                {
                    if (!InBounds(rom, rom.Position, 4)) break;
                    int entryPtr = rom.ReadPointer();
                    if (entryPtr == 0 || (entryPtr >>> 16) == 0) break; // sentinel
                    if (!InBounds(rom, entryPtr, 3)) break; // garbage pointer -- stop, don't guess further

                    rom.PushPosition(entryPtr);
                    rom.Skip(2); // Index
                    byte mode = (byte)rom.ReadByte();
                    int scriptAddr = entryPtr + 3; // mode-0 script starts right after the 3-byte header
                    int jumpTarget = 0;
                    if (mode == 5 && InBounds(rom, entryPtr, 8))
                    {
                        rom.PopPosition();
                        rom.PushPosition(entryPtr + 4);
                        jumpTarget = rom.ReadPointer();
                    }
                    rom.PopPosition();

                    if (mode == 5)
                    {
                        if (jumpTarget > 0 && LooksLikeDialogSequence(rom, jumpTarget))
                            WalkSequence(rom, jumpTarget, maxEntries, results, visited, depth + 1);
                        continue;
                    }

                    if (mode != 0) continue;
                    if (!InBounds(rom, scriptAddr, 1)) continue; // can't read even one byte -- skip this entry

                    int readLen = Math.Min(256, rom.Length - scriptAddr);
                    byte[] scriptBytes = rom.ReadBytesAt(scriptAddr, readLen);
                    var instructions = InstructionDecoder.Decode(scriptBytes);
                    results.AddRange(InstructionDecoder.FindFlagWrites(instructions));
                }
            }
            finally
            {
                rom.PopPosition();
            }
        }
    }

    /// <summary>
    /// Answers "where in the whole game does flag N get set/cleared, and which quests
    /// test it" -- the tracking tool requested alongside the Quest editor, since a flag id
    /// by itself (as shown in the quest list) gives no way to find the trigger/dialogue
    /// that actually flips it. Scans every map's trigger array (the only place opcode
    /// 27/28 writes happen, per Quest-System.md's confirmed "no other hook point" note) via
    /// the same payload-resolution steps DragonRadarUI.ResolveTriggerScriptPayload already
    /// uses for one trigger, just walked across every map instead of one.
    /// </summary>
    public static class FlagUsageScanner
    {
        // Source: "Trigger" (a map trigger's payload) or "Object" (an object's OnPickup --
        // Math Book etc). ItemId is the object's g_ItemsInGame index for Object hits, -1 otherwise.
        // MapIndex = position in the game's MapEntries list (unique per map+variation, unlike
        // Zone/Area). EntitySlot = the trigger/object's slot address in its map's array -- the
        // same value Dragon Radar's Entity.SourceAddress holds -- so the UI can jump straight
        // to the entity. Both default to -1 = unknown.
        public sealed record FlagUsage(int Zone, int Area, int TriggerDataAddress, int FlagId, bool IsSet, string Source = "Trigger", int ItemId = -1, int MapIndex = -1, int EntitySlot = -1);

        public static List<FlagUsage> ScanAllTriggers(ROM rom, IEnumerable<DrGero.Types.MapEntry> mapEntries)
        {
            var results = new List<FlagUsage>();

            var entryList = mapEntries.ToList();
            for (int mapIndex = 0; mapIndex < entryList.Count; mapIndex++)
            {
                var entry = entryList[mapIndex];
                if (entry.TriggerCount == 0 || entry.MapTriggers <= 0) continue;

                for (int t = 0; t < entry.TriggerCount; t++)
                {
                    int slot = entry.MapTriggers + t * 8;
                    if (slot < 0 || slot + 8 > rom.Length) continue;

                    rom.PushPosition(slot);
                    rom.Skip(4); // vTable
                    int dataPtr = rom.ReadPointer();
                    rom.PopPosition();
                    if (dataPtr <= 0 || dataPtr + 20 > rom.Length) continue;

                    rom.PushPosition(dataPtr + 0x10);
                    int payloadAddr = rom.ReadPointer();
                    rom.PopPosition();
                    if (payloadAddr <= 0 || payloadAddr >= rom.Length) continue;

                    var writes = new List<(int FlagId, bool IsSet)>();
                    int readLen = Math.Min(512, rom.Length - payloadAddr);
                    byte[] bytes = rom.ReadBytesAt(payloadAddr, readLen);
                    writes.AddRange(InstructionDecoder.FindFlagWrites(InstructionDecoder.Decode(bytes)));

                    if (DialogScanner.LooksLikeDialogSequence(rom, payloadAddr))
                        writes.AddRange(DialogScanner.FindFlagWritesInDialog(rom, payloadAddr));

                    foreach (var (flagId, isSet) in writes.Distinct())
                        results.Add(new FlagUsage(entry.Zone, entry.Area, dataPtr, flagId, isSet, "Trigger", -1, mapIndex, slot));
                }
            }

            return results;
        }

        /// <summary>Everything that can set/clear a story flag: trigger payloads plus object OnPickup payloads.</summary>
        public static List<FlagUsage> ScanAll(ROM rom, IEnumerable<DrGero.Types.MapEntry> mapEntries)
        {
            var entries = mapEntries.ToList();
            var results = ScanAllTriggers(rom, entries);
            results.AddRange(ScanAllObjects(rom, entries));
            return results;
        }

        /// <summary>
        /// Same scan, for every map object's collectionMsg dialogue (objectPtr+0x14 -- see
        /// EntityReader.ReadObjectArray). This is the hook the Math Book/Golden Capsules
        /// use: picking the object up runs its collectionMsg sequence (via the native
        /// onPickup handler), whose mode-0 entries are where SetStoryFlag lives. Without this, "Find Flag Usage" could only ever see triggers.
        /// </summary>
        public static List<FlagUsage> ScanAllObjects(ROM rom, IEnumerable<DrGero.Types.MapEntry> mapEntries)
        {
            var results = new List<FlagUsage>();

            var entryList = mapEntries.ToList();
            for (int mapIndex = 0; mapIndex < entryList.Count; mapIndex++)
            {
                var entry = entryList[mapIndex];
                if (entry.ObjectCount == 0 || entry.MapObjects <= 0) continue;

                for (int i = 0; i < entry.ObjectCount; i++)
                {
                    int slot = entry.MapObjects + i * 4;
                    if (slot < 0 || slot + 4 > rom.Length) continue;

                    rom.PushPosition(slot);
                    int objectPtr = rom.ReadPointer();
                    rom.PopPosition();
                    if (objectPtr <= 0 || objectPtr + 0x18 > rom.Length) continue;

                    rom.PushPosition(objectPtr + 4);
                    int itemId = rom.ReadInt();
                    rom.PopPosition();

                    // CONFIRMED via IDA 2026-09-19: +0x10 (onPickup) is a NATIVE function
                    // pointer (MapObject_OnPickup_RunCollectionMsg), not bytecode -- decoding
                    // it as a script just reads Thumb machine code as garbage opcodes. That
                    // handler runs +0x14 (collectionMsg) as a dialog sequence[], and THAT is
                    // where flag writes live (Mathbook: seq 0x83BB7A8, mode-0 entry
                    // SetStoryFlag(4)). So only the collectionMsg sequence is scanned.
                    rom.PushPosition(objectPtr + 0x14);
                    int payloadAddr = rom.ReadPointer();
                    rom.PopPosition();
                    if (payloadAddr <= 0 || payloadAddr >= rom.Length) continue;

                    var writes = new List<(int FlagId, bool IsSet)>();
                    if (DialogScanner.LooksLikeDialogSequence(rom, payloadAddr))
                        writes.AddRange(DialogScanner.FindFlagWritesInDialog(rom, payloadAddr));

                    foreach (var (flagId, isSet) in writes.Distinct())
                        results.Add(new FlagUsage(entry.Zone, entry.Area, objectPtr, flagId, isSet, "Object", itemId, mapIndex, slot));
                }
            }

            return results;
        }
    }
}
