> **CORRECTION (2026-09-19, later the same day):** the claim below that *no* character entity can start a
> conversation is **wrong for ordinary town NPCs**. NPCs spawned by `MapScript_CreateNpcSprite`
> (handler 0x0800B711, 245 records) have `MapEntity_HandleCollisionInteract` in their OnCollision
> slot, and their record's `+0xC` is the dialog sequence it opens. The earlier check looked at the
> wrong vtable. Enemies and script actors (0xD6DF) still have no interact path, so a Dialog trigger is
> still the way to give *those* dialogue. See `Engine-Notes.md` and `NPC-and-Enemy-Spawning.md`.

# How NPCs, Dialogue, and Quests Actually Connect

This follows on from `Quest-System.md` -- read that first for the quest table
itself. This one is about the other half of the question: when you walk up to an
NPC and talk to them, what actually happens, and how does that end up setting a
quest flag? All of this is from real IDA disassembly, not guesses.

## The short version

A walking NPC's sprite and its dialogue are two completely separate things in
the ROM. There is no field anywhere that links "this sprite" to "this
conversation". Bumping into an NPC does nothing by itself -- dialogue only
starts because a Dialog-type trigger zone happens to be placed on or near the
NPC. Whoever built the map had to place both by hand and line them up visually;
the game doesn't connect them for you.

Once a conversation is running, individual lines can silently run a bit of game
logic (give an item, set a flag, anything a normal script can do) in between
lines of text. That's the actual mechanism a "talk to this NPC to advance a
quest" moment uses -- somewhere in that NPC's conversation there's a line that
runs `SetStoryFlag` instead of showing text.

## Why it's not the sprite's own doing (bumping into an NPC)

Every character-type entity in this ROM -- the player, party members, generic
map NPCs (spawned via `MapScript_CreateCharacter`), and enemy/Scouter-target
entities -- shares the same chain of fallback collision handlers:

```
CharacterSprite_OnCollision / CombatEntity_OnCollision
  -> Entity_HandleCollision_Shared      (0x8007B08)
  -> Entity_HandleCollision_Shared_Ext1 (0x8019B70)
  -> Entity_HandleCollision_Shared_Ext2 (0x800786A)
```

Collision "type 3" is the interact/talk case. Every single link in that chain
was checked, and the last one explicitly does nothing for type 3 -- it's a dead
end. There's a function, `MapEntity_HandleCollisionInteract`, that clearly WOULD
start a conversation (it reads the entity's own stored dialogue fields) but
nothing in this chain, or any entity constructor found, ever calls it. It looks
like leftover code from an earlier version of the collision system that got
replaced by the trigger-based approach below, without anyone deleting the old
function.

So: walking into an NPC and pressing A does not, on its own, start a
conversation for any character type in this game.

## What actually starts a conversation: trigger zones

Map triggers (the same rectangular zones Dragon Radar already draws in green)
come in a few flavors sharing one general-purpose engine
(`MapTrigger_OnEnter`/`MapTriggerDialog_Update`). When the player's position
overlaps a trigger's rectangle and its condition passes, the engine reads a
pointer at a fixed offset in the trigger's data and hands it to
`InteractionHandler_Init`, which is the same function that runs any
conversation. That fixed offset is the trigger's `dataPtr + 0x10` (this was
originally misread as `+4` while building the Dragon Radar integration below --
`+4` is actually a separate embedded sub-object, not the payload; fixed).

What's at that offset can be one of two things, and there's no flag anywhere
saying which:

- **A real conversation** -- a `sequence[]` array (see below).
- **Raw bytecode directly** -- some triggers reuse the exact same plumbing to
  just run a script with no dialogue at all (confirmed real example: one of
  Zone 1's triggers has three bytes there that just set a flag, no text).

Dragon Radar's trigger properties panel and quest-link check both try reading
it both ways and use whichever one produces something sensible.

## Conversations: how one line leads to the next

A conversation is a `sequence[]` array -- a flat list of pointers, one per line
or step, ending in a sentinel (any pointer whose value doesn't look like a real
ROM address). You don't walk it in order: each entry carries the index of the
NEXT entry to jump to once it finishes, baked into its own 2-byte header. That's
how branches and loops in dialogue work -- an entry can point anywhere in the
array, not just "the next one physically after it".

Each entry starts with `[u16 NextIndex][u8 Mode]`, and `Mode` picks what kind of
step it is:

| Mode | What it does |
|---|---|
| 0 | **Runs a script, shows no text at all.** This is the hook point. |
| 1 | Plain text box |
| 2 | Compressed plain text box |
| 3 | Text box with `%s`/`%d` filled in by a small script (e.g. inserting the active character's name) |
| 4 | Same as 3, but the text itself is compressed |
| 5 | Jump to a different point (not fully traced yet -- treat as unreliable) |

Mode 0 is the important one. A real, confirmed example from this ROM: the
Yajirobe "Senzu Bean" conversation has a mode-0 entry mid-conversation that's
just `PushByte(itemId); Step(PickUpItem); END` -- three bytes, no text box at
all, chained straight into the next entry (the "You received a Senzu Bean!"
message) via its own `NextIndex`. The player never sees this step happen; it
just silently gives them the item between two lines.

**A quest-flag-setting NPC works exactly the same way.** Somewhere in its
conversation's `sequence[]`, there's a mode-0 entry whose script is
`PushByte(flagId); Step(SetStoryFlag); END`, chained in between two lines of
dialogue the same way the Senzu Bean pickup is. There's no special "quest step"
concept in the engine at all -- it's the same generic mechanism used for every
other mid-conversation game action.

Full byte-level format for all 6 modes, with more real examples, is in
`Dialog_Format.md`.

## The two flag systems, one more time (now with the visited-map finding)

`Quest-System.md` already covers this, but one thing was confirmed since:
`Map_LoadInternal` -- the function that runs every single time you load into a
map -- calls the "party member flag" function
(`PartyState_SetMemberFlag(&g_PartyState, zone, area)`) unconditionally on every
map load. So at least part of what that system tracks is "have you ever been to
this zone/area" -- nothing about individual party members despite the name.
That's on top of it also being usable as a general two-part-key flag store by
scripts (opcode 23/128). Either way: it is NOT what the Quest Log reads. If
you're trying to figure out why a quest isn't updating, you want the story-flag
opcodes (19/20/27/28), not these.

## What Dragon Radar does with all this

- **Quests tab** (now a full top-level tab next to Map Viewer, not squeezed into
  the sidebar): the full decoded quest table.
- **Trigger properties panel**: reads the trigger's payload at the corrected
  `dataPtr+0x10` offset, tries decoding it as both raw bytecode and as a
  dialog `sequence[]` (walking every mode-0 entry's script), and reports any
  quest whose flag gets set or cleared anywhere in there.
- Deliberately does NOT try to guess a link between an NPC's sprite and a
  quest directly -- per the finding above, that link doesn't exist in the ROM,
  so guessing one would just be misleading. The trigger is the real anchor.

## Code

- `DrGero/Quests.cs` -- `DialogScanner` (`LooksLikeDialogSequence`,
  `FindFlagWritesInDialog`) walks a `sequence[]` array and decodes mode-0
  entries' scripts. `InstructionDecoder`/`FindFlagWrites` (already existed for
  the quest condition scripts) handles the raw-bytecode case.
- `Dragon Radar/DragonRadarUI.cs` -- `FindQuestsSetByScript` now tries both
  interpretations against the trigger's `dataPtr+0x10` payload.
