#pragma once
// Globals and ROM tables the sound engine reads. On the GBA these live at the addresses in rom_map.h; the host build defines them
// in audio_state.cpp (TODO) or points them at a loaded ROM image.
#include "dbzlog2/audio/audio.h"

namespace dbzlog2::audio {

// The music player (IDA: g_MusicChannel, 0x030026C8): a 0x18-byte header followed by the 15 channels = 0x5B8 bytes. (IDA's type is 1764 bytes
// because it also swallows the first 300 bytes of the SFX voices that follow in the mixer object.)
// Offsets confirmed from the decompile are marked; the cursor fields marked TBD sit somewhere in +0x09..+0x17 and their exact
// offsets still have to be read from IDA (it was unreachable when this was written).
struct MusicPlayer {
    u8 tick_counter;         // +0x00  counts down; the row advances at 0, then reloads from ticks_per_row
    u8 unknown_01;           // +0x01  Audio_Fx02 writes this (IDA: g_AudioRemainingNotes)   [LOW]
    u8 unknown_02;           // +0x02  Audio_Fx02 reads this (IDA: g_AudioNoteCount)         [LOW]
    u8 ticks_per_row;        // +0x03  the song's speed; Audio_Fx01 changes it (IDA: g_LastNoteChannel)
    u8 unknown_04;           // +0x04
    u8 status;               // +0x05  1 = playing (IDA: g_MusicState)
    u16 tick_reload;         // +0x06  samples per tick = 40000 / tempo (IDA: g_AudioTempoDivided)
    u16 tick_remaining;      // +0x08  samples left in the current tick
    u8 cursor_bytes[6];      // +0x0A..+0x0F  see SequencerCursor below (TBD)
    u8 unknown_10;           // +0x10  Audio_Fx02 writes this (IDA: g_AudioPrevChannel)      [LOW]
    u8 cursor_bytes2[7];     // +0x11..+0x17
    MusicChannel channels[kMusicChannelCount];   // +0x18
};
static_assert(sizeof(MusicPlayer) == 0x18 + kMusicChannelCount * 0x60 || sizeof(void*) != 4, "MusicPlayer is 0x5B8 bytes on the GBA");

// The row cursor Sequencer_ParseRow / Music_Init use. TBD offsets inside MusicPlayer; kept as a named group so the code reads like the
// decompile (a1->pattern_length, a1->row_counter, ...).
struct SequencerCursor {
    u8 pattern_length;       // rows in the current pattern
    u8 row_counter;          // current row
    s32 pattern_index;       // index into the order list (-1 before the first pattern)
    RomPtr read_ptr;         // next event byte
    const MusicTrackInfo* track;
    u8 active;
    u16 tick_low;
};

extern MusicPlayer g_MusicPlayer;                      // 0x030026C8
extern SequencerCursor g_SequencerCursor;              // TBD, see above
extern const MusicTrackInfo* g_CurrentMusicTrack;      // 0x030026D4
extern u8 g_AudioTempoRaw;                             // 0x030026CC

extern const u32 g_NoteFrequencyTable[104];            // 0x087FC93C
extern const u16 g_FineSlideUpTable[64];               // 0x087FCB9C
extern const u16 g_SlideUpTable[64];                   // 0x087FCC1C
extern const u16 g_FineSlideDownTable[64];             // 0x087FCE1C
extern const u16 g_SlideDownTable[64];                 // 0x087FCE9C
extern const s8 g_VibratoSineTable[64];                // 0x087FD09C

using EffectHandler = void (*)(MusicChannel*);
extern const EffectHandler g_AudioEffectTable[32];     // 0x087FCB1C, indexed by effect id

// PCM / instrument access by ROM address (a plain pointer cast on the GBA).
const SampleInstrument* InstrumentAt(RomPtr address);
const u8* RomData(RomPtr address);

// The mixer object (IDA: the object passed to Audio_MixerUpdate, size >= 0xD00). Offsets from the decompile of Audio_MixerUpdate / Audio_SequencerTick.
struct AudioMixer {
    u32 busy;                       // +0x000  1 while Audio_MixerUpdate runs
    u32 dma_position;               // +0x004  where the DMA has read up to (ring index, wraps at 0x400)
    u32 last_position;              // +0x008  where the mixer last refilled to
    MusicPlayer music;              // +0x00C  header (0x18) + 15 channels
    AudioVoice sfx[kSfxVoiceCount]; // +0x5C4  16 SFX voices
    s32 accumulator[512];           // +0x9C4  per-sample (left | right << 16) sums for one refill
};

constexpr u32 kTickNumerator = 40000;                  // Div32(tempo, 40000): samples per tick

}  // namespace dbzlog2::audio
