// Audio_Fx02_NoteOnPoly -- 0x0801FEC6. Effect 2 (27 uses, params 1..5). LOW: purpose unknown; the accesses are copied verbatim.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_Fx02_Unknown(MusicChannel* c) {
    Audio_NoteOn(c);
    g_MusicPlayer.unknown_01 = g_MusicPlayer.unknown_02 - 1;   // g_AudioRemainingNotes = g_AudioNoteCount - 1
    g_MusicPlayer.unknown_10 = c->effect_param - 1;            // g_AudioPrevChannel = param - 1
}

}  // namespace dbzlog2::audio
