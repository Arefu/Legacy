// BytecodeVM_StackPop -- main opcode 0x14, 0x08009128 (0x8 bytes). HIGH.
// Discards the top value.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_StackPop(VmContext* vm, const u8**) {
    vm->stack_count -= 1;
}

}  // namespace dbzlog2::vm
