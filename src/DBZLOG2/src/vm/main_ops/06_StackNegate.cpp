// BytecodeVM_StackNegate -- main opcode 0x06, 0x08009000 (0xE bytes). HIGH.
// Negates the top value.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_StackNegate(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = -*top;
}

}  // namespace dbzlog2::vm
