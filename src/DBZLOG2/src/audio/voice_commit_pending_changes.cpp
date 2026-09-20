// AudioVoice_CommitPendingChanges -- 0x0801F9D6 (was sub_801F9D6). HIGH. Applies a voice's pending bits once per tick.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void AudioVoice_CommitPendingChanges(AudioVoice* v) {
    if (!(v->pending & kPendingActive)) return;

    if (v->pending & kPendingVolume) {
        const u32 scaled = v->master_volume * v->volume;
        const u32 left = static_cast<u32>((64 - v->pan) * static_cast<s32>(scaled)) >> 10;
        const u32 right = static_cast<u32>((v->pan + 64) * static_cast<s32>(scaled)) >> 10;
        v->volume_left = static_cast<u16>(left);
        v->volume_right = static_cast<u16>(right);
        v->mix_kernel = (left | right) ? 0x03000520u : 0u;          // nullsub_2 when silent
    }
    if (v->pending & kPendingInstrument) {
        const SampleInstrument* ins = InstrumentAt(v->instrument);
        v->data = ins->data;
        v->loop_start = static_cast<u32>(ins->loop_start) << 14;
        v->loop_end = static_cast<u32>(ins->length) << 14;
        v->sample_rate = ins->sample_rate;
        v->mode = static_cast<u8>(ins->mode);
    }
    if (v->pending & kPendingRestart) {
        v->position = 0;
        v->mode &= 0xFB;                                            // forward again
    }
    if (v->pending & kPendingPitch) {
        v->step_magnitude = (4195u * ((v->frequency * v->sample_rate) >> 14) + 2047u) >> 12;
        v->step = (v->mode & 4) ? -static_cast<s32>(v->step_magnitude) : static_cast<s32>(v->step_magnitude);
    }
    v->pending = kPendingActive;
}

}  // namespace dbzlog2::audio
