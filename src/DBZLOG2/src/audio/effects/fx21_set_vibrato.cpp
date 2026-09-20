// Audio_Fx21_SetVibrato -- 0x08020596. HIGH. Effect 21: sets the vibrato parameters directly (depth = param & 0xF, speed = param >> 4, unlike effect 8 which
// multiplies the depth by 4) and updates at once. Not used by any song.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_Fx21_SetVibrato(MusicChannel* c) {
    Audio_NoteOn(c);
    if (c->event_flags & (kEventNote | kEventRetrigger)) c->vibrato_phase = 0;
    const u8 p = c->effect_param;
    if (p & 0xF) c->vibrato_depth = p & 0xF;
    if (p >> 4) c->vibrato_speed = p >> 4;
    c->tick_handler = Audio_VibratoUpdate;
    c->state |= kStateHandlerWhileNote;
    Audio_VibratoUpdate(c);
}

}  // namespace dbzlog2::audio
