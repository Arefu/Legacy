// BytecodeVM_PushAccumulator -- action opcode 0, 0x08009392 (0x12 bytes). HIGH.
// Pushes the VM accumulator (g_VMAccumulator, 0x03001FB0).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushAccumulator(VmContext* vm, const u8**) {
    Push(vm, g_VMAccumulator);
}

}  // namespace dbzlog2::vm
