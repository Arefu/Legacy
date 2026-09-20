// BytecodeVM_StackAdd -- main opcode 0x07, 0x0800900E (0x14 bytes). HIGH.
// a + b (a = second from top, b = top; the result replaces a and the stack shrinks by one).
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_StackAdd(VmContext* vm, const u8**) {
    vm->stack_count -= 1;
    s32* a = &vm->stack[vm->stack_count - 1];
    const s32 b = a[1];
    *a += b;
}

}  // namespace dbzlog2::vm
