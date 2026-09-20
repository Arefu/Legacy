// BytecodeVM_EnableLayer -- action opcode 25, 0x08009670 (0x18 bytes). HIGH.
// Pops a background layer number and shows that layer.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EnableLayer(VmContext* vm, const u8**) {
    GBA_EnableDisplayLayer(g_MapRenderer, Pop(vm));
}

}  // namespace dbzlog2::vm
