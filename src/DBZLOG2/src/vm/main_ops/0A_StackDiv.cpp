// BytecodeVM_StackDiv -- main opcode 0x0A, 0x0800904A (0x18 bytes). HIGH.
// a / b (signed, via the BIOS-style _Div32 helper at 0x08021F30) (a = second from top, b = top; the result replaces a and the stack shrinks by one).
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

s32 Div32(s32 divisor, s32 dividend);   // 0x08021F30 (_Div32): returns dividend / divisor

void BytecodeVM_StackDiv(VmContext* vm, const u8**) {
    vm->stack_count -= 1;
    s32* a = &vm->stack[vm->stack_count - 1];
    const s32 b = a[1];
    *a = Div32(b, *a);   // _Div32(divisor, dividend) returns dividend / divisor: a / b
}

}  // namespace dbzlog2::vm
