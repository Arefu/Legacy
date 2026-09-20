// Audio_Fx11_NoteOnEnvelopeVibrato -- 0x080202E4 (IDA name was ...EnvelopeTremolo), Audio_EnvelopeDecayVibrato -- 0x08020290. MEDIUM. Effect 11: the volume envelope of
// effect 4 together with vibrato (Audio_VibratoUpdate stays the per-tick handler). Not used by any song; transcribed for completeness.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_EnvelopeDecayVibrato(MusicChannel* c) {
    const int v = c->volume_now - c->envelope_rate;
    c->volume_now = static_cast<u8>(v);
    if (v <= 0) {
        c->volume_now = 0;
        c->tick_handler = Audio_VibratoUpdate;      // the envelope is done; only the vibrato continues
    }
    c->voice.volume = c->volume_now;
    c->voice.pending |= kPendingVolume;
    Audio_VibratoUpdate(c);
}

void Audio_Fx11_NoteOnEnvelopeVibrato(MusicChannel* c) {
    Audio_NoteOn(c);
    if (c->effect_param) c->envelope_param = c->effect_param;
    if (!(c->state & kStateNotePlaying)) return;
    if (c->event_flags & (kEventNote | kEventRetrigger)) c->vibrato_phase = 0;
    const u8 p = c->envelope_param;
    if ((p & 0xF) != 0) {
        if ((p >> 4) != 0) {
            if ((~p & 0xF) != 0) {
                if ((p >> 4) == 15) { c->envelope_rate = p & 0xF; Audio_EnvelopeDecayStop(c); c->tick_handler = Audio_VibratoUpdate; }
            } else { c->envelope_rate = p >> 4; Audio_EnvelopeAttack(c); c->tick_handler = Audio_VibratoUpdate; }
        } else {
            c->envelope_rate = p;
            c->tick_handler = Audio_EnvelopeDecayVibrato;
            if (c->envelope_rate == 15) Audio_EnvelopeDecayStop(c);
        }
    } else {
        c->envelope_rate = p >> 4;
        c->tick_handler = reinterpret_cast<TickHandler>(0x080201BB);   // TODO(unknown): the attack+vibrato handler (0x080201BA, unnamed in IDA)
        if (c->envelope_rate == 15) Audio_EnvelopeAttack(c);
    }
    c->state |= kStateHandlerWhileNote;
    Audio_VibratoUpdate(c);
}

}  // namespace dbzlog2::audio
