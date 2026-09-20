# Roadmap: IDA <-> source <-> assets <-> a buildable ROM

The end state (from the project owner):

1. **IDA and this source tree stay in step.** Every function/struct/table in the source has the same name in IDA, and vice versa.
   When one changes, the other is updated in the same sitting.
2. **Every data block is exported to `.bin`** with a manifest, so the ROM's content is a set of named files, not an opaque image.
3. **A devkit builds a playable ROM** from source + assets. First milestone: rebuild the original ROM byte-for-byte from the
   compiled functions and the asset blocks; after that, edited assets and new code.
4. Virtual calls (vtable-style dispatch) may be rewritten as plain calls, or kept and adjusted for the builder. That is expected to be
   the easy part: see "Virtual calls" below.

## Steps

| # | Step | State |
|---|---|---|
| 1 | Source scaffold, one file per opcode/effect, certainty tags | started (`audio/`; VM pending) |
| 2 | Asset export to `assets/` + `manifest.json` | audio done (`tools/extract_audio_assets.py`, 960 blocks, byte-exact, no overlaps); graphics, maps, scripts, dialogue, text still to do |
| 3 | IDA parity: every exported block is a typed, named item in IDA | audio tables done; per-pattern blobs and the rest pending (see `ida_todo.md`) |
| 4 | Bytecode VM (30 main opcodes + ~145 actions), one file each | pending: IDA was unreachable when this was written |
| 5 | Builder: place blocks by name, relocate the pointers listed in the manifest, emit a `.gba`; verify against the original | pending |
| 6 | Code generation target: compile the C++ for ARM/Thumb and link it into the ROM | pending; needs a toolchain choice |

## Assets

`tools/extract_audio_assets.py` writes `assets/audio/**.bin` and `assets/audio/manifest.json`. Each manifest block has the ROM address,
size, kind, the IDA symbol it corresponds to, and (for tables) which 4-byte fields are ROM pointers and which block file they target.
That pointer list is what lets the builder move a block and fix up whatever references it. Blocks are byte-exact copies (verified),
and none overlap.

The `.bin` files are ROM content, so they are git-ignored by default (`.gitignore` in this folder); the manifest and the extractor are
tracked. Remove the ignore line if you want the blocks committed.

## Virtual calls

The engine dispatches through tables whose entries are **relative**: `function = table_address + word` (e.g. entity vtables, the
`Audio_EffectTable` is the exception and holds absolute Thumb addresses). In source these become `struct VTable { s32 slot[N]; }` plus
an accessor, so today's code still matches the binary. For a buildable ROM there are two options, both mechanical:
keep the tables and have the builder compute the relative words from symbol addresses, or replace the indirection with direct
calls where the target is fixed. Decide per table when it is reached.
