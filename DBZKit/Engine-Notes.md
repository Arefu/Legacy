# Engine notes (US ROM, ALFE) -- toward adding maps, scripts and dialogue

Everything here was read from IDA (2026-09-19) and, where it says **verified**, checked against the
ROM's own data. Certainty is marked: **HIGH** (decompile + data agree), **MEDIUM** (decompile only),
**LOW** (inferred). All names below exist in the IDB.

## 1. What spawns things on a map

`Map_LoadInternal` (0x800C390) runs, in order: LevelGates (`npcArray`), items, objects, triggers,
music, then **`mapScripts[]`** -- each record's first word is a handler called after its
`flags` condition passes. Handlers seen in the ROM:

| Handler | Function | Records | Result |
|---|---|---|---|
| 0x0800B711 | `MapScript_CreateNpcSprite` | 245 | **talkable, solid town NPC** |
| 0x0800D6DF | `MapScript_CreateCharacter` | 197 | script-driven actor: not solid, not talkable |
| 0x0800E77F | `MapScript_CreateEnemy` | 666 | enemy / Scouter target |

Records (all HIGH, verified on the whole ROM):

```
NPC sprite  (0xB711)   +0 handler  +4 flags  +8 x,y(int16)  +0xC dialogSeq*  +0x10 spriteId
                       +0x14 behavior count  +0x18 behavior ptr[count]      (size 0x18+4n)
Actor       (0xD6DF)   +0 handler  +4 flags  +8 x,y          +0xC spriteId
                       +0x10 behavior count  +0x14 ptr[count]  (its one inline behavior sits at +0x18)
Enemy       (0xE77F)   +0 handler  +4 flags  +8 statIndex  +0xC x,y  +0x10 spriteId
                       +0x14 actionObj*{func,payload}  +0x18 count  +0x1C ptr[count]
```

- `spriteId` goes through `Character_GetSpriteId`: 0 = the player, 1-6 = party slots (dynamic),
  >= 7 = `g_CharacterSpriteIndex[id]` (frames, hit boxes).
- **Talking:** the NPC-sprite vtable (`NpcSprite_vtable` 0x80255CC) has
  `MapEntity_HandleCollisionInteract` in its OnCollision slot, so collision type 3 opens the dialog
  sequence at record+0xC (0 = not talkable). 242 of 245 records have one. The old "no character
  can talk, only trigger zones can" conclusion came from checking the wrong vtable (0x802560C).
- **Solid:** `NpcSprite_Create` registers the NPC in `g_WorldCollisionMap` and `g_EntityBlockList`;
  actors (0xD6DF) do not.
- Enemy `actionObj.func`: 0x0801069B run bytecode, 0x080106B3 open a dialog sequence, 0x08010699
  nothing. **When the game fires it: not traced (LOW).**

## 2. Behavior lists (movement / AI)

`EntityBehaviorList_Tick` (0x800D156) cycles `{count, entries[]}` forever: run the current
behavior's update (vtable slot 2) until it returns nonzero, call finish (slot 1), advance and wrap,
create the next through `entry[0]` (a create function). Behavior records seen (counts = uses):

| create fn | name | rec after fn | meaning |
|---|---|---|---|
| 0x08010099 (112) | `NpcBehavior_StandWait_Create` | ticks | animation 0, wait N ticks (0x7D00 ~ 9 min) |
| 0x0800CB0F (390) | `NpcBehavior_FaceAndWait_Create` | flags: byte2 = facing, low16 = ticks | face a direction, wait |
| 0x0800CFDD (57) | `NpcBehavior_WanderSolid_Create` | speed (0x59) | wander, blocked by walls **and the player** |
| 0x0800D061 (32) | `NpcBehavior_WanderWorld_Create` | speed | wander, walls only |
| 0x0800D0AF (93) | `NpcBehavior_WanderEnemyBlocked_Create` | speed | wander, enemy move test |
| 0x0800D0FD (27) | `NpcBehavior_WanderAnimTable_Create` | speed | wander, own anim table |
| 0x0800D00F (28) | `NpcBehavior_WanderRect_Create` | speed + 4 x int16 rect | wander inside a rect |
| 0x0800D5FF (227) | `NpcBehavior_WalkTo_Create` | u16 x, u16 y, speed | walk to a point |
| 0x0800D625 (1096) | `NpcBehavior_WalkToAnimated_Create` | u16 x, u16 y, speed | walk animation + walk to a point |
| 0x080100BF, 0x08011E1D, 0x0800CB47, 0x0800D093, 0x0800D0E0 | ... | | rare, see IDB |

`WanderBehavior_Init(obj, speed)`: step = `((32*speed+2047)&~0x7FF)>>8` px, chance = `16*speed`.
"Idle NPCs don't move and can be walked through" was because Dragon Radar placed **actors** (0xD6DF)
with a 9-minute face-and-wait. Placed NPCs now use 0xB711 with StandWait or WanderSolid. (HIGH)

## 3. The map system

- `g_MapEntries` (0x08409368) is an array of 0x38-byte `MapEntry`. `Map_FindEntry(zone, area)` is a
  **linear scan** of `Map_GetCount()` entries; `Map_GetCount` is the constant 327 encoded as
  `FF 20 48 30` (`MOVS R0,#0xFF; ADDS R0,#0x48`) at file offset 0xE0AC. (HIGH)
- The table address appears in **four code literals**: file offsets 0x5ADC (`Draw_DebugTestMenu`), 0x5D5C
  (`Debug_LoadMapFromSelection`), 0xEBD8 (`Map_FindEntry`) and 0x195C, which is `&g_MapEntries[3]` (the new-game
  map). To add maps: copy the table to free space with room, patch those four words and the count. (HIGH;
  a ROM-wide search found no other pointer into the table from code.)
- Two maps with the same `(zone, area)` are distinguished by `variation`; a `MapEntry`'s
  `variationArray` holds `Map_VariationEntry*` (0x50 bytes), index `variation-1`.
- **A new game starts on entry index 3** (Z1A4) at that variation's `defaultSpawn`. (HIGH)
- `Map_Load(state, zone, area, spawn*, variation, flag)`; warp opcode
  `BytecodeVM_WarpToMapWithEntities` pops `y, x, variation, area, zone`.
- `PartyState_SetMemberFlag` marks a (zone, area) visited only if it is in `g_AreaVisitTable`;
  new maps are silently ignored there, so they work without touching it. (HIGH)
- `MapEntry.mapNameIndex` is an id (< 0x10000) or a VM script pointer returning the id;
  221 (0xDD) = no title banner.

`Map_VariationEntry` (fields HIGH unless noted):

```
+0x00 bgBlendConfig   +0x04 bgPriorityPacked   +0x08 width u16   +0x0A height u16
+0x0C layerSlotCount  +0x10 layerSlots*        (purpose LOW)
+0x14..+0x20 bgLayer0..3 descriptors           (first word = create function)
+0x24 graphicObjectCount  +0x28 graphicObjects (24-byte records, decorations)
+0x2C spriteSheetData (LOW)   +0x30 defaultSpawn = INLINE Point (x,y int16)
+0x34 collisionMapData   +0x38 "tilemapGraphicsData" (both ResourceHeader*, 0x2000 bytes each; +0x38 is
                       probably a SECOND walkability-style bitmap edited by opcodes 117/118, see IDA -- MEDIUM)
+0x3C useAffineBg   +0x40 variationTriggerCount  +0x44 variationTriggers
+0x48 staticTilesetDeltaData   +0x4C tileBankTable (shared, 0x084DF574)
```

- **ResourceHeader** `{u32 mode, u32 size}` then data: **mode 0 = raw memcpy**, mode 1/2 = JCALG1.
  So new map data can be stored **uncompressed** -- no compressor needed. (HIGH)
- Layer descriptors: 0x080056A1 `TiledLayer` (standard), 0x08004FCD `AffineEffectLayer` (rain etc.),
  three rare ones on entry 176 only.
- Static tileset: `staticTilesetDeltaData` decodes to u16[]; delta `e[i]=e[i-1]+e[i]+1`; high byte =
  bank in `tileBankTable`, low byte = 64-byte tile inside a 256-tile bank; DMA'd sequentially into VRAM.
- **Collision** is a 256x256-tile bitmap (2048x2048 px, 8 u32 per row); **set bit = walkable**.
  `Collision_CheckRect` treats out-of-range as blocked.

### To add a map (recipe, not yet automated)
1. Author width/height, the four layer descriptors (reuse the TiledLayer format), collision bitmap,
   the +0x38 bitmap (copy an existing one) and the delta list; store blobs as ResourceHeader mode 0.
2. Build a `Map_VariationEntry` (copy an existing one and change fields) and a `variationArray`.
3. Append a `MapEntry`; relocate `g_MapEntries` if there is no room; patch the four literals and
   `Map_GetCount`.
4. Reach it by a warp opcode script or the debug menu below.

## 4. Debug menu (Music / Sample / Map Test)

`DebugMenu_Create` (0x80056B0, was `NewGame_Create`) is a leftover menu with a **Map Test** that loads any
map with a level-40 party (`Debug_LoadMapFromSelection`). The title screen only reaches it with reason
code 2, which `TitleScreen_HandleInput` never produces. To enable: patch file offset **0x574**
`1E F0 E2 FB` (`BL GameIntroVideo_Create`) to `05 F0 9C F8` (`BL DebugMenu_Create`); then idling 30 s at
the title opens the debug menu. Encoding verified by regenerating the original bytes. (HIGH; not run in
an emulator)

## 5. Still unknown
- What runs `EnemyAction`, and `layerSlots` (+0x10).
- `spriteSheetData` (+0x2C), `graphicObjects` record layout.
- The save-slot and options screens beyond their entry points.

## 6. IDA naming status (2026-09-19, autonomous pass)

Convention for uncertain names: `BytecodeVM_EntityCmd_<vtable>_<n>arg` = a script opcode that pops a
charIdx plus n args and enqueues a command object (vtable `0x<vtable>`) on `g_CommandQueue`; what the command
does is in that vtable's slots (**relative** offsets: function = word + vtable address). Every renamed
function carries a `[certainty ...]` tag in its IDA comment; treat LOW as a hypothesis.

Done: entity spawning/behaviors, the scene manager (`EntityList_*`, `ObjVram_*`), collision (`Collision_*`,
`CollisionList_InsertRemove`), the map loader/renderer setup, the resource loader, the title/debug flow, and
all but a handful of the VM opcode handlers (the leftovers now have structural names). The IDB types
`MapScriptNpcRecord`, `MapScriptSpawnMultiConditional`, `MapScriptEnemyRecord`, `EntityBehaviorList`,
`EntityBehaviorState`, `ResourceHeader` and a corrected `Map_VariationEntry` are declared and applied.

Good next targets: the `EntityCmd` vtables above (decode slots), `sub_8011FA6`/`sub_8012840` (entry-176
layers), `MapRenderer` VRAM/palette upload, the save/load code (`SaveSlot_*`), the sound engine, and
`Dialog_ProcessNext`'s mode handlers. About 1,500 functions are still `sub_XXXX`.

## 7. Goku is removed after Cell (and the one-byte fix)

Cell (Z14 A3, enemy stat 25, sprite 30) has an enemy action object `{EnemyAction_RunDialog, 0x083EC758}` -- so this
is the dialog that runs when he is beaten. The end of that 55-entry sequence is "Hercule is now an available
character!", a script, then "Journal Updated!". The script does `PushByte 4; Step 91` = **ClearCharacterInParty(slot 4)**,
which removes Goku permanently. Changing the step byte at file offset **0x3EC738** from 0x5B (91) to 0x4F (79,
SetCharacterInParty) keeps him. DBZKit's Engine tools has this as a checked, reversible patch. (HIGH that this is
the removal; not run in an emulator, and nothing else was found that clears him.) It also shows enemy action
objects DO fire -- at least this one, on defeat.

## 8. DBZKit: the "Engine tools" tab
`DBZKit/EngineToolsPanel.cs` (a tab, loaded when a ROM is opened) over `DrGero/EngineTables.cs`: default party (stats and abilities), display rows
(add/copy, with sprite preview), the ability table (add/copy), verified patches (Keep Goku, debug menu on title
idle), and a map-table extender. All edits go to a private ROM copy; "Apply to DBZKit" hands it back, "Save ROM As"
writes it. Dragon Radar reads the map table's location and count from the ROM, so it loads extended ROMs.

## 9. Legacy's dialogue tree now covers NPCs and enemies
Legacy used to index dialogue only from triggers, items and objects (76 trigger dialogues), so it missed **242 town-NPC
conversations** (`mapScripts[]` handler 0x0800B711, record+0xC) and **27 enemy dialogues** (handler 0x0800E77F whose action
object is `EnemyAction_RunDialog`), e.g. Cell's defeat dialogue in Z14 A3. It now walks `mapScripts[]` too, and reads the map
table's address and count from the ROM so extended ROMs load. Verified by running its tree builder against the ROM.
