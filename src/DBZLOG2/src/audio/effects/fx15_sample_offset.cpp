// Audio_Fx15_SampleOffset -- 0x080204A8 (IDA: ...NoteOnDirect). MEDIUM.
// Starts the sample `param * 256` samples in (position is 14-bit fixed point, so param << 22); like XM 9xx.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_Fx15_SampleOffset(MusicChannel* c) {
    Audio_NoteOn(c);
    if (c->event_flags & (kEventNote | kEventRetrigger)) {
        c->voice.position = static_cast<u32>(c->effect_param) << 22;
        c->voice.pending &= static_cast<u8>(~kPendingRestart);
        c->voice.mode &= 0xFB;
    }
}

}  // namespace dbzlog2::audio
