// BytecodeVM_StackSub -- main opcode 0x08, 0x08009022 (0x14 bytes). HIGH.
// a - b (a = second from top, b = top; the result replaces a and the stack shrinks by one).
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_StackSub(VmContext* vm, const u8**) {
    vm->stack_count -= 1;
    s32* a = &vm->stack[vm->stack_count - 1];
    const s32 b = a[1];
    *a -= b;
}

}  // namespace dbzlog2::vm
