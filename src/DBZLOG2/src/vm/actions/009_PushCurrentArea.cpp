// BytecodeVM_PushCurrentArea -- action opcode 9, 0x0800943E (0x14 bytes). HIGH.
// Pushes the current map's area number.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushCurrentArea(VmContext* vm, const u8**) {
    Push(vm, g_MapState.area);
}

}  // namespace dbzlog2::vm
