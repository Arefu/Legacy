# Roster, sprites and abilities (US ROM, ALFE)

Read from IDA (2026-09-20). Certainty: **HIGH** = decompile and ROM data agree, **MEDIUM** = decompile only,
**LOW** = inferred. File offset = ROM address - 0x08000000.

## 1. The three tables that define a character

| Table | ROM address | File offset | Layout |
|---|---|---|---|
| `CharacterDefaultStats_Table` | 0x081D9548 | 0x1D9548 | 6 x `CharacterEntry` (28 bytes): the party at New Game |
| `Character_DisplayData` | 0x081D967C | 0x1D967C | **22** x `CharacterDisplayData` (20 bytes): every playable *form* |
| `g_AbilityTable` | 0x081D9900 | 0x1D9900 | **14** x `AbilityDef` (16 bytes) |

The display table ends exactly where the EXP table begins (0x1D9834) and the ability table is followed by
unrelated data at 0x1D99E0, so **neither can grow in place** -- both must be relocated. (HIGH)

### `CharacterEntry` (one party slot, also what is saved)
```
+0x00 currentHp u16   +0x02 maxHp u16   +0x04 currentEp u16   +0x06 maxEp u16
+0x08 displayDataIndex u8   -> row in Character_DisplayData (which character/form this slot IS right now)
+0x09 abilityIndex u8       -> which of the 4 abilities is selected
+0x0A str u16  +0x0C pow u16  +0x0E end u16   (growth is scaled: 256 = 1.0; capped at 25600)
+0x10 currentEXP u32   +0x14 flags u32 (bit0 = in party, bit1 = ?, ... see the SetCharacterFlag opcodes)
+0x18 abilityList[4] u8     -> four indices into g_AbilityTable
```

### `CharacterDisplayData` (one row = one character *form*, e.g. base and Super Saiyan are separate rows)
```
+0x00 flags  (bit0 = "is a transformed form", bit1 = ?)   +0x01 spriteId  (g_CharacterSpriteIndex id)
+0x02 portraitIndex (Portrait_Table)   +0x03 unk3 (per-character family id, 8..25; meaning LOW)
+0x04 transformedDataIndex  +0x05 detransformedDataIndex   (row links: SSJ <-> base)
+0x06 minFrames +0x07 maxFrames   +0x08 transformedDataIndex2   (transform timing; MEDIUM)
+0x0A u16 unk  +0x0C u32 unk   +0x10 ppVTable (RAM pointer 0x030021E0..0x030021F0 -- picks the
                                               flight/ground behaviour variant, copy from a similar row)
```

## 2. Who is in the default party (HIGH for the data, names inferred from their abilities)

| slot | displayIdx -> sprite | stats (hp/str/pow/end) | abilityList | looks like |
|---|---|---|---|---|
| 0 | 3 -> 57 | 85 / 3 / 5 / 3 | Masenkoball, Kamehameha, Transformation, KiBlast | Gohan |
| 1 | 6 -> 103 | 95 / 4 / 4 / 5 | SpecialBeamCannon, ScatterShot, Transformation, KiBlast | Piccolo |
| 2 | 8 -> 137 | 105 / 6 / 4 / 6 | BigBang, EnergyPunch, Transformation, KiBlast | Vegeta |
| 3 | 18 -> 135 | 110 / 6 / 3 / 4 | BurningAttack, SwordBlast, Transformation, KiBlast | Trunks |
| 4 | 19 -> 60 | 100 / 5 / 7 / 6 | Kamehameha, SpiritBomb, Transformation, KiBlast | Goku |
| 5 | 21 -> 72 | 50 / 8 / 1 / 6 | Hercules x4 | Mr. Satan |

(stats shown /256.) `Character_ResetCharacter_Impl` copies this whole table over the party on a new game.

## 3. Abilities

`AbilityDef = { readyIcon*, coolingIcon*, isReady fn, onUse fn }` -- the first two are 0x200-byte icon
resources for the HUD (`HUD_UpdateAbilityPanel`), the last two are **compiled Thumb functions**.
`Player_UseAbility` runs `abilityList[abilityIndex]`: if `isReady` returns true it calls `onUse`, else it
plays the "can't" sound. Table contents (index -> ability), all HIGH:

`0 BigBang  1 Burning  2 KiBlast  3 Kamehameha  4 Masenkoball  5 (none)  6 Hercules  7 EnergyPunch
 8 ScatterShot  9 SpecialBeamCannon  10 SpiritBomb  11 Transformation  12 Transformation(B)  13 SwordBlast`

Every function is named in the IDB (`*_IsReady`, `*_OnUse`).

**What that means for editing**
- *Reassigning existing abilities* is pure data: change a slot's `abilityList` bytes (index 0-13). Do it
  in the default table for new games. **Existing saves keep their own copy**, so they need a new game (or a
  script that rewrites the slot).
- *A new combination* (e.g. new icon + existing attack code) is also pure data: add an `AbilityDef` to a
  relocated table (only **2** code words point at the table: file offsets 0x8024 and 0x8E8C).
- *A genuinely new attack* needs new Thumb code (an `onUse` that spawns the projectile). Not data.

## 4. How a slot's identity works at runtime (HIGH)

`BytecodeVM_SetCharacterTransformation(slot, spriteId)` calls `CharacterData_FindBySprite`, which scans
`Character_DisplayData` for the row whose `spriteId` matches and stores that row in
`characters[slot].displayDataIndex`, then refreshes the player sprite. So **a script can already make any
slot become any display row** -- the SSJ mechanic is just this. Sprite, portrait and form links follow the
row; **stats and abilities stay with the slot**.

`Character_GetSpriteId(1..6)` = the sprite of party slot 1..6 and `>= 7` = an NPC sprite, and
`Character_GetPortraitResource(2..7)` = portrait of slot 0..5, `> 7` = `Portrait_Table[id]`. So the
number 6 is baked into the sprite-id and portrait-id spaces as well as the data.

## 5. Why the *party* can't simply grow (HIGH on the constraint, not exhaustively counted)

`PartyState` is 356 bytes (0x164) at IWRAM 0x03000E90, copied to/from each of 3 SRAM save slots, and
ends at 0x03000FF4 where other globals begin. The 6 `CharacterEntry` records sit at +0x0C, followed by
story flags (+0xB4), visited-area bits (+0xE7) and the inventory (+0x12B) -- all addressed by absolute
offset from many functions. Loops hard-code 6 (`Character_GetActiveCount`, `PartyState_ResetTransformedSlots`,
the debug loader, the character-select UI). Widening it means relocating the struct, changing the save
format and touching every one of those. **Not recommended.**

## 6. Recommended way to a much larger roster: keep 6 active slots, widen the *pool*

1. **Add display rows** (new characters/forms). Relocate `Character_DisplayData` to free space and patch:
   - the **18** literal words containing 0x081D967C -- file offsets
     `1954 1B3C 3B30 414C 42F0 854C 886C 8B04 A77C AA04 B050 C804 10F18 112BC 13574 14FF4 26458 26474`;
   - the row limit `CMP R2,#0x16` in `CharacterData_FindBySprite` at file offset **0x42D6** (byte 0x16 -> new count;
     Thumb `CMP #imm8`, so up to 255 rows).
   Each new row needs a `spriteId` that exists in `g_CharacterSpriteIndex`, a `portraitIndex` in
   `Portrait_Table` (0x083EC9D4), and a `ppVTable` copied from a row of the same kind.
2. **Swap who occupies a slot** with the existing opcode (section 4). No engine change.
3. **Abilities per character** are the one gap: they belong to the slot, so a swapped-in character inherits
   the slot's four abilities. Options, easiest first:
   a. accept it and choose slot abilities that suit the characters that will use the slot;
   b. rewrite the slot's `abilityList` when swapping (needs a small new opcode = new code);
   c. move the ability list into the display row (`unk_C` at +0x0C is a spare u32, ideal for 4 bytes) and
      patch `Player_UseAbility` and `HUD_UpdateAbilityPanel` to read it from the row -- about two small
      Thumb edits, but they need testing.
4. **Default party**: edit `CharacterDefaultStats_Table` (0x1D9548). Its single code reference is at 0x3EA0.

## 7. Not verified / next steps
- `unk3`, `unk_A`, `unk_C` and flag bit1 of a display row; what `Portrait_Table` ends at.
- The character-select UI layout (`UI_CharacterSelect_*`) -- assumed 6.
- Nothing here was run in an emulator. The cheapest safe first experiment: edit the ability bytes of the
  default table (section 3) and start a new game.

## 8. Tooling
DBZKit > Tools > Engine tools implements sections 3, 4 and 6 (steps 1 and 4): edit the default party and
abilities, add display rows (relocation + the 18 literals + the row limit are handled), add abilities, all on a
private ROM copy. Verified headlessly against the real ROM: table round trips, relocation, and reads after
relocation. The swap-in-game mechanic (section 4) needs no tool -- it is the existing script opcode.

## Ability animations vs sprite records (added 2026-09-20)
- Each ability's OnUse spends EP then runs animation N; the animation's init does `entity+0x4C = spriteRecord + K`. Stock map: BigBang/Burning/KiBlast/Masenko/ScatterShot K=156, Kamehameha/SpecialBeam/SwordBlast K=364, SpiritBomb K=380 (HIGH certainty; see `AbilityRequirements.Stock`).
- `AbilityRequirements.Check` warns when a form lacks 4 valid frame pointers at K. MEDIUM/LOW certainty: vanilla Slot 0 (Kamehameha) and Slot 3 (Sword Blast) also trip it, so either vanilla reads garbage there or the check is too strict. Test in an emulator before trusting it.
- Default-party edits only apply to a NEW GAME and only reach disk via Save ROM As.
- Animation picker (Engine tools > Abilities, "Animation" column): stock OnUse is a 24-byte routine with `movs r1,#ANIM`; picking another animation writes a 24-byte clone to free space (tail-call via `ldr r3,[pc]; bx r3`, because free space is beyond BL's +-4 MB reach) and repoints OnUse. HIGH certainty on the byte patterns (verified on the ROM), untested in an emulator. Only animations 34, 44-51 are offered (their init and block are known); EP cost is left alone (IsReady has its own copy of the cost).
- PNG -> frames (the Sprite editor tab): PNG -> blob {u32 1, u32 size, literal-only JCALG1 stream} + 12-byte descriptor {xoff,yoff,w,h,attr0|attr1<<16,ptr}. Two things the engine REQUIRES (found by crash, 2026-09-20; HIGH, decoder run in an emulator + IDA): (1) the game's JCALG1 decoder (IWRAM 0x3000000) never initialises its literal width, so every stream must start with the 9-bit literal-size op 0 0 1 0000 0 1 or it decodes junk and smashes the stack; (2) a record slot points at an ARRAY of descriptors indexed by frame (sub_800775A: *(rec+0x4C+4*dir) + 12*frameIdx; walk cycles use 0-1, the ki-blast pose 0-2), so writing one descriptor makes frameIdx 1+ read junk. WriteFrameArray writes as many descriptors as the stock array had. 'Write into ROM copy' copies the sprite record to free space and fills the chosen group.
