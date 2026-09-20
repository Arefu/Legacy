// BytecodeVM_PlayAudio -- action opcode 46, 0x08009BF8 (0x2A bytes). HIGH.
// Pops a sound-effect index and plays g_SFXDefTable[n] at volume 64, pan 0 (SFX_Queue).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PlayAudio(VmContext* vm, const u8**) {
    SFX_Queue(g_SFXPool, &g_SFXDefTable[Pop(vm)], 64, 0);
}

}  // namespace dbzlog2::vm
