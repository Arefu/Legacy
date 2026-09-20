# Sound engine

The full description (formats, effects, ROM addresses, IDA state) is in [`DBZKit/Audio-Notes.md`](../../../DBZKit/Audio-Notes.md);
that file is kept next to the DBZKit code that reads the same data. This folder holds the recovered C++:

| Area | Files | Certainty |
|---|---|---|
| structs, flag enums | `include/dbzlog2/audio/audio.h`, `audio_state.h` | HIGH (voice/channel offsets from the decompile) |
| note trigger | `src/audio/audio_note_on.cpp` | HIGH |
| per-row effect dispatch | `src/audio/sequencer_run_channel_effect.cpp`, `effect_table.cpp` | HIGH |
| voice commit | `src/audio/voice_commit_pending_changes.cpp` | HIGH |
| effects 1, 2, 4, 5, 6, 7, 8, 15, 17, 20, 24 | `src/audio/effects/fxNN_*.cpp` | see each file (2 is LOW) |
| sequencer row parser, tick loop, `Music_Init`, mixer kernels, SFX voices | not yet written | decompiled in IDA, to be transcribed |

DBZKit's `DrGero/AudioRender.cs` is a working C# re-implementation of the same engine (used for playback in the Sound tab); it is a good
cross-check while these files are being filled in.
