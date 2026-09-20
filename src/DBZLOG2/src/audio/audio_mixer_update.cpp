// Audio_MixerUpdate -- 0x08020BD8 (0x102 bytes). MEDIUM: transcribed literally; the ring-buffer constants come straight from the decompile and the
// meaning of the second destination (why it is +0x200 / +0x600) has not been re-derived. Refills the two 8-bit FIFO rings the DMA streams to the
// sound hardware: sequencer + music voices + SFX voices are summed into `accumulator`, then sub_080236F0 packs left / right into the rings
// (byte = clamp(sum >> 6, -128, 127); verified in an emulator).
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void SfxVoices_CommitPendingChanges(AudioVoice* voices);
void SfxVoices_Mix(AudioVoice* voices, s32* accumulator, int frames);
// sub_080236F0: dstA / dstB / accumulator / frames. Splits each accumulator word (left in the low half, right in the high half) into two bytes.
void PackFifoSamples(u8* dst_a, u8* dst_b, const s32* accumulator, int frames);

void Audio_MixerUpdate(AudioMixer* m) {
    m->busy = 1;
    const u32 pos = m->dma_position;
    u32 write = m->last_position;
    int pending = static_cast<int>((pos - write) & 0x3FF);
    if (pending >= 0x40) {
        m->last_position = pos;
        SfxVoices_CommitPendingChanges(m->sfx);
        do {
            int n = pending > 512 ? 512 : pending;
            Audio_SequencerTick(&m->music, m->accumulator, n);
            SfxVoices_Mix(m->sfx, m->accumulator, n);
            const u32 end = write + n;
            u8* const ring_a = reinterpret_cast<u8*>(kEwramBase);
            if (end <= 1024) {
                if (write >= 512)      PackFifoSamples(ring_a + write, reinterpret_cast<u8*>(kEwramBase + 0x200) + write, m->accumulator, n);
                else if (end <= 512)   PackFifoSamples(ring_a + write, reinterpret_cast<u8*>(kEwramBase + 0x600) + write, m->accumulator, n);
                else {
                    PackFifoSamples(ring_a + write, reinterpret_cast<u8*>(kEwramBase + 0x600) + write, m->accumulator, 512 - write);
                    PackFifoSamples(reinterpret_cast<u8*>(kEwramBase + 0x200), reinterpret_cast<u8*>(kEwramBase + 0x400), m->accumulator + (512 - write), end - 512);
                }
            } else {
                PackFifoSamples(ring_a + write, reinterpret_cast<u8*>(kEwramBase + 0x200) + write, m->accumulator, 1024 - write);
                PackFifoSamples(ring_a, reinterpret_cast<u8*>(kEwramBase + 0x600), m->accumulator + (1024 - write), end - 1024);
            }
            write = (write + n) & 0x3FF;
            pending -= n;
        } while (pending != 0);
    }
    m->busy = 0;
}

}  // namespace dbzlog2::audio
