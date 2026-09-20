// BytecodeVM_JumpIfFalse -- main opcode 0x13, 0x0800910A (0x1E bytes). HIGH.
// Pops a condition; consumes the 1-byte signed operand; if the condition was 0, branches by the operand.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_JumpIfFalse(VmContext* vm, const u8** pc) {
    const s32 condition = Pop(vm);
    const s8 offset = static_cast<s8>(**pc);
    *pc += 1;
    if (condition == 0) *pc += offset;
}

}  // namespace dbzlog2::vm
