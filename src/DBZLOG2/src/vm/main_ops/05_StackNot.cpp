// BytecodeVM_StackNot -- main opcode 0x05, 0x08008FE6 (0x1A bytes). HIGH.
// Logical NOT of the top value.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_StackNot(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = (*top == 0);
}

}  // namespace dbzlog2::vm
