// BytecodeVM_PushCurrentWorld -- action opcode 8, 0x0800942A (0x14 bytes). HIGH.
// Pushes the current map's zone number (IDA's older name: 'world').
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushCurrentWorld(VmContext* vm, const u8**) {
    Push(vm, g_MapState.zone);
}

}  // namespace dbzlog2::vm
