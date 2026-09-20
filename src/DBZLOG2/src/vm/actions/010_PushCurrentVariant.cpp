// BytecodeVM_PushCurrentVariant -- action opcode 10, 0x08009452 (0x14 bytes). HIGH.
// Pushes the current map's variation number.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushCurrentVariant(VmContext* vm, const u8**) {
    Push(vm, g_MapState.variant);
}

}  // namespace dbzlog2::vm
