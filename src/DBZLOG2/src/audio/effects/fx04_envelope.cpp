// Audio_Fx04_NoteOnEnvelope -- 0x0801FF44, plus Audio_EnvelopeAttack (0x0801FF16) and Audio_EnvelopeDecayStop (0x0801FEE8). MEDIUM.
// param nibbles: low==0 -> per-tick attack by hi; hi==0 -> per-tick decay by low (stops at 0); low==15 -> one-off attack by hi;
// hi==15 -> one-off decay by low. A zero param keeps the previous one.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_EnvelopeAttack(MusicChannel* c) {
    const int v = c->volume_now + c->envelope_rate;
    c->volume_now = static_cast<u8>(v);
    if (v >= 64) {
        c->volume_now = 64;
        c->state = static_cast<u8>(4 * (c->state >> 2));       // handler off
    }
    c->voice.volume = c->volume_now;
    c->voice.pending |= kPendingVolume;
}

void Audio_EnvelopeDecayStop(MusicChannel* c) {
    const int v = c->volume_now - c->envelope_rate;
    c->volume_now = static_cast<u8>(v);
    if (v <= 0) {
        c->volume_now = 0;
        c->state = static_cast<u8>(4 * (c->state >> 2));
    }
    c->voice.volume = c->volume_now;
    c->voice.pending |= kPendingVolume;
}

void Audio_Fx04_NoteOnEnvelope(MusicChannel* c) {
    Audio_NoteOn(c);
    if (c->effect_param) c->envelope_param = c->effect_param;
    const u8 p = c->envelope_param;
    if ((p & 0xF) != 0) {
        if ((p >> 4) != 0) {
            if ((~p & 0xF) != 0) {
                if ((p >> 4) == 15) {                           // hi == 15: one-off decay by the low nibble
                    c->envelope_rate = p & 0xF;
                    Audio_EnvelopeDecayStop(c);
                }
            } else {                                            // low == 15: one-off attack by the high nibble
                c->envelope_rate = p >> 4;
                Audio_EnvelopeAttack(c);
            }
        } else {                                                // hi == 0: per-tick decay by the low nibble
            c->envelope_rate = p;
            c->tick_handler = Audio_EnvelopeDecayStop;
            c->state |= kStateHandlerWhileNote;
            if (c->envelope_rate == 15) Audio_EnvelopeDecayStop(c);
        }
    } else {                                                    // low == 0: per-tick attack by the high nibble
        c->envelope_rate = p >> 4;
        c->tick_handler = Audio_EnvelopeAttack;
        c->state |= kStateHandlerWhileNote;
        if (c->envelope_rate == 15) Audio_EnvelopeAttack(c);
    }
}

}  // namespace dbzlog2::audio
