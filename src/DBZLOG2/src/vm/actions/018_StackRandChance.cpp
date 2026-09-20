// BytecodeVM_StackRandChance -- action opcode 18, 0x0800957A (0x18 bytes). HIGH.
// Replaces the top value p (a percentage, 0-100) with 1 with probability p percent, else 0. The threshold is p * 167773 / 256 (~ p * 2^32 / 100 / 256 * 256).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackRandChance(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = Random_Chance(static_cast<u32>((167773 * *top) >> 8));
}

}  // namespace dbzlog2::vm
