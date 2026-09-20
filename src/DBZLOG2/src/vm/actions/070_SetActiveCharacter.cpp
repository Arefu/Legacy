// BytecodeVM_SetActiveCharacter -- action opcode 70, 0x0800A784 (0x10 bytes). HIGH.
// Same as SetActiveCharacterNoReset, then PartyState_ResetTransformedSlots (returns every non-active slot to its base form).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetActiveCharacterNoReset(VmContext*, const u8**);   // opcode 115

void PartyState_ResetTransformedSlots(PartyState*);   // 0x08003ED4

void BytecodeVM_SetActiveCharacter(VmContext* vm, const u8** pc) {
    BytecodeVM_SetActiveCharacterNoReset(vm, pc);
    PartyState_ResetTransformedSlots(&g_PartyState);
}

}  // namespace dbzlog2::vm
