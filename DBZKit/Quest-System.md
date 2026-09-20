# The Quest Log System

How the game's Quest Log actually works, confirmed from IDA (real ARM disassembly,
not guesses from pseudocode), and what Dragon Radar's new Quests tab does with it.

## Where it lives

There's a fixed table of 43 quests in the ROM, at address `0x086D68DC`
(`g_QuestEntries` in IDA, `Game.QuestTableOffset` in the tool config). Every entry
is 16 bytes:

```
int    Priority           -- 0, 1, or 2 in the real ROM. Picks an icon for the
                              "complete" list (indexes off_81D9C80). Not used at
                              all for the "in progress" list -- those all get the
                              same fixed icon.
byte*  isAvailableScript   -- tiny bytecode script, described below
byte*  isCompleteScript    -- same, but for "is this quest done"
char*  EntryName           -- the display string, e.g. "Enter the Cell Games"
```

The function that reads this table is `QuestLog_Build` (`0x80049FC`). It runs
every time the pause menu's quest log is opened. For each of the 43 entries, in
table order:

1. Run `isAvailableScript`. If it comes back true, the quest goes on the
   "in progress" list, using a fixed icon. Done with this entry.
2. Otherwise, run `isCompleteScript`. If true, the quest goes on the "complete"
   list, with an icon picked by `Priority`.
3. If neither is true, the quest doesn't show up at all.

So a quest is only ever "not started" (invisible), "in progress", or "complete" --
there's no separate "not started" list, it's just the absence of the quest from
both lists.

## The condition scripts

`isAvailableScript` and `isCompleteScript` aren't flag IDs sitting in the table --
they're pointers to actual bytecode, run fresh every time through the normal VM
(`BytecodeVM_ExecuteScript`). In the real ROM they're almost always tiny: push a
number, then run opcode 19 or 20 on it. For example, quest 1's complete script
(`0x086D66CD`) is just three bytes: `PushByte 191`, `Step 19`, `END`.

Opcode 19 (`StackTestStoryFlag`) pops the value on top of the stack, treats it as
a flag ID, and pushes back whether that flag is set. Opcode 20
(`StackTestStoryFlagNot`) is the same but inverted. The script's last value on
the stack when it hits `END` is the answer QuestLog_Build checks.

A handful of quests AND several flags together (opcode 4, `StackAnd`), e.g. quest
2's complete condition is `Flag(162) AND Flag(163) AND Flag(164) AND Flag(185) AND
Flag(186) AND Flag(187) AND Flag(188)` -- seven flags that all have to be set. A
few quests check something other than a flag, e.g. quest 29's available
condition is just `StackGetItemCount(45)` -- true if you're carrying item 45 (no
flag involved at all, just "do you have this item").

Flag IDs aren't limited to a byte -- some are pushed with `PushByte` (values
0-255, one byte) and some with `PushVarint` (a variable-length encoding), so IDs
above 255 do show up (quest 38's available condition tests flag 298).

## Where the flags actually live

This is the part that's easy to get wrong, because there are TWO unrelated flag
systems in this ROM, and until 2026-09-19 they had overlapping "quest"/"party
flag" names that made them look related. They've now been renamed in IDA and in
Legacy's opcode table to keep them apart:

**The one the quest log actually uses -- "story flags":** a flat bit array
inside the save state, at `g_PartyState + 0x180` (at least 38 bytes / ~300 bits
used). Set with opcode 27 (`SetStoryFlag`), cleared with opcode 28
(`ClearStoryFlag`), tested with opcode 19/20 (`StackTestStoryFlag`/
`StackTestStoryFlagNot`) as above. These are ordinary opcodes -- any script
anywhere in the game (an NPC's dialog, a map's entry script, a trigger zone) can
set one of these flags, and there's no separate "give quest" opcode. Advancing a
quest just means some script, somewhere, ran opcode 27 with the right number.

**The one that ISN'T the quest log -- "party member flags":** `PartyState_SetMemberFlag`
and `PartyState_TestMemberFlag` (opcode 128 `SetPartyMemberFlag` and opcode 23
`StackTestPartyMemberFlag` -- opcode 128 was called `SetQuestFlag` before the
rename, which is exactly the confusion this section exists to clear up). These
use a two-part key (category + sub-id) looked up through a 68-category table
(`g_PartyMemberFlagCategories`) into a different bit region at
`g_PartyState + 0x231`. Nothing in `QuestLog_Build` touches this at all. It looks
like a per-character or per-encounter status system that happens to share code
with the real story flags underneath, but it is a separate namespace. Don't
confuse the two when reading a script -- if you see opcode 128 or 23, it's not
talking to the quest log.

## The 43 quests, decoded

All of these come straight from the live ROM (see `questtest` scratch tool /
Dragon Radar's Quests tab -- nothing here is guessed):

| # | Name | Available when | Complete when |
|---|------|-----------------|----------------|
| 0 | Talk to Dende on the Lookout | (never -- literal 0) | Flag(195) |
| 1 | Enter the Cell Games | Flag(193) | Flag(191) |
| 2 | Return the Dragon Balls to Dende on the Lookout | Flag(191) | Flag(162) AND ... AND Flag(188) (7 flags) |
| 3 | Collect the seven Dragon Balls | Flag(162) AND ... AND Flag(188) (7 flags) | Flag(158) |
| 4 | Meet up with the others in the Briefs' residence to hear Cell's announcement | Flag(157) | Flag(154) |
| 5 | Talk to Bulma in Dr. Briefs' workshop | Flag(156) | Flag(154) |
| 6 | Meet Master Roshi at Capsule Corporation | Flag(154) | Flag(151) |
| 7 | Prevent Cell from absorbing Android 18 | Flag(151) | Flag(149) |
| 8 | Meet Trunks and Vegeta outside the Hyperbolic Time Chamber | Flag(149) | Flag(148) |
| 9 | Intercept the Androids on Master Roshi's Island | Flag(148) | Flag(144) |
| 10 | Meet Goku at Kami's Lookout | Flag(144) | Flag(143) |
| 11 | Check on Goku's progress at Master Roshi's island | Flag(143) | Flag(141) |
| 12 | Destroy the computer in Dr. Gero's lab | Flag(140) | Flag(136) |
| 13 | Investigate the disturbance in Gingertown | Flag(136) | Flag(132) |
| 14 | Find what came out of the time machine | Flag(132) | Flag(131) |
| 15 | Investigate the time machine outside Gingertown | Flag(131) | Flag(129) |
| 16 | Piccolo must see Kami at his Lookout | Flag(129) | Flag(127) |
| 17 | Follow Androids 16, 17, and 18 | Flag(127) | Flag(126) |
| 18 | Locate Dr. Gero's lab in the Northern Mountains | Flag(126) | Flag(86) |
| 19 | Meet up with the others on the Southern Continent | Flag(86) | Flag(77) |
| 20 | Intercept the Androids on Amenbo Island | Flag(77) | Flag(69) |
| 21 | Meet Goku in East District 439 | Flag(68) | Flag(66) |
| 22 | Retrieve the City Key and return it to the Mayor | Flag(66) | Flag(62) |
| 23 | Save the village from the Triceratops and report back to the Mayor | Flag(62) | NOT Flag(59) |
| 24 | Piccolo must talk to Goku or the Mayor about a problem in West City | NOT Flag(60) | Flag(57) |
| 25 | Get the Hercule Parade started to gain access to Piccolo | Flag(57) | Flag(30) |
| 26 | Go to West City | Flag(30) | Flag(29) |
| 27 | Meet Krillin and the others in the Northern Wastelands | Flag(26) | Flag(11) |
| 28 | Find the Mathbook | Flag(4) | Flag(2) |
| 29 | Get the Dragon Radar from Bulma | have item 45 | Flag(158) |
| 30 | Destroy the three power generators to drop Dr. Gero's barrier | Flag(97) AND Flag(112) AND Flag(123) | Flag(91) |
| 31 | Find the album Eyes of the Lion and give it to Hercule | Flag(34) | Flag(48) |
| 32 | Get an Open Faced Club Sandwich for Hercule | Flag(48) | NOT Flag(37) |
| 33 | Save the four Missing Children at West City Highway | Flag(43) | Flag(38) |
| 34 | Deliver the Scouter Part to Bulma | have item 22 | have item 24 |
| 35 | Deliver Bulma's note to the electronics store | Flag(205) | have item 23 |
| 36 | See Bulma at Capsule Corporation | don't have item 24 | Flag(30) |
| 37 | Find the 25 Golden Capsules for Dr. Briefs | have item 1 | Flag(206) |
| 38 | Find the seven Missing Nameks and return to Capsule Corporation | Flag(298) | Flag(211) |
| 39 | Train with Master Roshi | Flag(215) AND Flag(216) AND Flag(217) | Flag(69) |
| 40 | Rescue the man kidnapped by the Warlord | Flag(221) | Flag(55) |
| 41 | Investigate the Capsule Corporation espionage | Flag(210) | Flag(208) |
| 42 | Retrieve the Bait from West City and return it to the fisherman in Gingertown | Flag(219) | Flag(134) |

("have item N" / "don't have item N" are the `StackGetItemCount` cases -- opcode
21, not a flag test.)

Notice the flag numbers mostly count DOWN as you go through the early quests
(195, 193, 191, 188...162, 158, 157...) -- that's the main story's flag numbering,
handed out roughly in play order. The side-quest-looking entries near the end
(29 through 42, priority 1/2) use flags out of that sequence (205, 206, 211, 215,
298...) because they're not part of the main flag countdown.

## What Dragon Radar shows now

**Quests tab** (in the right-hand sidebar, next to Properties): lists all 43
quests exactly as decoded above -- name, priority, both conditions, and the flag
IDs involved. Loads automatically when you open a ROM.

**Trigger properties panel:** when you click a trigger zone on the map, if that
trigger's action script sets or clears a flag that matches one of the 43 quests'
conditions, it says so right there ("Quest link found -- this trigger's script
sets/clears a flag a quest checks: ..."). This is how you find out which trigger
on a map is the one that actually advances a given quest.

This deliberately does NOT try to link a walking/talking NPC (`EntityKind.Character`
in the properties panel) directly to a quest, because that link genuinely doesn't
exist in the ROM -- see `NPC-Dialog-and-Quests.md` for the full trace, but the short
version is: a walking NPC's sprite and its dialogue are two completely separate
placements (a `MapScript_CreateCharacter` record for the sprite, a Dialog-type map
trigger zone for the conversation), with nothing connecting them except that
whoever built the map put both in roughly the same spot. The trigger IS the real
hook, which is exactly what the check above already uses.

**Objects (Math Book etc):** `Map_Object.onPickup` (+0x10) is a *native* function
pointer (`MapObject_OnPickup_RunCollectionMsg`, 0x800FFE6), not bytecode. It runs
`collectionMsg` (+0x14) as a dialog `sequence[]`, and the flag write is a mode-0 entry in
that sequence. Confirmed example: Mathbook object 0x83EED68 -> sequence 0x83BB7A8 ->
entry 0x83BB792 = `SetStoryFlag(4)`, which is quest 28's "available" flag. Find Flag Usage
scans every object's collectionMsg for exactly this.

A trigger's payload can be either raw bytecode OR a dialog `sequence[]` array whose
individual lines can run bytecode mid-conversation (`DrGero.Quests.DialogScanner`,
see the NPC dialog doc) -- `FindQuestsSetByScript` tries both, since there's no way
to tell which one a given trigger is without inspecting its vtable.

## How to actually edit a quest

**Change the display text:** `EntryName` is a normal null-terminated string
pointer. If your replacement text is the same length or shorter, you can just
overwrite the bytes in place. If it's longer, write the new string to free space
at the end of the ROM (same trick used everywhere else in this project) and patch
the table's `EntryName` pointer to point there.

**Change which flag a quest depends on:** find the `PushByte`/`PushVarint`
instruction right before the `Step 19`/`Step 20` in the condition script and
change the number it pushes. If the old and new IDs are both under 128 (so both
fit in a single `PushByte`), you can patch the one operand byte directly, in
place, no reallocation needed. If you're changing between a one-byte ID and a
two-or-more-byte one, the instruction lengths differ and the script needs to be
rewritten to free space and re-pointed, same as the text case above.

**Make a quest that depends on more than one flag:** write a new script using
`PushByte`/`PushVarint` + `Step 19`/`20` pairs chained with `StackAnd` (opcode 4)
in between, ending in `END` (opcode 0x11) -- copy the exact byte pattern from
quest 2 or quest 30's complete condition above as a template.

**Make something actually SET a quest's flag:** put opcode 27
(`SetStoryFlag`) with the flag's ID pushed right before it, anywhere in any
script that should trigger the quest step -- an NPC's dialog script, a trigger's
action script, a map's entry script. That's genuinely all it takes; there's no
registration step, no "this script belongs to this quest" link anywhere. The
quest log just polls all 43 conditions fresh every time it's opened.

**Add a whole new quest / more than 43:** the table itself would need to grow,
which means relocating it to free space (same free-space + pointer-patch pattern
as everything else) and updating `QuestLog_Build`'s hardcoded loop bound (`43`,
at `0x8004AE4`) and its two fixed-size output buffers (`ArenaAlloc(1092)` for the
whole build, plus the two list arrays inside it) to fit more entries. Not
attempted yet -- the buffer sizing needs to be worked out for real before this is
safe, so treat it as open work, not a solved recipe like the two above.

## Code

- `DrGero/Quests.cs` -- `QuestEntry`, `QuestReader.ReadAll`, `ConditionDecoder`
  (turns a condition script into the readable `Flag(191)`-style text above), and
  `InstructionDecoder` (a general script decoder, independent of the Legacy
  project, used to scan any script for flag-setting opcodes).
- `Dragon Radar/DragonRadarUI.cs` -- Quests tab (`PopulateQuestList`) and the
  trigger-to-quest link check (`FindQuestsSetByScript`).
- `DrGero/Config.cs` / `Dragon Radar/games/ALFE.json` -- `QuestTableOffset`
  (`0x6D68DC`) and `QuestCount` (`43`).

In IDA: `g_QuestEntries` is renamed and typed as `QuestEntry[43]` (was
"QuerstEntries", a typo, now fixed), with a comment on it explaining the two
flag systems so nobody re-confuses them later.
