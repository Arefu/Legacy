#pragma once
// Sound engine structures and entry points. Layouts verified in IDA (see DBZKit/Audio-Notes.md).
// HIGH unless a field says otherwise.
#include "dbzlog2/types.h"

namespace dbzlog2::audio {

// ---- ROM data ---------------------------------------------------------------------------------------------------------

// g_SFXDefTable[64] (0x080E15B0). Data is signed 8-bit PCM.
struct SfxSample {
    RomPtr data;         // +0x00
    u32 loop;            // +0x04  whole-sample loop flag
    u16 length;          // +0x08  samples
    u16 sample_rate;     // +0x0A  Hz (16000 or 8000)
};
static_assert(sizeof(SfxSample) == 12);

// g_InstrumentBank[101] (0x0814A8A4). Data is signed 8-bit PCM; `length` doubles as the loop end.
struct SampleInstrument {
    RomPtr data;         // +0x00
    u16 mode;            // +0x04  bit0 = loop, bit1 = ping-pong
    u16 loop_start;      // +0x06  samples
    u16 length;          // +0x08  samples
    u16 sample_rate;     // +0x0A  Hz; plays at this rate on note 60
};
static_assert(sizeof(SampleInstrument) == 12);

// g_MusicTrackTable[44] (0x081D4B2C).
struct MusicTrackInfo {
    RomPtr pattern_table;               // +0x00  u32[]: pattern id -> pattern data
    RomPtr order_list;                  // +0x04  pattern ids, 0xFF ends (then loops to the first)
    RomPtr instrument_bank;             // +0x08  SampleInstrument[]
    RomPtr event_presets;               // +0x0C  u8[14] flag bytes shared by all songs
    u8 tempo;                           // +0x10  BPM-like: samples per tick = 40000 / tempo
    u8 ticks_per_row;                   // +0x11  initial speed
    u16 unused;                         // +0x12
};
static_assert(sizeof(MusicTrackInfo) == 20);

// ---- voice / channel state -----------------------------------------------------------------------------------------------------

// One mixer voice (0x40 bytes). Music channels embed one at +0; SFX voices are bare AudioVoice[16].
struct AudioVoice {
    u32 position;            // +0x00  sample position, 14-bit fixed point (index = position >> 14)
    s32 step;                // +0x04  per-output-sample advance; negative while a ping-pong sample plays backwards
    RomPtr data;             // +0x08  PCM (signed 8-bit)
    u16 volume_left;         // +0x0C  (64 - pan) * master * volume >> 10
    u16 volume_right;        // +0x0E  (64 + pan) * master * volume >> 10
    u8 pending;              // +0x10  1 active, 2 pitch, 4 restart, 8 reload instrument, 0x10 volume
    u8 mode;                 // +0x11  bit0 loop, bit1 ping-pong, bit2 currently reversed
    u8 volume;               // +0x12  0..64
    s8 pan;                  // +0x13  -64..+64
    u32 sample_rate;         // +0x14
    u32 mix_kernel;          // +0x18  IWRAM mixing routine (or a no-op when both volumes are 0)
    u32 step_magnitude;      // +0x1C
    u32 loop_start;          // +0x20  14-bit fixed point
    u32 loop_end;            // +0x24  14-bit fixed point (= length << 14)
    RomPtr instrument;       // +0x28  SampleInstrument*
    u32 owner;               // +0x2C  self / owner pointer (see MusicChannels_Reset)  [MEDIUM]
    u32 frequency;           // +0x30  current note frequency (g_NoteFrequencyTable units)
    u32 master_volume;       // +0x34  4 for music, 8 for SFX (AudioSystem_Init)
    u32 reserved38[2];       // +0x38
};
static_assert(sizeof(AudioVoice) == 0x40);

struct MusicChannel;
using TickHandler = void (*)(MusicChannel*);

// One music channel (0x60 bytes): an AudioVoice followed by sequencer state.
struct MusicChannel {
    AudioVoice voice;        // +0x00
    u32 frequency_base;      // +0x40  note frequency before slides / vibrato (voice.frequency is the modulated one)
    u32 frequency_target;    // +0x44  portamento target
    u8 volume_now;           // +0x48  running volume (envelope acts on this)
    u8 pad49;
    u8 state;                // +0x4A  bit0/1 per-tick handler mode, bit2 note playing, bit3 portamento armed
    u8 envelope_rate;        // +0x4B
    u8 envelope_param;       // +0x4C
    u8 slide_param;          // +0x4D  shared by effects 5, 6, 7
    u8 retrigger_param;      // +0x4E
    u8 vibrato_speed;        // +0x4F
    u8 vibrato_depth;        // +0x50
    u8 vibrato_phase;        // +0x51
    u8 retrigger_counter;    // +0x52
    u8 pad53;
    TickHandler tick_handler;            // +0x54  per-tick handler (a 32-bit ROM address on the GBA)
    u8 event_flags;          // +0x58  flags of this row's event (0 when the channel had none)
    u8 last_event_flags;     // +0x59  remembered by event preset 0
    u8 note;                 // +0x5A  stored note (0xFF = off)
    u8 instrument_index;     // +0x5B  stored instrument
    u8 event_volume;         // +0x5C  stored volume 0..64
    u8 effect_id;            // +0x5D  stored effect
    u8 effect_param;         // +0x5E  stored effect parameter
    u8 pad5f;
};
#if UINTPTR_MAX == 0xFFFFFFFFu
static_assert(sizeof(MusicChannel) == 0x60);
#endif

// Event flag bits (Sequencer_ParseRow / Audio_NoteOn).
enum EventFlags : u8 {
    kEventNote = 0x01,        // a note byte follows
    kEventInstrument = 0x02,  // an instrument byte follows
    kEventVolume = 0x04,      // a volume byte follows
    kEventEffect = 0x08,      // effect id + param follow
    kEventRetrigger = 0x10,   // (re)trigger using the STORED note
    kEventLegato = 0x20,      // trigger without reloading the instrument
    kEventVolumeStored = 0x40,   // set volume from the STORED volume
    kEventEffectStored = 0x80,   // run the STORED effect
};

// Voice pending bits.
enum VoicePending : u8 {
    kPendingActive = 0x01,
    kPendingPitch = 0x02,
    kPendingRestart = 0x04,
    kPendingInstrument = 0x08,
    kPendingVolume = 0x10,
};

// MusicChannel::state bits.
enum ChannelState : u8 {
    kStateHandlerWhileNote = 0x01,   // per-tick handler runs only while a note plays
    kStateHandlerAlways = 0x02,      // per-tick handler always runs (retrigger)
    kStateNotePlaying = 0x04,
    kStatePortamentoArmed = 0x08,
};

constexpr int kMusicChannelCount = 15;
constexpr int kSfxVoiceCount = 16;
constexpr u8 kNoteOff = 0xFF;

// ---- entry points (one .cpp each) ---------------------------------------------------------------------------------
void Audio_NoteOn(MusicChannel* channel);
void Audio_Fx01_SetSpeed(MusicChannel* channel);            // IDA: Audio_Fx01_NoteOnSetLastChannel
void Audio_Fx02_Unknown(MusicChannel* channel);             // IDA: Audio_Fx02_NoteOnPoly  [LOW]
void Audio_Fx04_NoteOnEnvelope(MusicChannel* channel);
void Audio_Fx05_NoteOnSlideDown(MusicChannel* channel);
void Audio_Fx06_NoteOnSlideUp(MusicChannel* channel);
void Audio_Fx07_Portamento(MusicChannel* channel);
void Audio_Fx08_NoteOnVibrato(MusicChannel* channel);
void Audio_Fx15_SampleOffset(MusicChannel* channel);
void Audio_Fx17_NoteOnRetrigger(MusicChannel* channel);
void Audio_Fx20_SetTempo(MusicChannel* channel);
void Audio_Fx11_NoteOnEnvelopeVibrato(MusicChannel* channel);
void Audio_Fx12_PortamentoEnvelope(MusicChannel* channel);   // 0x080203A8 (transcription pending)
void Audio_Fx21_SetVibrato(MusicChannel* channel);
void Audio_Fx24_SetPanning(MusicChannel* channel);
void Audio_Fx27_NoteDelay(MusicChannel* channel);
void Audio_NoteDelayUpdate(MusicChannel* channel);
void Audio_EnvelopeDecayVibrato(MusicChannel* channel);
void Sequencer_ParseRow(MusicPlayer* player);
void Sequencer_Tick(MusicPlayer* player);

void Audio_EnvelopeAttack(MusicChannel* channel);
void Audio_EnvelopeDecayStop(MusicChannel* channel);
void Audio_SlideDownUpdate(MusicChannel* channel);
void Audio_SlideUpUpdate(MusicChannel* channel);
void Audio_PortamentoDownUpdate(MusicChannel* channel);
void Audio_PortamentoUpUpdate(MusicChannel* channel);
void Audio_VibratoUpdate(MusicChannel* channel);
void Audio_RetriggerUpdate(MusicChannel* channel);

void Sequencer_RunChannelEffect(MusicChannel* channel);
void AudioVoice_CommitPendingChanges(AudioVoice* voice);

}  // namespace dbzlog2::audio
