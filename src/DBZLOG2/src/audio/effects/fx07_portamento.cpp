// Audio_Fx07_Portamento -- 0x08020184 (+ Audio_PortamentoDownUpdate 0x08020100, Audio_PortamentoUpUpdate 0x08020142). MEDIUM.
// Tone portamento: slides toward the new note's frequency without retriggering; only acts while a note is playing.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_PortamentoDownUpdate(MusicChannel* c) {
    const u32 f = static_cast<u32>(c->frequency_base * g_SlideDownTable[c->slide_param]) >> 16;
    c->frequency_base = c->voice.frequency = f;
    c->voice.pending |= kPendingPitch;
    if (f < c->frequency_target) {
        c->frequency_base = c->voice.frequency = c->frequency_target;
        c->voice.pending |= kPendingPitch;
        c->state = static_cast<u8>(4 * ((c->state & 0xF7) >> 2));   // handler off, portamento disarmed
    }
}

void Audio_PortamentoUpUpdate(MusicChannel* c) {
    const u32 f = static_cast<u32>(c->frequency_base * g_SlideUpTable[c->slide_param]) >> 14;
    c->frequency_base = c->voice.frequency = f;
    c->voice.pending |= kPendingPitch;
    if (f > c->frequency_target) {
        c->frequency_base = c->voice.frequency = c->frequency_target;
        c->voice.pending |= kPendingPitch;
        c->state = static_cast<u8>(4 * ((c->state & 0xF7) >> 2));
    }
}

void Audio_Fx07_Portamento(MusicChannel* c) {
    if (c->effect_param) c->slide_param = c->effect_param;
    if (!(c->state & kStateNotePlaying)) return;
    const u8 flags = c->event_flags;
    bool arm = false;
    if (flags & (kEventNote | kEventRetrigger)) {
        if (c->note != kNoteOff) {
            c->frequency_target = g_NoteFrequencyTable[c->note];
            c->state |= kStatePortamentoArmed;
            arm = true;
        }
    } else if (c->state & kStatePortamentoArmed) {
        arm = true;
    }
    if (arm) {
        c->tick_handler = (c->frequency_target <= c->frequency_base) ? Audio_PortamentoDownUpdate : Audio_PortamentoUpUpdate;
        c->state |= kStateHandlerWhileNote;
    }
    if (flags & (kEventVolume | kEventVolumeStored)) {
        c->volume_now = c->event_volume;
        c->voice.volume = c->event_volume;
        c->voice.pending |= kPendingVolume;
    }
}

}  // namespace dbzlog2::audio
