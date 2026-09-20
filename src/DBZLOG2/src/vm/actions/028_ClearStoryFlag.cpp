// BytecodeVM_ClearStoryFlag -- action opcode 28, 0x08009728 (0x18 bytes). HIGH.
// Pops a story-flag id and clears it.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_ClearStoryFlag(VmContext* vm, const u8**) {
    PartyState_ClearStoryFlag(&g_PartyState, Pop(vm));
}

}  // namespace dbzlog2::vm
