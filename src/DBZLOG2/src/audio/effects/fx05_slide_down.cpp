// Audio_Fx05_NoteOnSlideDown -- 0x0801FFE6, per-tick Audio_SlideDownUpdate -- 0x0801FFC8. MEDIUM.
// Effect 5 = pitch slide down. param hi nibble 0xF: one-off g_SlideDownTable[p & 15] >> 16; 0xE: one-off g_FineSlideDownTable[p & 15] >> 16;
// otherwise a per-tick step freq = freq * g_SlideDownTable[param] >> 16. (The table index for 0xE/0xF is `(2*p) & 0x1F` bytes.)
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_SlideDownUpdate(MusicChannel* c) {
    const u32 f = static_cast<u32>(c->frequency_base * g_SlideDownTable[c->slide_param]) >> 16;
    c->frequency_base = f;
    c->voice.frequency = f;
    c->voice.pending |= kPendingPitch;
}

void Audio_Fx05_NoteOnSlideDown(MusicChannel* c) {
    Audio_NoteOn(c);
    if (c->effect_param) c->slide_param = c->effect_param;
    if (c->state & kStateNotePlaying) {
        const int hi = c->slide_param >> 4;
        if (hi == 15) {
            const u32 f = static_cast<u32>(c->frequency_base * g_SlideDownTable[c->slide_param & 0xF]) >> 16;
            c->frequency_base = c->voice.frequency = f;
            c->voice.pending |= kPendingPitch;
        } else if (hi == 14) {
            const u32 f = static_cast<u32>(c->frequency_base * g_FineSlideDownTable[c->slide_param & 0xF]) >> 16;
            c->frequency_base = c->voice.frequency = f;
            c->voice.pending |= kPendingPitch;
        } else {
            c->tick_handler = Audio_SlideDownUpdate;
            c->state |= kStateHandlerWhileNote;
        }
    }
}

}  // namespace dbzlog2::audio
