// BytecodeVM_StackTestPartyMemberFlag -- action opcode 23, 0x08009628 (0x30 bytes). MEDIUM.
// Pops two values, tests PartyState_TestMemberFlag(&g_PartyState, first_pushed, second_pushed), pushes the result. Which is the member index and which is the flag id is not confirmed.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackTestPartyMemberFlag(VmContext* vm, const u8**) {
    const s32 second = Pop(vm);
    const s32 first = Pop(vm);
    Push(vm, PartyState_TestMemberFlag(&g_PartyState, first, second));
}

}  // namespace dbzlog2::vm
