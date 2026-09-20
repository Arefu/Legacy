// BytecodeVM_PlayAudioVolume -- action opcode 111, 0x08009C22 (0x32 bytes). HIGH.
// Pops (sound-effect index, volume) and plays it at that volume, pan 0.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PlayAudioVolume(VmContext* vm, const u8**) {
    const s32 volume = Pop(vm);
    SFX_Queue(g_SFXPool, &g_SFXDefTable[Pop(vm)], static_cast<u8>(volume), 0);
}

}  // namespace dbzlog2::vm
