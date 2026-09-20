// Sequencer_Tick -- 0x0802090C. HIGH. One sequencer tick: on the first tick of a row parse it and run each channel's effect, on the other ticks
// run the per-tick handlers (slide / vibrato / portamento / envelope / retrigger); then commit every voice.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Sequencer_Tick(MusicPlayer* player) {
    player->tick_counter -= 1;
    if (player->tick_counter != 0) {
        for (int i = 0; i < kMusicChannelCount; ++i) {
            MusicChannel* ch = &player->channels[i];
            const u8 mode = ch->state & 3;
            // mode 2 (retrigger) always runs; mode 1 only while a note is playing
            if (mode != 0 && (mode != 1 || (ch->state & kStateNotePlaying) != 0)) ch->tick_handler(ch);
        }
    } else {
        Sequencer_ParseRow(player);
        for (int j = 0; j < kMusicChannelCount; ++j) Sequencer_RunChannelEffect(&player->channels[j]);
        player->tick_counter = player->ticks_per_row;
    }
    for (int k = 0; k < kMusicChannelCount; ++k) AudioVoice_CommitPendingChanges(&player->channels[k].voice);
}

}  // namespace dbzlog2::audio
