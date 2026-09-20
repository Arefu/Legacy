// BytecodeVM_StackRand -- action opcode 17, 0x08009568 (0x12 bytes). HIGH.
// Replaces the top value n with a random number in [0, n) (Random_Range).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackRand(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = static_cast<s32>(Random_Range(static_cast<u32>(*top)));
}

}  // namespace dbzlog2::vm
