// BytecodeVM_PlayMusic -- action opcode 49, 0x08009C8A (0x30 bytes). HIGH.
// Pops a track number and starts that song (g_MusicTrackTable[n], 20-byte entries at 0x081D4B2C) unless it is already playing.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern u8 g_MusicState;                 // 0x030026CD
extern const void* g_CurrentMusicTrack;  // 0x030026D4
extern u8 g_MusicChannel[];              // 0x030026C8 (the music player)
void Music_Init(void* player, const void* track);   // 0x08020A4C

void BytecodeVM_PlayMusic(VmContext* vm, const u8**) {
    const u8* track = reinterpret_cast<const u8*>(0x081D4B2C) + 20 * Pop(vm);
    if (g_MusicState != 1 || g_CurrentMusicTrack != track) Music_Init(g_MusicChannel, track);
}

}  // namespace dbzlog2::vm
