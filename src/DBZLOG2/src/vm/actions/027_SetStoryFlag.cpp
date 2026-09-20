// BytecodeVM_SetStoryFlag -- action opcode 27, 0x08009710 (0x18 bytes). HIGH.
// Pops a story-flag id and sets it. (OpCodes.csv: StackSetPartyFlag.)
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetStoryFlag(VmContext* vm, const u8**) {
    PartyState_SetStoryFlag(&g_PartyState, Pop(vm));
}

}  // namespace dbzlog2::vm
