// Audio_Fx24_SetPanning -- 0x080205E2. MEDIUM. Effect 24: param is a signed pan, -64 (left) .. +64 (right), 0 = centre.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_Fx24_SetPanning(MusicChannel* c) {
    Audio_NoteOn(c);
    c->voice.pan = static_cast<s8>(c->effect_param);
    c->voice.pending |= kPendingVolume;
}

}  // namespace dbzlog2::audio
