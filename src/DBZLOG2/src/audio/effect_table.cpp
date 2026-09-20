// Audio_EffectTable -- 0x087FCB1C. HIGH. Effect id -> handler. Ids without their own handler run Audio_NoteOn.
// Effects 11, 12 and 21 are not used by any song. 27 = note delay.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_Fx12_PortamentoEnvelope(MusicChannel*);      // 0x080203A8  TODO(decompile)

const EffectHandler g_AudioEffectTable[32] = {
    Audio_NoteOn, Audio_Fx01_SetSpeed, Audio_Fx02_Unknown, Audio_NoteOn,                                      //  0.. 3
    Audio_Fx04_NoteOnEnvelope, Audio_Fx05_NoteOnSlideDown, Audio_Fx06_NoteOnSlideUp, Audio_Fx07_Portamento,   //  4.. 7
    Audio_Fx08_NoteOnVibrato, Audio_NoteOn, Audio_NoteOn, Audio_Fx11_NoteOnEnvelopeVibrato,                    //  8..11
    Audio_Fx12_PortamentoEnvelope, Audio_NoteOn, Audio_NoteOn, Audio_Fx15_SampleOffset,                        // 12..15
    Audio_NoteOn, Audio_Fx17_NoteOnRetrigger, Audio_NoteOn, Audio_NoteOn,                                      // 16..19
    Audio_Fx20_SetTempo, Audio_Fx21_SetVibrato, Audio_NoteOn, Audio_NoteOn,                                    // 20..23
    Audio_Fx24_SetPanning, Audio_NoteOn, Audio_NoteOn, Audio_Fx27_NoteDelay,                                     // 24..27
    Audio_NoteOn, Audio_NoteOn, Audio_NoteOn, Audio_NoteOn,                                                    // 28..31
};

}  // namespace dbzlog2::audio
