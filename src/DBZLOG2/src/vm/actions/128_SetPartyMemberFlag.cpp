// BytecodeVM_SetPartyMemberFlag -- action opcode 128, 0x0800AFA0 (0x22 bytes). MEDIUM.
// Pops (a, b) and calls PartyState_SetMemberFlag(&g_PartyState, a, b): the setter paired with StackTestPartyMemberFlag (23).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void PartyState_SetMemberFlag(PartyState*, s32 a, s32 b);   // 0x080040EE

void BytecodeVM_SetPartyMemberFlag(VmContext* vm, const u8**) {
    const s32 b = Pop(vm);
    const s32 a = Pop(vm);
    PartyState_SetMemberFlag(&g_PartyState, a, b);
}

}  // namespace dbzlog2::vm
