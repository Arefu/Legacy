// BytecodeVM_StackAnd -- main opcode 0x04, 0x08008FC6 (0x20 bytes). HIGH.
// Logical AND of the top two values (booleans 0 / 1).
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_StackAnd(VmContext* vm, const u8**) {
    vm->stack_count -= 1;
    s32* a = &vm->stack[vm->stack_count - 1];
    *a = (a[0] != 0 && a[1] != 0);
}

}  // namespace dbzlog2::vm
