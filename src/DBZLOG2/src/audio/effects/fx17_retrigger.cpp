// Audio_Fx17_NoteOnRetrigger -- 0x08020500 (IDA: ...NoteOnArpeggio), per-tick Audio_RetriggerUpdate -- 0x080204D4. MEDIUM.
// Restarts the sample every (param & 0xF) ticks; like XM E9x. Not an arpeggio.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_RetriggerUpdate(MusicChannel* c) {
    if (--c->retrigger_counter == 0) {
        c->voice.pending |= kPendingActive | kPendingRestart;   // |= 5
        c->retrigger_counter = c->retrigger_param & 0xF;
        c->state |= kStateNotePlaying;
    }
}

void Audio_Fx17_NoteOnRetrigger(MusicChannel* c) {
    Audio_NoteOn(c);
    if (c->effect_param) c->retrigger_param = c->effect_param;
    c->tick_handler = Audio_RetriggerUpdate;
    c->retrigger_counter = c->retrigger_param & 0xF;
    c->state = static_cast<u8>(4 * (c->state >> 2) + kStateHandlerAlways);
}

}  // namespace dbzlog2::audio
