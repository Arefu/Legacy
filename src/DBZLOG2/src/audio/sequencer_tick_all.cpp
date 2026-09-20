// Audio_SequencerTick -- 0x080209B8. HIGH. Runs the sequencer for `frames` output samples: every `tick_reload` samples it calls Sequencer_Tick,
// and in between mixes the 15 music voices into the accumulator.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void AudioVoice_MixSamples(AudioVoice* voice, s32* accumulator, int frames);   // 0x0801FA72

void Audio_SequencerTick(MusicPlayer* player, s32* accumulator, int frames) {
    if (player->status != 1) return;
    while (player->tick_remaining < frames) {
        if (player->tick_remaining != 0) {
            const int n = player->tick_remaining;
            for (int i = 0; i < kMusicChannelCount; ++i) AudioVoice_MixSamples(&player->channels[i].voice, accumulator, n);
            accumulator += n;
            frames -= n;
        }
        Sequencer_Tick(player);
        player->tick_remaining = player->tick_reload;
    }
    for (int j = 0; j < kMusicChannelCount; ++j) AudioVoice_MixSamples(&player->channels[j].voice, accumulator, frames);
    player->tick_remaining -= frames;
}

}  // namespace dbzlog2::audio
