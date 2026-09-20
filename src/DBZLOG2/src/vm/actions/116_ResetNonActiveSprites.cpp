// BytecodeVM_ResetNonActiveSprites -- action opcode 116, 0x0800A794 (0xC bytes). HIGH.
// Calls PartyState_ResetTransformedSlots: every non-active slot returns to its base form.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void PartyState_ResetTransformedSlots(PartyState*);   // 0x08003ED4

void BytecodeVM_ResetNonActiveSprites(VmContext*, const u8**) {
    PartyState_ResetTransformedSlots(&g_PartyState);
}

}  // namespace dbzlog2::vm
