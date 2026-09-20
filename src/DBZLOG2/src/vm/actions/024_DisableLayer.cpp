// BytecodeVM_DisableLayer -- action opcode 24, 0x08009658 (0x18 bytes). HIGH.
// Pops a background layer number and hides that layer (GBA_DisableDisplayLayer).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_DisableLayer(VmContext* vm, const u8**) {
    GBA_DisableDisplayLayer(g_MapRenderer, Pop(vm));
}

}  // namespace dbzlog2::vm
