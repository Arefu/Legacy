# How NPCs and Enemies Are Spawned (and How Dragon Radar Places Them)

Everything here was read out of IDA (2026-09-19) and checked against the real ROM's
`mapScripts[]` for all 327 maps. See `Engine-Notes.md` for the wider engine picture and
`NPC-Dialog-and-Quests.md` for dialogue (note its correction banner: town NPCs *can* talk).

## The short version

Every walking thing on a map -- Man, Woman, Child, Dog, and every enemy -- is a **record in
`MapEntry.mapScripts[]`**. `Map_LoadInternal` calls each record's first word (the handler)
after checking its condition. Three handlers matter:

| Handler | Function | Records in ROM | What it spawns |
|---|---|---|---|
| `0x0800D6DF` | `MapScript_CreateCharacter` | 197 | script-driven **actor**: not solid, not talkable |
| `0x0800B711` | `MapScript_CreateNpcSprite` | 245 | **town NPC**: solid, talkable (dialog at record+0xC) |
| `0x0800E77F` | `MapScript_CreateEnemy` | **666** (167 maps) | enemy / Scouter target |

Before this work only the first two were read (and the second was mis-described as scenery). The 666
enemy records were skipped entirely.

## Behavior lists (the "funky" part)

Characters and enemies don't have hard-coded AI. Each carries a **behavior list**
`{u32 count; ptr[count]}` that `EntityBehaviorList_Tick` (`0x800D156`) cycles forever: run the
current behavior's update until it finishes, call its finish, advance (wrapping), create the
next. Each entry's first word is a create function:

- `0x0800D625` -- waypoint move: `{fn, u16 x, u16 y, u32 param(0x80)}`
- `0x0800CB0F` -- **face a direction and wait**: `{fn, u32 flags}` -- byte 2 = facing, low 16 bits =
  ticks to wait (`0x7D00` is about 9 minutes). This is *standing still*, not wandering (an earlier version
  of this doc had that wrong).
- `0x0800CFDD` -- wander, blocked by walls and by the player (what a solid town NPC uses); param = speed.
  The full behavior table is in `Engine-Notes.md`.

For a script actor (0xD6DF) the list is **inline at record+0x10**, and the two fields IDA's old struct called
`next`/`spawnFunc` (+0x18/+0x1C) are really its one inline behavior object.

## Enemy record (`MapScriptEnemyRecord`, declared in the IDB)

```
+0x00 handler     0x0800E77F
+0x04 flags       spawn condition (0 = always)
+0x08 statIndex   g_ScouterStatDatabase index (24-byte entries: hp/str/pow/end...)
+0x0C x, +0x0E y  int16 pixels (same units/origin as NPCs; entity pos = x<<8)
+0x10 spriteId    Character_GetSpriteId -- same id space as NPCs (>= 7)
+0x14 actionObj*  {func, payload} -- see below
+0x18 count       behavior count (always >= 1 in shipped data)
+0x1C ptr[count]  behavior list (waypoints, idle...)
```

Size is `0x1C + 4*count`. Waypoints are never shared between enemies.

`actionObj.func`: `0x0801069B` = payload is VM bytecode (`EnemyAction_RunScript`), `0x080106B3`
= payload is a dialog `sequence[]` handed to `InteractionHandler_Init`
(`EnemyAction_RunDialog`, 27 records), `0x08010699` = nothing. **When the game fires this
object is not yet traced.** It is not the collision/interact path (that dead-ends, see the
other doc), so it should not be relied on as "talk to this enemy".

## A bug this fixed

`PersistNewCharacters` treated `actionData` as a plain value and copied the template's
first word (the count) while leaving `ptr[0]` null. The behavior cycler would have called
through that null.

A second, bigger problem: it placed **script actors** (0xD6DF). Those are not solid (they are never
registered in the collision lists), are not talkable, and stood still by design -- exactly the reported
"idle NPCs don't move and can be walked through". Placed NPCs now use the **0xB711 handler** (solid and
talkable, dialogue pointer at record+0xC, starter line "Hello!") with a choice of standing or wandering.
Script actors remain available as a separate placement mode.

## What Dragon Radar does now

- **Enemies are read and drawn** (orange-red, same sprite icons as NPCs) with their stat
  index shown. `EnemyRecords` in `DrGero/Enemies.cs` owns the layout.
- **Dragging an enemy drags its waypoints** by the same delta, so its patrol keeps its shape.
- **NPCs tab has a mode box:** Standing NPCs, Wandering NPCs, Script actors, or Enemies. Enemies mode lists the
  `(stat, sprite)` pairs the game ships, most common first (70 of them). A new enemy gets its
  own freshly allocated action object (cloned from the most common one) and a single waypoint
  at its own spawn point, so it stands still.
- **Edit Dialogue on a talkable NPC** edits the conversation at record+0xC (micro-editor kind `dialog`).
- **Add Dialogue Trigger Here** (Properties tab, for a saved script actor or enemy) builds a Dialog
  trigger zone around the sprite and opens its dialogue editor. For actors and enemies this is the
  dialogue link that is confirmed to work.
- **Edit Script / Edit Dialogue on an enemy** edits its own action object through the
  micro editor's new `enemy` kind, and sets the func word to match what was authored.

## Not done / not proven

- What fires `actionObj` on an enemy.
- Multi-waypoint patrols can't be authored yet (new enemies get one waypoint).
- Enemy names: the stat table has no name field that was identified, so the palette shows
  `Enemy <stat> x<count>` with the sprite as its thumbnail.
- Placed in-memory only, like every other edit -- use Save ROM As to write a file. Not tested in
  a running emulator.
