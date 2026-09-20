// Random_Next / Random_Range / Random_Chance (IDA: GetRandom 0x080213FC, GetRandomRange 0x0802141C, RandomChance 0x0802142E). HIGH.
// A tiny two-word generator; every "random" in the game (NPC wandering, enemy AI, SFX variants) comes from here. The state is in IWRAM.
#include "dbzlog2/gba_io.h"
#include "dbzlog2/gba_memory.h"
#include "dbzlog2/types.h"

namespace dbzlog2::system {

static u32 g_RandomState0;   // 0x03003890
static u32 g_RandomState1;   // 0x03003894

u32 Random_Next() {
    const u32 s0 = (g_RandomState0 + 0x02CBBC71u) ^ g_RandomState1;
    g_RandomState1 = (((g_RandomState1 >> 29) | (g_RandomState1 << 3)) + 0x0632D80Fu) ^ s0;
    g_RandomState0 = s0;
    return s0 - g_RandomState1;
}

// Uniform-ish value in [0, max): the high 16 bits of Random_Next() * max.
u32 Random_Range(u32 max) { return (Random_Next() * max) >> 16; }

// True with probability threshold / 2^32.
bool Random_Chance(u32 threshold) { return Random_Next() < threshold; }

}  // namespace dbzlog2::system
