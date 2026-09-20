# DBZLOG2 -- Legacy of Goku II (US, "ALFE") decompilation

Goal: a readable C++ source tree for the game, using **our** names, that we grow a function at a time from the IDA database
(`1085 - Dragon Ball Z - The Legacy of Goku II (U)(TrashMan).gba.i64`). The game's vtable style may be replaced by plain calls later;
until then the code mirrors the binary closely so it can be checked against the ROM.

Nothing here is guaranteed to build yet -- no compiler is wired up. It is written as portable C++17 so it can be built for the host
(with the GBA hardware layer stubbed) or later for the GBA.

## Layout

```
src/DBZLOG2/
  CMakeLists.txt
  include/dbzlog2/
    types.h            fixed-width aliases, GBA address helpers
    rom_map.h          ROM/RAM addresses of every table and global we have identified
    gba_io.h           the few hardware registers the code touches
    vm/                bytecode VM: state, opcode enums, one declaration list per table
    audio/             sound engine: structs, tables, function declarations
  src/
    vm/main_ops/       one file per top-level script opcode   (<hex>_<Name>.cpp)
    vm/actions/        one file per "Step" action opcode      (<dec>_<Name>.cpp)
    audio/             sequencer, mixer, voices
    audio/effects/     one file per pattern effect (Audio_FxNN_*)
  docs/                notes that explain a subsystem (mirrors DBZKit/*.md)
```

## Conventions

* **One opcode / one effect per file**, named after the IDA function. The file header records the ROM address, size and a
  certainty tag.
* Certainty tags (same as the DBZKit notes): **HIGH** = decompile and ROM data agree, **MEDIUM** = decompile only, **LOW** = inferred.
  Anything not HIGH says so at the top of its file.
* Names are the IDA names. When IDA has a better name than this tree, rename in IDA first (it is the source of truth) and regenerate.
* Struct fields keep their byte offset in a trailing comment (`// +0x4C`) and every struct has a `static_assert` on its size.
* Addresses are ROM addresses with the `0x08...` prefix; file offsets are `addr & 0x00FFFFFF`.
* No behaviour is invented: if the decompile is unclear, the code keeps the raw expression and a `// TODO(unknown)` note.

## Status

| Area | State |
|---|---|
| audio | sequencer (parse row, tick, `Music_Init`), voice commit, effects 1/2/4/5/6/7/8/15/17/20/24, SFX queue, mixer refill; effect 27 = note delay (was unknown), 11 and 21 done; still to do: effect 12, the sample-mixing kernel |
| characters / abilities / sprites | headers (`characters.h`, `sprites.h`, `entities.h`, `vtable.h`); stock Transformation ability, `Character_GetSpriteRecord`, action 73 (`SetCharacterTransformation`) from the ROM's Thumb code |
| bytecode VM | all 30 main opcodes (`src/vm/main_ops/`, `execute_script.cpp`, dispatch table) + action 73; the ~145 action opcodes are next |
| system / hardware | `gba_io.h` (full register map), `gba_memory.h` (memory map + every known RAM global), random, input, interrupts, game loop, boot (`Game_Main`), heap init / memset |
| everything else | not started |

See `docs/audio.md` for the sound engine and `../../DBZKit/Audio-Notes.md`, `../../DBZKit/Engine-Notes.md` for the underlying notes.
