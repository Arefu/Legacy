# BytecodeVM Reference (WIP)

Derived from `1085 - Dragon Ball Z - The Legacy of Goku II (U)(TrashMan).gba.i64`, session opened 2026-09-13.
Cross-checked against `DBZKit/Legacy/SView_Decode.cs` and `DBZKit/DBZKit/Assets/Dialog.cs`.

## Architecture — three separate tables, not one

The disassembly shows the "bytecode VM" is actually three distinct dispatch tables, which
`SView_Decode.cs` conflates into a single switch. `OpCodes.csv` is the **third** table below.

| Table | Address | Entries | Selected by | Purpose |
|---|---|---|---|---|
| `BytecodeVM_MainDispatchTable` | `0x83B5C00` | 30 (opcodes `0x00`-`0x1D`) | top-level script byte | stack ops, control flow, `Step`, `PushVarint` |
| `BytecodeVM_OpcodeTable` | `0x83B5D28` | ~146 | operand byte of opcode `0x02` (`Step`) and `0x15`-`0x1A` (`StepTypePost_*`) | this is `OpCodes.csv` — all the `BytecodeVM_*` game actions |
| `BytecodeVM_TypeHandlerTable` | `0x83B5F70` | small | operand byte of opcode `0x03` and the second half of `0x15`-`0x1A` | get/set of a "variable" (local slot vs global vs quest flag, etc.) — used for l-value access |

Main loop: `BytecodeVM_ExecuteScript` (`0x80092E4`) reads one byte, indexes
`BytecodeVM_MainDispatchTable`, and calls the handler until it hits opcode `0x11`, whose table
slot is `NULL` (confirmed by reading the raw table bytes — offset 17 is `00 00 00 00`). That is
the **only** valid terminator. Opcode `0x1E` used in `SView_Decode.cs` as a second "END" case is
dead code — the real table only has 30 entries, so a `0x1E` would read garbage past the table.
**Recommend deleting that case.**

### Bug found: Jump / JumpIfFalse / LoopOrJump operand size

`SView_Decode.cs` decodes the operand of opcodes `0x12` (Jump), `0x13` (JumpIfFalse) and `0x1C`
(LoopOrJump) as a LEB128 varint + zigzag, same as `PushVarint`. That's wrong. Decompiled:

```c
// BytecodeVM_Jump (0x80090FE)
result = *pc + *pc[0] + 1;   // target = (addr of next byte after operand) + signed_byte(*pc)
*pc = result;

// BytecodeVM_JumpIfFalse (0x800910A)
// pops cond; pc += 1; if (!cond) pc += signed_byte(operand)

// BytecodeVM_LoopOrJump (0x800926C)
// reads a loop-counter slot in ctx+0x44; on last iteration falls through (pc+=1, no jump),
// otherwise pc -= signed_byte(operand)   <-- backwards branch for loops
```

All three take **one signed byte**, not a varint. `Dialog.cs`'s `SkipScript` already guessed this
correctly (`pos += 1` with a "TODO: confirm operand size (guessing 1 byte)" comment) — that guess
is confirmed correct. `PushVarint` (`0x01`) and `PushMultiVarint` (`0x1D`) **do** use real LEB128 +
zigzag — those two are correctly decoded in both C# files.

`LoopOrJump` also isn't a plain jump — it decrements a counter (VM state `+0x44`, an array indexed
by nested-loop depth) and only branches backward while the counter hasn't hit 1. It's a
"decrement-and-loop" instruction, closer to a `dbnz` than a raw jump.

## `BytecodeVM_MainDispatchTable` (0x00–0x1D)

Matches `SView_Decode.cs` cases 1:1 except for the Jump-family operand size noted above:

| Op | Name | Operand |
|---|---|---|
| 0x00 | PushByte | 1 byte, unsigned |
| 0x01 | PushVarint | LEB128 + zigzag |
| 0x02 | Step | 1 byte → `BytecodeVM_OpcodeTable` |
| 0x03 | (type handler dispatch, `sub_8008FA4`) | 1 byte → `BytecodeVM_TypeHandlerTable`, pops the value/target off the VM stack |
| 0x04-0x10 | StackAnd/Not/Negate/Add/Sub/Mul/Div/CmpEq/Ne/Gt/Ge/Lt/Le | none |
| 0x11 | END (NULL table slot) | — |
| 0x12 | Jump | **1 signed byte**, relative |
| 0x13 | JumpIfFalse | **1 signed byte**, relative, pops cond |
| 0x14 | StackPop | none |
| 0x15-0x1A | StepTypePost_{Pop,IncPeek,Peek,PopDec,DecPeek,PeekDec} | 1 byte → OpcodeTable, then 1 byte → TypeHandlerTable (post-inc/dec variable access) |
| 0x1B | PushToAltStack | none |
| 0x1C | LoopOrJump | **1 signed byte**, decrement-and-branch |
| 0x1D | PushMultiVarint | 1 count byte + N × (LEB128 + zigzag) |

## `BytecodeVM_OpcodeTable` (the `OpCodes.csv` table)

Below are the entries I decompiled this session that were still `sub_XXXXXXX` in the CSV. All are
**stack-popping actions**: the VM stack holds ints pushed by earlier `PushByte`/`PushVarint`
opcodes, and each handler pops its own argument count (visible in the decompilation as repeated
`*stackPtr - 1`). Confidence noted — please correct where you know better.

| # | Old name | Decompiled behavior | Proposed name | Confidence |
|---|---|---|---|---|
| 1 | sub_80093A4 | pushes global `dword_3001FB4` | `PushGlobalVar0` | medium — need to know what this global is (script param? save slot?) |
| 2 | sub_80093B6 | pushes global `dword_3001FB8` | `PushGlobalVar1` | medium |
| 3 | sub_80093C8 | pushes global `dword_3001FBC` | `PushGlobalVar2` | medium |
| 4 | sub_80093DA | pushes `*g_VMContextPtr` (ctx+0) | `PushScriptArg0` | medium — likely the caller-supplied args to a sub-script |
| 5 | sub_80093EE | pushes `*(ctx+4)` | `PushScriptArg1` | medium |
| 6 | sub_8009402 | pushes `*(ctx+8)` | `PushScriptArg2` | medium |
| 7 | sub_8009416 | pushes `*(ctx+0xC)` | `PushScriptArg3` | medium |
| 23 | sub_8009628 | pops 2, calls `PartyState_TestQuestFlag(state, a, b)`, pushes bool | `StackTestPartyMemberFlag` | high |
| 27 | sub_8009710 | pops 1, calls `BytecodeVM_SetFlag(&g_PartyState, val)` | `StackSetPartyFlag` (sibling of quest-flag Set/Clear at 24/25?) | high |
| 28 | sub_8009728 | pops 1, calls `BytecodeVM_ClearFlag(&g_PartyState, val)` | `StackClearPartyFlag` | high |
| 31 | sub_800977A | pops 3, builds a struct via `sub_800DF1C`, blocks on `GameLoop_ExecuteAndWait` | `PanCameraToPosition` (camera pan; x/y = camera top-left in world px, third = speed; tested in-game, was wrongly guessed as a choice prompt) | **medium — speed unit unconfirmed** |
| 43 | sub_8009B94 | pops 1, allocs a 12-byte async "command" object (vtable `0x802545C`), blocks via `GameLoop_ExecuteAndWait` | `BytecodeVM_ShowCutsceneCommand_A` | **low, needs vtable inspection or in-game correlation** |
| 44 | sub_8009BBC | same shape as #43, different vtable `0x8024778` | `BytecodeVM_ShowCutsceneCommand_B` | **low** |
| 51 | sub_8009CC4 | pops 3 (world/area/variant?), calls `MapScript_CreateCharacter`, adds to entity list | `SpawnCharacterEntity` | high |
| 53 | ~~sub_8009D4A~~ `BytecodeVM_WalkToPosition` | pops 3, resolves an entity by char index, builds a move-command via `sub_8026620`, enqueues it | **RESOLVED 2026-09 (user):** walks toward the target position (ground pathing) — distinct from opcode 54, `BytecodeVM_FlyToPosition` (renamed from `MoveEntityToPosition`, direct/airborne move) | confirmed |
| 58 | sub_800A044 | pops 2, entity-command with vtable `unk_8024878`, 1 payload word | sibling of SetEntityFacing/Animation — `SetEntityAnimationSpeed`? | **low** |
| 60 | sub_800A09A | pops 1, entity-command vtable `unk_8025258`, no payload | `EntityStopAnimation`? | **low** |
| 62 | sub_800A44E | pops 3, entity-command vtable `unk_8024DE4`, 2 payload words | `SetEntityVelocity`? | **low** |
| 67 | sub_800A622 | no pops, allocs cmd referencing `dword_8025B98[60]` (fixed table slot), blocks | looks like opening one specific fixed menu/screen | **low — need in-game context** |
| 68 | sub_800A638 | pops 1, cmd vtable `unk_8023D2C`, blocks | `ShowMessageBox`(param) | **low** |
| 78 | sub_8009688 | pops 2, calls `sub_80062C0(g_MapRenderer, a, b)` | map-renderer parameter setter (palette/scroll speed?) | **low** |
| 79 | sub_800A9B6 | pops 1 (char index), sets `characters[idx].flags |= 1` | `SetCharacterFlag_Bit0` (party roster: "recruited/present"?) | medium — bit-family with #80/#81/#82/#83/#91 below |
| 80 | sub_800AA10 | `flags |= 2` | `SetCharacterFlag_Bit1` | medium |
| 81 | sub_800AA34 | `flags &= ~2` | `ClearCharacterFlag_Bit1` (pairs with #80) | medium |
| 82 | sub_800AA58 | `flags |= 0x20` | `SetCharacterFlag_Bit5` | medium |
| 83 | sub_800AA7C | pops 2 (char, bit), `flags |= (4 << bit)` | `SetCharacterFlag_Dynamic` (generic bit-set, bits 2+) | medium |
| 88 | sub_800AC00 (`FindPlayerEntity`) | no pops, `EntityList_FindByTypeAndIndex(list, kind=16, null)`; the result is discarded (nothing pushed) | `FindPlayerEntity` -- no visible effect (tested); boss scripts call it before `AddEXP`, which takes only the amount | **medium** |
| 89 | sub_8009D94 (`WalkByOffset`) | pops 4 (char, other char, offX, offY); command via `sub_801094C` walks the entity to its own position + (offX, offY) | `WalkByOffset(charIdx, otherChar, offX, offY)` -- tested in-game: walks that many pixels with the walk animation; the second character is ignored; a zero offset just returns the entity to idle facing right | confirmed |
| 90 | sub_800AB44 (`DrawBossHealthBarRange`) | pops 3, builds the health-bar effect via `sub_80104C0` (`BossHealthBar_Create`), adds to entity list | `DrawBossHealthBarRange(charIdx, minPercent, maxPercent)` -- entity must be on screen; opcode 85 (`DrawBossHealthBar`, was misnamed `FadeIn`) is the same bar with (0, 100) | **medium** |
| 91 | sub_800A9DA | pops 1, `flags &= ~1` (clears bit0) | `ClearCharacterFlag_Bit0` (inverse of #79) | medium |
| 93 | sub_800A49A | pops 1, entity-cmd vtable `0x8024BF0`, both payload words zeroed | `StopEntityMovement` (0-arg sibling of #94) | medium |
| 94 | sub_800A526 | pops 3, same vtable `0x8024BF0`, 2 payload words | `SetEntityTargetVelocity(x,y)` | medium |
| 95 | sub_800A572 (`PlayDamageEffect`) | pops 1, entity-cmd vtable `0x8024DD0`; execute slot installs a 12-frame palette-remap flash on the entity | `PlayDamageEffect` (the "damage taken" flash, not the hit animation) | **medium** |
| 96 | sub_800A158 (`NPCFireKiBlastAtEntity`) | pops 3, entity-cmd vtable `0x8025514`, one payload = `speed * 1966`; update slot spawns a projectile aimed at the target's hit-rect centre | `NPCFireKiBlastAtEntity(charIdx, targetCharIdx, speed)` -- non-blocking, both characters must exist on the map | **medium** |
| 99 | sub_800A210 (`NPCFireKiBlast`) | pops 4, complex 32-byte cmd struct, same `*1966` scaling, nested fixed constants 32/64 | `NPCFireKiBlast` (tested in-game: character fires a ki blast) | **medium — actor, x/y destination, speed (tested in-game)** |
| 100 | sub_800A4DA (`SpawnDustCloud`) | pops 3, entity-cmd vtable `0x8025230` | `SpawnDustCloud` (tested: dust-cloud explosion at x/y) | **medium** |
| 101 | sub_800AC5C | pops 2 (each -1), spawns via `sub_8012A68` + lookup table `dword_83B5CA8` | tile-indexed entity spawn (decoration?) | **low** |
| 102 | sub_800ACAC | pops 2 (each -3), same factory, different table `unk_83B5CE8` | sibling of #101, different entity class | **low** |
| 103 | sub_800ACEA | pops 4 raw params, spawns via `sub_8012B38` | generic 4-param map entity spawn | **low** |
| 104 | sub_800AAC6 | pops 1 (char), despawns entity, records last position/facing into `g_MapState`, sets `g_PlayerWasDespawned` if it was the player | `DespawnCharacterAndRecordPosition` | high |

### Session 2026-09-14: remaining opcodes decompiled, renamed where confident

All previously-unresolved `sub_XXXXXX` entries (and the 7 index-1..7 `Push*` stubs) were
decompiled this session. Renamed via IDA (`OpCodes.csv` and the IDB itself now match):

| # | New name | Behavior |
|---|---|---|
| 1 | `BytecodeVM_PushGlobalVar0` | pushes `dword_3001FB4` |
| 2 | `BytecodeVM_PushGlobalVar1` | pushes `dword_3001FB8` |
| 3 | `BytecodeVM_PushGlobalVar2` | pushes `dword_3001FBC` |
| 4 | `BytecodeVM_PushScriptArg0` | pushes `*(g_VMContextPtr+0)` |
| 5 | `BytecodeVM_PushScriptArg1` | pushes `*(g_VMContextPtr+4)` |
| 6 | `BytecodeVM_PushScriptArg2` | pushes `*(g_VMContextPtr+8)` |
| 7 | `BytecodeVM_PushScriptArg3` | pushes `*(g_VMContextPtr+12)` |
| 113 | `BytecodeVM_SetSaveExistsFlag_Bit2` | `g_PartyState.saveExists \|= 4` |
| 114 | `BytecodeVM_SetSaveExistsFlag_Bit3` | `g_PartyState.saveExists \|= 8` |
| 119 | `BytecodeVM_GetActiveEntityContext` | sets+returns `g_ActiveEntityContext = 0x30020F4` (fixed address, not heap) |
| 129 | `BytecodeVM_PushSaveStatePtr` | pushes `g_SaveState` |

`BytecodeVM_StackTestPartyMemberFlag`(23), `StackSetPartyFlag`(27), `StackClearPartyFlag`(28),
`SpawnCharacterEntity`(51), the `SetCharacterFlag_*`/`ClearCharacterFlag_*` family (79-83, 91), and
`DespawnCharacterAndRecordPosition`(104) were found **already renamed** in the IDB from an earlier
pass — this file's opcode table (`OpCodes.csv`) was just stale; it's now synced.

Everything else decompiled this session is a real entity-command constructor (allocates a small
struct, sets a vtable pointer, pops N args into fixed payload words, enqueues on
`g_CommandQueue` via `Entity_EnqueueCommand`, or spawns via `EntityList_Add`) where **the vtable's
own update function was not traced** — so the exact runtime effect (as opposed to the argument
shape) is a hypothesis, not a fact. Left as `sub_XXXXXX` deliberately; do not treat the guessed
names in the wiki pages under `opcodes/unknown/` as confirmed. (Opcodes 31, 85, 88, 90, 95, 96, 99, 100, 112, 117, 118 and 121 were resolved 2026-09 and are no longer in this list -- 137 is `NPCFireKamehameha(charIdx, frames)` (the beam fires 8 frames in, in the direction the entity faces; the attack is fixed and the number is only a duration); 136 is `GlideToPosition(charIdx, x, y, speed)` (moves an entity with no walk animation; the only use is Cell knocking Hercule back in Z14 A2); 124 is `DrawSprite(spriteId, x, y)` (draws a plain character sprite; only shipped use is Zone 8 Area 50's three-trigger puzzle); 135 is `FlashScreenColor(charIdx, r, g, b, frames)` (five arguments; the old table said four); note 85 was misnamed `FadeIn` and is `DrawBossHealthBar` (90 is `DrawBossHealthBarRange`, 88 is `FindPlayerEntity`, 121 is `SetEntityAnimationNoWait`, 117/118 are `RemoveRectFromMask2`/`AddRectToMask2`): `PanCameraToPosition`, `PlayDamageEffect`, `NPCFireKiBlastAtEntity`, `NPCFireKiBlast`, `SpawnDustCloud`, `PlaySFX` -- 112 plays sound effect `sfxId` through the entity's command queue, and `charIdx` must exist on the map.) Full list, with vtable address and
payload shape (see the individual wiki pages for byte-level detail):

`sub_8009B94`(43, vtable `0x802545C`), `sub_8009BBC`(44, vtable
`0x8024778`), `sub_800A044`(58, vtable `unk_8024878`),
`sub_800A09A`(60, vtable `unk_8025258`), `sub_800A44E`(62, vtable `unk_8024DE4`), `sub_800A622`(67,
opens fixed menu/table slot `dword_8025B98[60]`), `sub_800A638`(68, vtable `unk_8023D2C`),
`sub_8009688`(78, calls `sub_80062C0(g_MapRenderer, a, b)`), `sub_800A49A`/`sub_800A526`(93/94, shared vtable
`0x8024BF0`, 0-arg vs 2-arg pair), `sub_800AC5C`/`sub_800ACAC`(101/102, tile-indexed spawns via `sub_8012A68`, tables
`dword_83B5CA8`/`unk_83B5CE8`, args offset by -1/-3 respectively), `sub_800ACEA`(103, generic
4-param spawn via `sub_8012B38`), `sub_800AD3E`(120, spawn via `sub_8016AB4`, args +15), `sub_800AFD4`(130, spawn via
`sub_801C4B0`), `sub_800A276`(134, vtable `0x8025550`), (Opcode 125, `sub_800AF44`, is `PlayCredits()`: it plays the credits and then clears VRAM, so the map must be reloaded. Opcode 89 is `WalkByOffset`. Opcode 140, `sub_8009F3C`, is `LockEntityPose(charIdx, value)`: holds an entity in a non-resetting pose, used when Cell absorbs an android in Z12 A4; the byte it sets at entity+8 is not traced. Opcode 142, `sub_800A1B4`, was resolved 2026-09 as `NPCFireBigBangAtEntity`: the same attack as 96 with the big projectile, vtable `0x8024BDC`, non-blocking.)

If you're chasing one of these down in-game, the fastest path is to trigger the specific cutscene/
dialog known to call it (cross-reference the opcode index back through `sequence[]`/script data)
and watch what visibly changes, then match that to the vtable's update-function behavior.

## Dialog / DialogEntryScripts region (0x083B5FB4+)

Documented separately in `DBZKit/Dialog_Format.md` — covers the 6 dialog entry modes, the
`sequence[]`/`Index`-chaining mechanism, and a fully-traced real example (`PickUpItem` used as a
mode-0 "run an event" dialog step).

### Session 2026-09-19: quest opcode renames, StackRand check, NPC dialog trace, unknown-opcode arity audit

**Renamed** (IDA + `OpcodeTable.cs` + `BYTECODE_VM.cs` + `opcode_data.js` + `OpcodeDocs.xml`, all kept
in sync): opcode 19/20 `StackTestQuestFlag`/`Not` → `StackTestStoryFlag`/`Not`, opcode 27/28
`StackSetPartyFlag`/`StackClearPartyFlag` → `SetStoryFlag`/`ClearStoryFlag`, opcode 128
`SetQuestFlag` → `SetPartyMemberFlag` (arity corrected 1→2, confirmed via disasm it pops two
values), opcode 31 `sub_800977A` → `ShowChoicePrompt` (later corrected to `PanCameraToPosition` after an in-game test showed it is a camera pan). Full
rationale in `DBZKit/Quest-System.md` — short version: two completely unrelated flag storages in
this ROM used to share "Quest"/"PartyFlag" names, which is exactly backwards (the story-flag
system, 19/20/27/28, is what the Quest Log reads; the party-member-flag system, 23/128, isn't).
Also renamed the underlying IDA functions: `BytecodeVM_StackSetPartyFlag`→`BytecodeVM_SetStoryFlag`,
`BytecodeVM_StackClearPartyFlag`→`BytecodeVM_ClearStoryFlag`, `BytecodeVM_StackTestFlag`→
`_Raw`/`BytecodeVM_SetFlag`→`_Raw`/`BytecodeVM_ClearFlag`→`_Raw` (the low-level bit-array ops,
now distinguished from the opcode wrappers), `PartyState_SetQuestFlag`/`TestQuestFlag`→
`PartyState_SetMemberFlag`/`TestMemberFlag`, `BytecodeVM_SetQuestFlag`→`BytecodeVM_SetPartyMemberFlag`,
`dword_86F6C7C`→`g_PartyMemberFlagCategories`. Also newly learned: `Map_LoadInternal` calls
`PartyState_SetMemberFlag(&g_PartyState, zone, area)` on every single map load — the party-member-flag
system is (at least partly) a "have you visited this zone/area" tracker, not anything about
individual party members despite the name. `StackTestPartyMemberFlag`'s argument order is now
CONFIRMED (disasm of `0x8009628`): the first-pushed value becomes `PartyState_TestMemberFlag`'s
`a2`, the second/top-of-stack becomes `a3` — there's no separate category selector, the function
brute-force-scans all 68 `g_PartyMemberFlagCategories` entries for a descriptor matching the exact
`(a2, a3)` pair.

**`StackRand` (17) arity check** — user reported seeing scripts push 3 values before a `StackRand`
call. Disasm of `BytecodeVM_StackRand`/`BytecodeVM_StackRandChance` (`0x8009568`/`0x800957A`)
confirms both pop exactly ONE value (peek-and-replace, no net stack change) — `GetRandomRange`/
`RandomChance` (`0x802141C`/`0x802142E`) each take a single `Max`/`Threshold` argument, nothing
more. Conclusion: the arity is correct as documented; a script pushing 3 values before `StackRand`
is pushing extra operands for OTHER opcodes later in the same sequence, not for `StackRand` itself
— totally normal stack-machine behavior, not a bug in the table.

**NPC dialog / how "talk to an NPC to advance a quest" actually works** — traced the full
collision-interact chain for every entity constructor found (`CharacterSprite_OnCollision` /
`CombatEntity_OnCollision` → `Entity_HandleCollision_Shared` → `_Ext1` → `_Ext2`, used by the
player, party-member sprites, generic map NPCs via `MapScript_CreateCharacter`, and the 496-byte
combat/Scouter entity). **Collision type 3 ("interact") does nothing in every one of them** —
confirmed by reading all the way to the terminal link (`Entity_HandleCollision_Shared_Ext2`,
`0x800786A`), which explicitly falls through as a no-op for type 3. So walking up to an NPC and
bumping into it is NEVER how dialogue starts, for any entity type in this ROM.
`MapEntity_HandleCollisionInteract` (`0x800B632`) — the ONE function that reads an entity's own
`dialogSeq`/`seqIndex` fields (offsets +384/+364) and would start a conversation — is provably
unreachable from any checked constructor's vtable; almost certainly dead code left over from an
earlier design. **Real dialogue is entirely trigger-zone driven**: `MapTrigger_OnEnter`
(`0x800D7E0`) builds a wrapper object whose `dialogSeq` field is `*(source+0x10)` (source =
the trigger's own `dataPtr`), and `MapTriggerDialog_Update` (`0x800D7A0`) is what actually calls
`InteractionHandler_Init` with it once the trigger's own `Condition_Evaluate` passes. **This means
a walking NPC's visual sprite (from `MapScript_CreateCharacter`) and its dialogue (a nearby/
overlapping Dialog-type map trigger) are two completely independent placements in the ROM** —
there is no field linking one to the other; whoever authored a map just had to place both near
each other. The NPC record's own `actionData` field (`MapScriptSpawnMultiConditional+0x10`,
confirmed via `type_inspect`) gets stored into the spawned entity's interaction-state buffer but,
per the dead-code finding above, is never read back by anything reachable — for ordinary walking
NPCs it looks like inert data.

Once a Dialog trigger's conversation is running, `DBZKit/Dialog_Format.md`'s mode-0
`DIALOG_SCRIPT` entries are the actual "run game logic mid-conversation" hook — confirmed real
example there is a `PickUpItem` call chained between two lines of dialogue via the entry's own
`Index` field. This is genuinely how quest-flag-setting NPCs work: a mode-0 entry somewhere in
their conversation's `sequence[]` runs `PushByte(flagId); Step(27=SetStoryFlag); END`. Implemented
this properly in code: `DrGero.Quests.DialogScanner` (`LooksLikeDialogSequence` /
`FindFlagWritesInDialog`) walks a `sequence[]` array, finds mode-0 entries, and decodes each one's
embedded script for opcode 27/28 flag writes — wired into Dragon Radar's trigger-to-quest link
(`FindQuestsSetByScript` now tries both raw-bytecode and dialog-sequence interpretation, since a
trigger's payload slot can be either depending on which of the four trigger variants it is).
**Corrected a real bug found while wiring this up**: Dragon Radar was reading a trigger's payload
from `dataPtr+4`, which disasm of `MapTrigger_OnEnter` shows is actually a NESTED constructible
sub-object (passed through `call_ctor`), not the payload at all — the real payload/dialogSeq field
is at `dataPtr+0x10`. Fixed.

**Unknown-opcode audit** — re-decompiled every remaining `op_unkNN` entry (31, 43/44, 58, 60, 62,
67, 68, 78, 88, 89, 90, 93-96, 99-103, 112, 117/118, 120/121, 124/125, 130, 134-137, 140, 142) from
scratch against real disasm. Every single arity already in `OpcodeTable.cs` checked out exactly —
no arity bugs found in this batch (unlike opcode 128 above). Almost all of them follow one of two
shared shapes: (a) `Entity_GetByCharIndex` + `ArenaAlloc` a small struct with a FIXED vtable
pointer + `Entity_EnqueueCommand` onto `g_CommandQueue` (the "entity visual command" family --
walking/animation/velocity-type commands), or (b) a spawn via a shared factory function
(`sub_8012A68`/`sub_8012B38`/etc) + `EntityList_Add`. In every case the exact runtime EFFECT
depends on a vtable's own update function, which wasn't traced (that's a per-vtable investigation,
not a per-opcode one) -- so none of these were renamed beyond the confidence already recorded
against them individually above; forcing a name without that confirmation would just be a
guess wearing a real-looking label. Notable structural findings worth keeping: the same "×1966"
frame/angle scaling constant appears in THREE different opcodes (96, 99, 142) -- a shared
move/rotate-toward-entity family; opcodes 93/94 and 117/118 and 101/102 are each a 0-arg/N-arg or
set/clear PAIR sharing one vtable, matching the SetStoryFlag/ClearStoryFlag pattern seen elsewhere
in this table.

## Unresolved opcodes that no script calls (2026-09-20)

`OpcodeUsage` (Legacy's "Find opcode usage") over every dialog, trigger, NPC and enemy script in the shipped ROM
finds no call to these unresolved opcodes: **101, 102, 103, 118, 120, 130**. They still assemble and
disassemble, but Zenkai leaves them out of autocomplete (`OpcodeTable.UnusedInGame`).

In-game tests: 101 draws a 3x3-pixel sprite at screen pixel (x, y) (palette indices 0x65 and 2 -- the "rock texture"
seen in a test); 103 (`x1, y1, x2, y2`) only put a teal square on screen. 102 is 101's twin (7x7 sparkle sprite,
offset -3), untested. None of them has a known purpose.

Also never called (but already named): StackGetCharHP/EP/MaxHP/MaxEP (105-108), PlayAudioVolume (111), SpawnMapEntity
(127), SetPartyMemberFlag (128), PushLevelUpStat1-3 (131-133), EnterSleepMode (138), CenterCameraOnChar (32),
WaitFramesSimple (68), and a few Push* helpers -- these stay in autocomplete.

## Wiki

Every Step-table opcode (this file's second table) and the main dispatch table now has a
per-opcode reference page under `Legacy.wiki/Zenkai/opcodes/<category>/` and
`Legacy.wiki/Zenkai/vm/{dispatch-table,opcode-table}.md`, in the style of the pre-existing
`Zenkai/Audio/PlayMusic.md`. Use those for authoring scripts; this file remains the primary
decompilation research log.
