// BytecodeVM_SetBgLayerPriority -- action opcode 78, 0x08009688 (0x22 bytes). HIGH.
// Pops (layer, priority): stores the priority for that layer and rewrites its BGxCNT register.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetBgLayerPriority(VmContext* vm, const u8**) {
    const s32 priority = Pop(vm);
    const s32 layer = Pop(vm);
    MapRenderer_SetBgLayerPriority(g_MapRenderer, layer, priority);
}

}  // namespace dbzlog2::vm
