// BytecodeVM_StackTestStoryFlagNot -- action opcode 20, 0x080095CA (0x1A bytes). HIGH.
// Like StackTestStoryFlag but pushes 1 when the flag is CLEAR.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackTestStoryFlagNot(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = 1 - PartyState_TestStoryFlag(&g_PartyState, *top);
}

}  // namespace dbzlog2::vm
