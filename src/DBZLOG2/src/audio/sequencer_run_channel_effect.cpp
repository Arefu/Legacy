// Sequencer_RunChannelEffect -- 0x080207C0 (was sub_80207C0). HIGH. Called for every channel on the first tick of each row.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Sequencer_RunChannelEffect(MusicChannel* c) {
    c->state = static_cast<u8>(4 * (c->state >> 2));               // per-tick handler bits are cleared every row
    if (c->state & kStateNotePlaying) {
        if (c->voice.frequency != c->frequency_base) {              // undo last row's vibrato offset
            c->voice.frequency = c->frequency_base;
            c->voice.pending |= kPendingPitch;
        }
    }
    if (c->event_flags) {
        // Flag 0x88 (effect bytes present, or "run stored effect") selects the stored effect id; otherwise effect 0 = plain note-on.
        const u8 effect = (c->event_flags & (kEventEffect | kEventEffectStored)) ? c->effect_id : 0;
        g_AudioEffectTable[effect](c);
    }
}

}  // namespace dbzlog2::audio
