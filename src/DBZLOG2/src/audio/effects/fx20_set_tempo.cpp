// Audio_Fx20_SetTempo -- 0x08020578. HIGH. Effect 20: param is the new tempo value; samples per tick = 40000 / param.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_Fx20_SetTempo(MusicChannel* c) {
    Audio_NoteOn(c);
    g_AudioTempoRaw = c->effect_param;
    g_MusicPlayer.tick_reload = static_cast<u16>(kTickNumerator / g_AudioTempoRaw);   // Div32(raw, 40000)
}

}  // namespace dbzlog2::audio
