// BytecodeVM_StackCmpNe -- main opcode 0x0C, 0x0800907C (0x1A bytes). HIGH.
// Compares: pushes 1 if a != b else 0 (a = second from top, b = top; the result replaces a and the stack shrinks by one).
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_StackCmpNe(VmContext* vm, const u8**) {
    vm->stack_count -= 1;
    s32* a = &vm->stack[vm->stack_count - 1];
    const s32 b = a[1];
    *a = (*a != b);
}

}  // namespace dbzlog2::vm
