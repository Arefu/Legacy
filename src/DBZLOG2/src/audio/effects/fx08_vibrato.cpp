// Audio_Fx08_NoteOnVibrato -- 0x08020240 (IDA: ...NoteOnTremolo), per-tick Audio_VibratoUpdate -- 0x080201F8. MEDIUM.
// param: low nibble = depth (*4, &0x3F), high nibble = speed. Each tick the phase advances by `speed` (mod 64) and the pitch is
// modulated by depth * g_VibratoSineTable[phase] >> 6 through the fine slide tables.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_VibratoUpdate(MusicChannel* c) {
    c->vibrato_phase = (c->vibrato_phase + c->vibrato_speed) & 0x3F;
    const int m = (c->vibrato_depth * g_VibratoSineTable[c->vibrato_phase]) >> 6;
    u32 f;
    if (m >= 0) f = static_cast<u32>(c->frequency_base * g_FineSlideUpTable[m]) >> 15;
    else        f = static_cast<u32>(c->frequency_base * g_FineSlideDownTable[-m]) >> 16;
    c->voice.frequency = f;
    c->voice.pending |= kPendingPitch;
}

void Audio_Fx08_NoteOnVibrato(MusicChannel* c) {
    Audio_NoteOn(c);
    if (!(c->state & kStateNotePlaying)) return;
    if (c->event_flags & (kEventNote | kEventRetrigger)) c->vibrato_phase = 0;
    const u8 p = c->effect_param;
    if (p & 0xF) c->vibrato_depth = (4 * p) & 0x3F;
    if (p >> 4) c->vibrato_speed = p >> 4;
    c->tick_handler = Audio_VibratoUpdate;
    c->state |= kStateHandlerWhileNote;
    Audio_VibratoUpdate(c);
}

}  // namespace dbzlog2::audio
