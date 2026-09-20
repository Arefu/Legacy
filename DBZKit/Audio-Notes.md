# Sound engine (US ROM, ALFE) -- BGM and SFX

Read from IDA (2026-09-20). **HIGH** = decompile and ROM data agree, **MEDIUM** = decompile only.
It is the game's own software mixer, not Nintendo's m4a: 8-bit signed samples, ~16 kHz, a tracker-style sequencer.

## Output path (HIGH)
`AudioSystem_Init` (0x8020AF4): Timer0 reload 0xFBE6 (~15981 Hz), DMA1/DMA2 stream two 8-bit FIFO channels from 1024-byte rings at
0x2000000 / 0x2000600. `Audio_MixerUpdate` (0x8020BD8) refills them: `Audio_SequencerTick` steps the song, `AudioVoice_MixSamples`
mixes 15 music channels, `SfxVoices_Mix` mixes 16 SFX voices.

## Sound effects (HIGH)
`g_SFXDefTable` 0x80E15B0 (file 0xE15B0), 64 x 12 bytes: `{u32 data, u32 loop, u16 length, u16 rate}`. Data is signed 8-bit PCM, `length`
samples, rate 16000 (56 of them) or 8000 (8). `SFX_Queue(pool, &table[n], volume, pan)` plays one; script opcodes PlayAudio/StopAudio use
index*12 + 0x80E15B0.

## Instrument bank (HIGH)
`g_InstrumentBank` 0x814A8A4, 101 x 12 bytes: `{u32 data, u16 mode, u16 loopStart, u16 length, u16 rate}`. mode bit0 = loop, bit1 = ping-pong
(so 0, 1, 3 occur). `length` is also the loop end. Shared by all 44 songs (a song's header can point at its own bank; DBZKit's XM import does).

## Songs (HIGH)
`g_MusicTrackTable` 0x81D4B2C, 44 x 20 bytes = `MusicTrackInfo`:

| off | field | meaning |
|---|---|---|
| 0 | pattern_table | u32[] pointers to patterns |
| 4 | order_list | u8 pattern ids, ended by 0xFF (loops to the first) |
| 8 | instrument_bank | -> g_InstrumentBank |
| 12 | event_presets | u8[14] flag bytes (`g_EventPresets`, shared) |
| 16 | tempo | BPM-like: samples per tick = 40000 / tempo (~ProTracker BPM) |
| 17 | ticks_per_row | speed |

**Pattern** = `u8 rows`, then per row a list of events terminated by `0x00`. Event byte = `(preset << 4) | channel` (channel 1..15):
preset 0 -> next byte is the flag byte (remembered per channel), preset 1 -> the channel's remembered flags, preset 2..15 ->
`g_EventPresets[preset-2]`. Flag bytes then pull data: bit0 note, bit1 instrument, bit2 volume (0..64), bit3 two bytes (effect id,
param), in that order. Bits 0x10/0x20/0x40/0x80 are behaviour flags tested by `Audio_NoteOn` (0x11 = note trigger, 0x44 = set volume,
0x20 = legato, 0x88 = run the effect). Note 255 = note off; the note is an index into `g_NoteFrequencyTable` (512*2^(n/12), 104
entries) and note 60 plays an instrument at its own sample rate. All 44 songs parse cleanly with this (channels 1-8, notes <= 111).

**Effects** (`Audio_EffectTable` 0x87FCB1C, index = effect id; every handler except portamento starts with `Audio_NoteOn`; an event with flag 0x80
but no effect bytes re-runs the channel's STORED effect id/param, and 0x10 / 0x40 re-trigger the stored note / volume): 1 = **set speed** (param = ticks per row, 3-7 in the ROM; the old IDA global g_LastNoteChannel is really MusicPlayer+3),
2 = unknown (27 uses; touches MusicPlayer+1/+2/+0x10), 4 volume envelope (param nibbles: attack / decay per tick, or one-off), 5 pitch slide DOWN, 6 pitch slide UP (param
0xEx/0xFx = one-off fine step, otherwise a per-tick step from the slide tables), 7 tone portamento, 8 vibrato (low nibble depth, high nibble
speed, `g_VibratoSineTable`), 15 sample offset (param*256 samples, like XM 9xx), 17 **retrigger** every (param&0xF) ticks (like XM E9x, not an
arpeggio), 20 set tempo (param = BPM value), 24 pan (signed byte -64..+64), 27 unknown (0x802054C), 0/3/9-14/16/21 = plain note-on.
Usage counts across all songs: 24 (18707), 4 (8192), 1 (3272), 7 (2922), 15 (1650), 8 (1542), 5 (1536), 17 (979), 6 (752), 20 (211), 27 (117), 2 (27).
Per row: `Sequencer_RunChannelEffect` clears each channel's handler bits, restores the base pitch, then runs the effect; on the other ticks of the
row the per-tick handler (slide / vibrato / portamento / envelope / retrigger) runs. Voices are committed every tick (`AudioVoice_CommitPendingChanges`).

## DBZKit: the "Sound" tab
`DrGero/Audio.cs` (tables, WAV, song read/write, JSON), `DrGero/AudioXm.cs` (XM) and `DrGero/AudioRender.cs` (`SongRenderer`, a re-implementation of the game's sequencer + mixer used for playback; renders a song to 16-bit stereo at 15978 Hz, output = clamp(sum(sample*vol) >> 6) like `sub_80236F0`). Playback (play/pause/stop/seek/volume/loop) is `DBZKit/AudioPanel.cs` over winmm (`WaveOutPlayer.cs`). The Engine tools, Sprite editor and Sound tabs share one ROM copy (`EditSession`). SFX and instruments: play, export WAV, replace from WAV
(8/16/24-bit, any channels; converted to mono 8-bit, halved until it fits 65535 samples and <= 32 kHz, written at the end of the ROM).
Songs: export/import **XM** and **JSON**. XM keeps notes, note-off, instruments (samples, loops, ping-pong), volume, pan, tempo, sample
offset, retrigger (E9x), speed/BPM; the envelope/vibrato/tremolo/slide/portamento effects are dropped (counted in the status line). JSON is lossless.
The exporter shifts a song's notes down when it uses more than XM's 96 notes and records the shift in the module name (`LoG2 t<track> s<shift>`),
so re-importing an unedited export gives back the same notes and reuses the stock instruments. Import builds a private copy of the bank for
that song (new instruments appended, identical stock ones reused) so other songs are untouched.
Verified headlessly on the US ROM: XM export -> import round trip reproduces every note of all 44 songs with no new instruments; JSON
write -> read is identical for all 44; WAV round trip is exact. Not verified: how it sounds in an emulator.

## IDA state
Typed and named: `MusicTrackInfo` (fixed to 20 bytes), `SampleInstrument`, `SfxSample`, `g_MusicTrackTable`, `g_SFXDefTable`,
`g_InstrumentBank`, `g_EventPresets`, `g_NoteFrequencyTable`, `Audio_EffectTable`, `Music_TrackNN_OrderList` / `_PatternTable` (44 each;
tracks 26 and 29-43 only got their first pattern-table element defined because IDA refused to overlap existing items), the
`Audio_FxNN_*` effect handlers, `Sequencer_ParseRow`, `Sequencer_RunChannelEffect`, `AudioVoice_*`, `SfxVoices_*`, `MusicChannels_*`.
Still open: the exact envelope/vibrato/tremolo/slide parameter semantics, effect 27, and the `MusicChannel` struct's channel layout (96 bytes).
