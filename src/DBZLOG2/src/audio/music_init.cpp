// Music_Init -- 0x08020A4C. HIGH. Starts a song: resets the cursor and the 15 channels.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Music_Init(MusicPlayer* player, const MusicTrackInfo* track) {
    SequencerCursor& cursor = g_SequencerCursor;
    cursor.pattern_length = 1;                     // forces Sequencer_ParseRow to load pattern 0 on the first tick
    player->status = 1;
    cursor.row_counter = 0;
    cursor.track = track;
    player->ticks_per_row = track->ticks_per_row;  // IDA called this field "time_signature"
    cursor.pattern_index = -1;
    for (int i = 0; i < kMusicChannelCount; ++i) {
        MusicChannel* ch = &player->channels[i];
        ch->state = 0;
        ch->pad5f = 0;
        ch->event_flags = 0;
        ch->voice.pending = 0;
        ch->voice.owner = static_cast<u32>(reinterpret_cast<uintptr_t>(ch));   // self pointer (MusicChannels_Reset does the same)
    }
    cursor.tick_low = track->tempo;
    player->tick_reload = static_cast<u16>(kTickNumerator / track->tempo);      // Div32(tempo, 40000)
    cursor.active = 1;
}

}  // namespace dbzlog2::audio
