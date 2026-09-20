// Audio_Fx06_NoteOnSlideUp -- 0x08020074, per-tick Audio_SlideUpUpdate -- 0x08020056. MEDIUM.
// Effect 6 = pitch slide up. param hi nibble 0xF: one-off g_SlideUpTable[p & 15] >> 14; 0xE: one-off g_FineSlideUpTable[p & 15] >> 15;
// otherwise a per-tick step freq = freq * g_SlideUpTable[param] >> 14.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_SlideUpUpdate(MusicChannel* c) {
    const u32 f = static_cast<u32>(c->frequency_base * g_SlideUpTable[c->slide_param]) >> 14;
    c->frequency_base = f;
    c->voice.frequency = f;
    c->voice.pending |= kPendingPitch;
}

void Audio_Fx06_NoteOnSlideUp(MusicChannel* c) {
    Audio_NoteOn(c);
    if (c->effect_param) c->slide_param = c->effect_param;
    if (c->state & kStateNotePlaying) {
        const int hi = c->slide_param >> 4;
        if (hi == 15) {
            const u32 f = static_cast<u32>(c->frequency_base * g_SlideUpTable[c->slide_param & 0xF]) >> 14;
            c->frequency_base = c->voice.frequency = f;
            c->voice.pending |= kPendingPitch;
        } else if (hi == 14) {
            const u32 f = static_cast<u32>(c->frequency_base * g_FineSlideUpTable[c->slide_param & 0xF]) >> 15;
            c->frequency_base = c->voice.frequency = f;
            c->voice.pending |= kPendingPitch;
        } else {
            c->tick_handler = Audio_SlideUpUpdate;
            c->state |= kStateHandlerWhileNote;
        }
    }
}

}  // namespace dbzlog2::audio
