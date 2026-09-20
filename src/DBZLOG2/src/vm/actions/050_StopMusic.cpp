// BytecodeVM_StopMusic -- action opcode 50, 0x08009CBA (0xA bytes). HIGH.
// Stops the music (g_MusicState = 0).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern u8 g_MusicState;   // 0x030026CD

void BytecodeVM_StopMusic(VmContext*, const u8**) {
    g_MusicState = 0;
}

}  // namespace dbzlog2::vm
