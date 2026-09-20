// BytecodeVM_LoopOrJump -- main opcode 0x1C, 0x0800926C (0x2E bytes). HIGH.
// Decrement-and-branch-backward. Operand is 1 signed byte. The counter is the top of the alt stack: if it was 1 the loop is over (pop the alt stack, fall through); otherwise it is decremented and pc moves BACK by the operand.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_LoopOrJump(VmContext* vm, const u8** pc) {
    const s8 offset = static_cast<s8>(**pc);
    *pc += 1;
    s32* counter = &vm->alt_stack[vm->alt_count - 1];
    if ((*counter)-- == 1) vm->alt_count -= 1;
    else *pc -= offset;
}

}  // namespace dbzlog2::vm
