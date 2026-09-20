// SFX_Queue -- 0x0801FC9C (0x30 bytes). MEDIUM (the field names are inferred: IDA's SFXSlot is really an AudioVoice).
// Starts a sound effect on the first free voice. An SfxSample has the same first 12 bytes layout as a SampleInstrument (its u32 `loop` is the
// instrument's mode + loop_start), so the voice's "instrument" pointer can point straight at g_SFXDefTable[n]. 0x4000 is the frequency of note 60 =
// native pitch. pending 0x1F = active | pitch | restart | reload instrument | volume.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

AudioVoice* SFX_Queue(AudioVoice* pool, RomPtr sfx, u8 volume, s8 pan) {
    for (int i = 0; i < kSfxVoiceCount; ++i) {
        if (pool[i].pending & kPendingActive) continue;
        AudioVoice* v = &pool[i];
        v->instrument = sfx;
        v->frequency = 0x4000;
        v->volume = volume;
        v->pan = pan;
        v->pending = 0x1F;
        return v;
    }
    return nullptr;                                 // all 16 voices busy
}

}  // namespace dbzlog2::audio
