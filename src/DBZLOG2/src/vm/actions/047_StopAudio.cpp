// BytecodeVM_StopAudio -- action opcode 47, 0x08009C54 (0x26 bytes). HIGH.
// Pops a sound-effect index and stops every voice playing g_SFXDefTable[n] (SFXPool_StopByResource).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void SFXPool_StopByResource(void* pool, const SfxSample* resource);   // 0x0801FC22

void BytecodeVM_StopAudio(VmContext* vm, const u8**) {
    SFXPool_StopByResource(g_SFXPool, &g_SFXDefTable[Pop(vm)]);
}

}  // namespace dbzlog2::vm
