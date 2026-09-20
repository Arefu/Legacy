// Audio_Fx01_NoteOnSetLastChannel -- 0x0801FEB0. Effect 1 = SET SPEED. HIGH.
// The old IDA global g_LastNoteChannel is MusicPlayer+3, i.e. ticks_per_row; the effect param (3..7 in the ROM) replaces it.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_Fx01_SetSpeed(MusicChannel* c) {
    Audio_NoteOn(c);
    g_MusicPlayer.ticks_per_row = c->effect_param;
}

}  // namespace dbzlog2::audio
