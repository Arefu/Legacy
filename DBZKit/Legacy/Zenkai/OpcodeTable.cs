// AUTO-GENERATED from OpCodes.csv + Legacy.wiki/Zenkai/opcodes/**/*.md signatures.
// Index -> (Name, Arity). Arity is the opcode's argument count as documented on its
// wiki page (i.e. how many values it pops off the VM stack / expects pushed before it).
// Regenerate by re-joining those two sources if opcode docs change.
//
// Genuinely-unresolved opcodes (still `sub_XXXXXX` in OpCodes.csv / documented as
// low-confidence "Unknown" under Legacy.wiki/Zenkai/opcodes/unknown/*.md, ~36 of them)
// are given a stable `op_unk<index>` name below instead of the raw ROM address, so
// they're visible and writable in Zenkai source at all (an address-shaped name is easy
// to typo and gives no hint it's the same opcode across scripts/sessions if the
// function ever gets renamed in IDA). The original sub_XXXXXX is kept on OriginalName
// purely for cross-referencing back to BytecodeVM_OpCodes.md / the wiki - it is NOT
// parseable/printable Zenkai source, only op_unk<index> is.
namespace Legacy.Zenkai
{
    internal static class OpcodeTable
    {
        internal record OpcodeInfo(int Index, string Name, int Arity, string? OriginalName = null);

        internal static readonly OpcodeInfo[] ByIndex = new OpcodeInfo[]
        {
            new(0, "PushAccumulator", 0),
            new(1, "PushGlobalVar0", 0),
            new(2, "PushGlobalVar1", 0),
            new(3, "PushGlobalVar2", 0),
            new(4, "PushScriptArg0", 0),
            new(5, "PushScriptArg1", 0),
            new(6, "PushScriptArg2", 0),
            new(7, "PushScriptArg3", 0),
            new(8, "PushCurrentWorld", 0),
            new(9, "PushCurrentArea", 0),
            new(10, "PushCurrentVariant", 0),
            new(11, "PushActiveCharIndex", 0),
            new(12, "PushActiveCharName", 0),
            new(13, "PushActiveCharHP", 0),
            new(14, "PushActiveCharMaxHP", 0),
            new(15, "PushActiveCharEP", 0),
            new(16, "PushActiveCharMaxEP", 0),
            new(17, "StackRand", 1),
            new(18, "StackRandChance", 1),
            new(19, "StackTestQuestFlag", 1),
            new(20, "StackTestQuestFlagNot", 1),
            new(21, "StackGetItemCount", 1),
            new(22, "StackGetCharLevel", 1),
            new(23, "StackTestPartyMemberFlag", 2),
            new(24, "DisableLayer", 1),
            new(25, "EnableLayer", 1),
            new(26, "PickUpItem", 1),
            new(27, "StackSetPartyFlag", 1),
            new(28, "StackClearPartyFlag", 1),
            new(29, "FindCharacterEntity", 1),
            new(30, "FindEntityByType", 1),
            new(31, "op_unk31", 3, "sub_800977A"),
            new(32, "CenterCameraOnChar", 1),
            new(33, "SetActiveEntity", 1),
            new(34, "SetMapRestoreFlag", 1),
            new(35, "LoadMapWithEntities", 5),
            new(36, "LoadMapNoEntities", 5),
            new(37, "RestoreMap", 0),
            new(38, "WarpToMapWithEntities", 5),
            new(39, "WarpToMapNoEntities", 5),
            new(40, "FadeOut", 1),
            new(41, "FadeFromWhite", 1),
            new(42, "FadeToWhite", 1),
            new(43, "op_unk43", 1, "sub_8009B94"),
            new(44, "op_unk44", 1, "sub_8009BBC"),
            new(45, "UploadSpritePalette", 0),
            new(46, "PlayAudio", 1),
            new(47, "StopAudio", 1),
            new(48, "StopAllAudio", 0),
            new(49, "PlayMusic", 1),
            new(50, "StopMusic", 0),
            new(51, "SpawnCharacterEntity", 3),
            new(52, "DespawnEntity", 1),
            // CONFIRMED 2026-09 (user): walks toward the target position (ground pathing), distinct from opcode 54's FlyToPosition (direct/airborne move).
            new(53, "WalkToPosition", 3),
            // RENAMED 2026-09 (was "MoveEntityToPosition") -- CONFIRMED: flies directly to the target position, not ground pathing.
            new(54, "FlyToPosition", 3),
            new(55, "SetEntityFacing", 2),
            new(56, "SetEntityFollow", 2),
            new(57, "SetEntityAnimation", 2),
            new(58, "op_unk58", 2, "sub_800A044"),
            new(59, "Deprecated_CharacterSpecialAttack", 0),
            new(60, "op_unk60", 1, "sub_800A09A"),
            new(61, "Deprecated_CharacterFollow", 0),
            new(62, "op_unk62", 3, "sub_800A44E"),
            new(63, "BeginCommandBatch", 0),
            new(64, "CommitCommandBatch", 0),
            // ARITY CORRECTED 2026-09-16: was 1, confirmed via IDA decompile (0x800A5BE)
            // it pops 2 values -- see opcode_data.js for details (semantics still unclear).
            new(65, "PlayAudioBlocking", 2),
            new(66, "Deprecated_ShowImage", 0),
            new(67, "op_unk67", 0, "sub_800A622"),
            new(68, "op_unk68", 1, "sub_800A638"),
            new(69, "WaitFrames", 1),
            new(70, "SetActiveCharacter", 1),
            new(71, "SetCollisionRect", 4),
            new(72, "ClearCollisionRect", 0),
            new(73, "SetCharacterTransformation", 2),
            new(74, "ReloadMap", 0),
            new(75, "PushActiveCharNameUpper", 0),
            new(76, "RemoveItem", 1),
            new(77, "RemoveItems", 2),
            new(78, "op_unk78", 2, "sub_8009688"),
            // RENAMED 2026-09-16: was SetCharacterFlag_Bit0. CONFIRMED via IDA
            // (Character_GetActiveCount, 0x8003E00, tests exactly this bit) -- bit0
            // marks a character as an active party member.
            new(79, "SetCharacterInPartyFlag", 1),
            new(80, "SetCharacterFlag_Bit1", 1),
            new(81, "ClearCharacterFlag_Bit1", 1),
            new(82, "SetCharacterFlag_Bit5", 1),
            new(83, "SetCharacterFlag_Dynamic", 2),
            new(84, "TriggerMapEvent", 1),
            new(85, "FadeIn", 1),
            new(86, "SetPlayerVisible", 1),
            new(87, "SpawnItem", 3),
            new(88, "op_unk88", 0, "sub_800AC00"),
            new(89, "op_unk89", 4, "sub_8009D94"),
            new(90, "op_unk90", 3, "sub_800AB44"),
            // RENAMED 2026-09-16: was ClearCharacterFlag_Bit0 -- inverse of SetCharacterInPartyFlag.
            new(91, "ClearCharacterInPartyFlag", 1),
            new(92, "SpawnItem", 3),
            new(93, "op_unk93", 1, "sub_800A49A"),
            new(94, "op_unk94", 3, "sub_800A526"),
            new(95, "op_unk95", 1, "sub_800A572"),
            new(96, "op_unk96", 3, "sub_800A158"),
            new(97, "ReturnToTitleScreen", 0),
            new(98, "SetCharacterLevel", 2),
            new(99, "op_unk99", 4, "sub_800A210"),
            new(100, "op_unk100", 3, "sub_800A4DA"),
            new(101, "op_unk101", 2, "sub_800AC5C"),
            new(102, "op_unk102", 2, "sub_800ACAC"),
            new(103, "op_unk103", 4, "sub_800ACEA"),
            new(104, "DespawnCharacterAndRecordPosition", 2),
            new(105, "StackGetCharHP", 1),
            new(106, "StackGetCharEP", 1),
            new(107, "StackGetCharMaxHP", 1),
            new(108, "StackGetCharMaxEP", 1),
            new(109, "SetCharHP", 2),
            new(110, "SetCharEP", 2),
            new(111, "PlayAudioVolume", 2),
            new(112, "op_unk112", 2, "sub_800AE76"),
            // RENAMED 2026-09-16: were SetSaveExistsFlag_Bit2/Bit3. IDA readers: sub_8004CAC
            // gates the overworld pause-menu shortcuts on bit2 (medium confidence, unverified);
            // sub_8017ACC (world map draw) gates location marker highlighting on bit3 --
            // CONFIRMED in-game. See opcode_data.js for details.
            new(113, "EnableMenuAccess", 0),
            new(114, "EnableDragonRadar", 0),
            new(115, "SetActiveCharacterNoReset", 1),
            new(116, "ResetNonActiveSprites", 0),
            new(117, "op_unk117", 4, "sub_800A850"),
            new(118, "op_unk118", 4, "sub_800A89E"),
            new(119, "GetActiveEntityContext", 0),
            new(120, "op_unk120", 2, "sub_800AD3E"),
            new(121, "op_unk121", 2, "sub_800A004"),
            new(122, "DisableWorld", 1),
            new(123, "EnableWorld", 1),
            new(124, "op_unk124", 3, "sub_800AEF0"),
            new(125, "op_unk125", 0, "sub_800AF44"),
            new(126, "PushActiveCharIsTransformed", 0),
            new(127, "SpawnMapEntity", 3),
            new(128, "SetQuestFlag", 1),
            new(129, "PushSaveStatePtr", 0),
            new(130, "op_unk130", 0, "sub_800AFD4"),
            new(131, "PushLevelUpStat1", 0),
            new(132, "PushLevelUpStat2", 0),
            new(133, "PushLevelUpStat3", 0),
            new(134, "op_unk134", 3, "sub_800A276"),
            new(135, "op_unk135", 4, "sub_800A2C2"),
            new(136, "op_unk136", 4, "sub_800A346"),
            new(137, "op_unk137", 2, "sub_800A3A2"),
            new(138, "EnterSleepMode", 0),
            new(139, "AddEXP", 2),
            new(140, "op_unk140", 2, "sub_8009F3C"),
            new(141, "RegisterScouterEntry", 1),
            new(142, "op_unk142", 3, "sub_800A1B4"),
            new(143, "PushEntityPosition", 1),
            new(144, "SetEntityPosition", 3),
        };

        internal static readonly Dictionary<int, OpcodeInfo> IndexMap = ByIndex.ToDictionary(o => o.Index);

        // Name is not unique (index 87 and 92 are both "SpawnItem", same handler address
        // per BytecodeVM_OpCodes.md) -- assembling by name uses the FIRST/lowest index.
        internal static readonly Dictionary<string, OpcodeInfo> NameMap = ByIndex
            .GroupBy(o => o.Name)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Index).First());

        /// <summary>
        /// Resolves a typed opcode name the same way a human reader would, not just an
        /// exact match: case-insensitive, then substring (typing "transformation" finds
        /// SetCharacterTransformation), then typo-tolerant (a small Damerau-Levenshtein
        /// distance against any same-length-ish window of a candidate name, so e.g.
        /// "TranFsorMation" -- transposed letters -- still resolves). Tries exact match
        /// first so well-formed scripts are completely unaffected.
        /// </summary>
        /// <summary>
        /// Every name from <paramref name="candidates"/> that plausibly matches
        /// <paramref name="typed"/> -- for LIVE autocomplete filtering, where multiple
        /// results are expected and fine (the user picks from the list), unlike
        /// TryResolveFuzzy which needs exactly one answer. Prefix matches first (so
        /// normal typing behaves the same as a plain prefix-filtered list always did),
        /// then substring (typing "transformation" surfaces SetCharacterTransformation
        /// even though it doesn't start with that), then typo-tolerant as a last resort.
        /// Returns everything, unfiltered, once typed is empty.
        /// </summary>
        internal static List<string> FuzzyFilterNames(string typed, IEnumerable<string> candidates)
        {
            var all = candidates.ToList();
            if (typed.Length == 0) return all;

            string needle = typed.ToLowerInvariant();

            var prefix = all.Where(n => n.ToLowerInvariant().StartsWith(needle)).ToList();
            var substring = all.Where(n => !prefix.Contains(n) && n.ToLowerInvariant().Contains(needle)).ToList();
            if (prefix.Count > 0 || substring.Count > 0)
                return prefix.Concat(substring).ToList();

            const int maxDistance = 2;
            var fuzzy = new List<(string Name, int Distance)>();
            foreach (var name in all)
            {
                string candidate = name.ToLowerInvariant();
                int best = int.MaxValue;
                for (int start = 0; start <= Math.Max(0, candidate.Length - needle.Length); start++)
                {
                    int len = Math.Min(needle.Length + maxDistance, candidate.Length - start);
                    if (len <= 0) continue;
                    int dist = DamerauLevenshtein(needle, candidate.Substring(start, len));
                    if (dist < best) best = dist;
                }
                if (candidate.Length <= needle.Length + maxDistance)
                {
                    int dist = DamerauLevenshtein(needle, candidate);
                    if (dist < best) best = dist;
                }
                if (best <= maxDistance)
                    fuzzy.Add((name, best));
            }

            return fuzzy.OrderBy(f => f.Distance).Select(f => f.Name).ToList();
        }

        internal static bool TryResolveFuzzy(string typed, out OpcodeInfo? result, out string? error)
        {
            result = null;
            error = null;

            if (NameMap.TryGetValue(typed, out var exact))
            {
                result = exact;
                return true;
            }

            string needle = typed.ToLowerInvariant();

            var caseInsensitive = NameMap.Where(kv => kv.Key.Equals(typed, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Value).Distinct().ToList();
            if (caseInsensitive.Count == 1) { result = caseInsensitive[0]; return true; }

            var substring = NameMap.Where(kv => kv.Key.ToLowerInvariant().Contains(needle)).Select(kv => kv.Value).Distinct().ToList();
            if (substring.Count == 1) { result = substring[0]; return true; }
            if (substring.Count > 1)
            {
                error = $"'{typed}' matches multiple opcodes: {string.Join(", ", substring.Select(o => o.Name).Distinct().OrderBy(n => n))}. Be more specific.";
                return false;
            }

            // Typo-tolerant fallback: slide a same-length-ish window across every
            // candidate name and keep the smallest edit distance seen, tiny threshold
            // (<=2) so it only catches genuine near-misses, not unrelated short names.
            const int maxDistance = 2;
            var fuzzy = new List<(OpcodeInfo Op, int Distance)>();
            foreach (var group in NameMap.GroupBy(kv => kv.Value).Select(g => g.First()))
            {
                string candidate = group.Key.ToLowerInvariant();
                int best = int.MaxValue;
                for (int start = 0; start <= Math.Max(0, candidate.Length - needle.Length); start++)
                {
                    int len = Math.Min(needle.Length + maxDistance, candidate.Length - start);
                    if (len <= 0) continue;
                    int dist = DamerauLevenshtein(needle, candidate.Substring(start, len));
                    if (dist < best) best = dist;
                }
                if (candidate.Length <= needle.Length + maxDistance)
                {
                    int dist = DamerauLevenshtein(needle, candidate);
                    if (dist < best) best = dist;
                }
                if (best <= maxDistance)
                    fuzzy.Add((group.Value, best));
            }

            if (fuzzy.Count > 0)
            {
                int bestDistance = fuzzy.Min(f => f.Distance);
                var closest = fuzzy.Where(f => f.Distance == bestDistance).Select(f => f.Op).Distinct().ToList();
                if (closest.Count == 1) { result = closest[0]; return true; }
                error = $"'{typed}' is ambiguous between: {string.Join(", ", closest.Select(o => o.Name).Distinct().OrderBy(n => n))}. Be more specific.";
                return false;
            }

            error = $"Unknown opcode '{typed}'.";
            return false;
        }

        // Standard Damerau-Levenshtein (insert/delete/substitute/adjacent-transpose) --
        // transpose is what makes "TranFsorMation" (swapped 's'/'f') a distance-1 match
        // against the matching substring of "SetCharacterTransFormation", rather than
        // requiring 2 substitutions like plain Levenshtein would.
        private static int DamerauLevenshtein(string a, string b)
        {
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int val = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                        val = Math.Min(val, d[i - 2, j - 2] + 1);
                    d[i, j] = val;
                }
            }
            return d[a.Length, b.Length];
        }
    }
}
