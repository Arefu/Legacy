// Audio_Fx27_NoteDelay -- 0x0802054C, per-tick Audio_NoteDelayUpdate -- 0x08020534. HIGH. Effect 27 = NOTE DELAY (like XM EDx): the note is not triggered
// now; it is triggered after `param` ticks. 117 events in the songs. (Was "Fx27_Unknown".)
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_NoteDelayUpdate(MusicChannel* c) {
    if (--c->retrigger_counter == 0) Audio_NoteOn(c);
}

void Audio_Fx27_NoteDelay(MusicChannel* c) {
    c->retrigger_counter = c->effect_param;
    c->tick_handler = Audio_NoteDelayUpdate;
    c->state = static_cast<u8>(4 * (c->state >> 2) + kStateHandlerAlways);
}

}  // namespace dbzlog2::audio
