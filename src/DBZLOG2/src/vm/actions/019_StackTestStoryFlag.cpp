// BytecodeVM_StackTestStoryFlag -- action opcode 19, 0x080095B4 (0x16 bytes). HIGH.
// Replaces the top value (a story-flag id) with 1 if that flag is set, else 0. (OpCodes.csv called this StackTestQuestFlag.)
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackTestStoryFlag(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = PartyState_TestStoryFlag(&g_PartyState, *top);
}

}  // namespace dbzlog2::vm
