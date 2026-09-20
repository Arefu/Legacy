// BytecodeVM_StopAllAudio -- action opcode 48, 0x08009C7A (0x10 bytes). HIGH.
// Stops every sound-effect voice (SFXPool_StopAll).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void SFXPool_StopAll(void* pool);   // 0x0801FC0C

void BytecodeVM_StopAllAudio(VmContext*, const u8**) {
    SFXPool_StopAll(g_SFXPool);
}

}  // namespace dbzlog2::vm
