# IDA edits still to make (IDA was unreachable when these were found)

# IDA diagnosis (2026-09-20, second attempt): NOT FIXED
- `import idapro` blocks inside `libida.init_library(0, None)` (idapro/__init__.py line 74), with 0% CPU, before any database is
  touched. Reproduced with a fresh APPDATA (so it is not user config/plugins) and with a copy of the .i64 in a scratch folder
  (so it is not the database). No stale ida/idalib processes exist (only the MCP supervisor and its python children). Loose
  .id0/.id1/.nam/.til files were left alone; a backup copy of the .i64 was made in the session scratchpad (13,829,207 bytes, same size).
- The remaining suspect is licensing: `C:\Program Files\IDA Professional 9.4\idapro.hexlic` is not an ordinary Hex-Rays licence
  (placeholder-looking ids/owner). I did not investigate or modify it. Fix the IDA install/licence yourself, then re-run.
- Consequently no opcode C++ files were written this pass.


The idalib worker timed out on every open after 2026-09-20 13:45 (even `import idapro` hung outside the MCP server), so these
IDA-side edits are queued. Apply them when IDA opens again, then tick them off.

## Corrections found while writing the source
- [ ] `g_LastNoteChannel` (0x030026CB) is **MusicPlayer.ticks_per_row** (MusicChannel+3), and `Audio_Fx01_NoteOnSetLastChannel`
      is **effect 1 = SET SPEED** (params 3..7). Rename the global to `g_MusicPlayer_ticksPerRow` (or fold it into the struct) and the
      function to `Audio_Fx01_SetSpeed`. DBZKit's renderer already treats it as set-speed.
- [ ] `g_AudioRemainingNotes` (0x030026C9), `g_AudioNoteCount` (0x030026CA), `g_AudioPrevChannel` (0x030026D8) are MusicPlayer+1, +2,
      +0x10 (effect 2 = unknown, 27 uses). Rename to `g_MusicPlayer_unknown01/02/10` until understood.
- [ ] `g_AudioTempoDivided` (0x030026CE) is MusicPlayer.tick_reload (+6); `g_MusicState` (0x030026CD) is MusicPlayer.status (+5).
- [ ] Retype `g_MusicChannel` / `MusicChannel` as `MusicPlayer` (1764 bytes): 0x18-byte header, `channels[15]` of 0x60 at +0x18.
      Find the offsets of the cursor fields (`pattern_length`, `row_counter`, `pattern_index`, `read_ptr`, `track`, `active`, `tick_low`)
      with `read_struct` and copy them into `include/dbzlog2/audio/audio_state.h` (`SequencerCursor`).
- [ ] Define the 0x60-byte `MusicChannelVoice` type (fields are listed in `audio.h`) and apply it to the channels.
- [ ] `Audio_Fx11_*`, `Audio_Fx12_*`, `Audio_Fx21_*`, `Audio_Fx27_Unknown` (0x080202E4, 0x080203A8, 0x08020596, 0x0802054C): decompile and
      write `src/audio/effects/fxNN_*.cpp`.

## Data blocks not yet defined in IDA
- [ ] The 960 blocks in `assets/audio/manifest.json`: SFX PCM (64), instrument PCM (101), per-song patterns (~800). Tables and per-song
      order lists / pattern tables are already typed.
- [ ] Tracks 26 and 29-43: `Music_TrackNN_PatternTable` only got its first element (IDA refused to overlap existing items); find what
      is defined inside them and undefine it first.

## Names to confirm
- [ ] `Audio_Fx05` / `Audio_Fx06` / `Audio_Fx08` / `Audio_Fx17` were renamed to slide down / slide up / vibrato / retrigger on
      2026-09-20; check no other function still carries the old "PitchBend / Vibrato / Tremolo / Arpeggio" names.

## Queued 2026-09-20 (sprites / characters / abilities) -- apply when IDA opens
- [ ] `Character_GetSpriteId` (0x08009324) returns the sprite RECORD pointer (what is stored at entity+0x48), not an id: rename to `Character_GetSpriteRecord`;
      set its prototype to `SpriteRecord *Character_GetSpriteRecord(int id)` (0 = player entity, 1..6 = party slot, >= 7 = g_CharacterSpriteIndex[id]).
- [ ] `sub_802645C` -> `CharacterEntry_GetSpriteRecord(const CharacterEntry *)`; `sub_80042BC` -> `CharacterData_FindBySprite` (check it already has that
      name); `sub_80086A2` -> `PlayerEntity_RefreshSprite`.
- [ ] Declare `FrameDescriptor` (12 bytes, see include/dbzlog2/sprites.h) and `SpriteRecord` (header 0x4C + groups of 4 x FrameDescriptor*; NOT fixed size)
      and retype `g_CharacterSpriteIndex` as `SpriteRecord *[157]` (ids 0..156; the table is followed by unrelated data at 0x083B50E8).
- [ ] Name `Transformation_IsReady` 0x080134F2, `Transformation_OnUse` 0x080134C0 and `Transformation_B_OnUse` 0x0801353C (abilities 11 / 12), and comment: the OnUse
      plays animation 0x25 (0x24 when the row is flagged transformed). Find and name the animation handlers for 0x24 / 0x25 (vtable slot 10 of the player entity).
- [ ] Type `CharacterEntry` (28), `CharacterDisplayData` (20, flags bit0 transformed / bit1 cannot transform), `AbilityDef` (16) from
      include/dbzlog2/characters.h and apply them to `g_PartyState.characters`, `Character_DisplayData` and `g_AbilityTable`.
- [ ] `g_RomPointerTable` (0x083B5C78): a table of pointers to globals; +0x20 = &g_PartyState. Identify entries 2 and 3 (used by Transformation_IsReady).
- [ ] Generated ability code lives in free space at the end of edited ROMs (DBZKit `TransformationAbility`), not in the original ROM: nothing to do in IDA for it.

## Done 2026-09-20 (IDA opened again; this session)
- [x] Renamed: Character_GetSpriteRecord, CharacterEntry_GetSpriteRecord, PlayerEntity_RefreshSprite, Transformation_IsReady / _OnUse / TransformationB_OnUse,
      Audio_Fx01_SetSpeed, Audio_Fx02_Unknown; g_MusicPlayer_ticksPerRow / _unknown01 / _unknown02 / _unknown10.
- [x] Renamed: OAM_Init -> Arena_Init (it initialises the EWRAM heap), Arena_UnlinkFreeBlock, GameLoop_VBlankHandler, GetRandom* -> Random_Next / Random_Range /
      Random_Chance, g_RandomState0/1, g_InputSystem, g_AudioMixer, g_BiosIrqHandler, g_LastInputFrame. Declared FrameDescriptor and AbilityDefEx types.
- [ ] Still queued: apply CharacterEntry / CharacterDisplayData / AbilityDef types to their tables (needs the ability table typed as AbilityDef[14]),
      rename g_ButtonState+4 (0x030022B4) to g_ArenaFreeListHead, type the mixer object (g_AudioMixer) and the MusicPlayer header.
