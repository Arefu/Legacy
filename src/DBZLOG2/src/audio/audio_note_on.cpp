// Audio_NoteOn -- 0x0801FE20, 0x90 bytes. Every effect handler except portamento starts by calling this. HIGH.
// Applies the row's note / volume flags to the channel's voice.
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Audio_NoteOn(MusicChannel* c) {
    const u8 flags = c->event_flags;
    if (flags & (kEventNote | kEventRetrigger)) {                 // (flags & 0x11): note trigger
        if (c->note == kNoteOff) {
            c->state &= static_cast<u8>(~kStateNotePlaying);      // note off cuts the voice immediately
            c->voice.pending &= static_cast<u8>(~kPendingActive);
            return;
        }
        c->volume_now = c->event_volume;
        const u32 frequency = g_NoteFrequencyTable[c->note];
        c->frequency_base = frequency;
        c->state = kStateNotePlaying;                             // clears the per-tick handler bits too
        c->voice.volume = c->volume_now;
        if (flags & kEventLegato) {                               // keep the current sample, just restart pitch + volume
            c->voice.frequency = frequency;
            c->voice.pending |= kPendingActive | kPendingPitch | kPendingRestart | kPendingVolume;   // 0x17
        } else {
            c->voice.instrument = g_CurrentMusicTrack->instrument_bank + sizeof(SampleInstrument) * c->instrument_index;
            c->voice.frequency = frequency;
            c->voice.pending |= kPendingActive | kPendingPitch | kPendingRestart | kPendingInstrument | kPendingVolume;   // 0x1F
        }
    } else if (flags & (kEventVolume | kEventVolumeStored)) {     // (flags & 0x44): volume only
        c->volume_now = c->event_volume;
        c->voice.volume = c->volume_now;
        c->voice.pending |= kPendingVolume;
    }
}

}  // namespace dbzlog2::audio
