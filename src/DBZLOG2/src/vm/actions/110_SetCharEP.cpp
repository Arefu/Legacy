// BytecodeVM_SetCharEP -- action opcode 110, 0x0800AE32 (0x44 bytes). HIGH.
// Pops (slot, ep) and sets the character's current EP clamped to [0, max EP].
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetCharEP(VmContext* vm, const u8**) {
    const s32 ep = Pop(vm);
    characters::CharacterEntry& c = g_PartyState.characters[Pop(vm)];
    if (c.max_ep >= ep) c.current_ep = (ep >= 0) ? static_cast<s16>(ep) : 0;
    else c.current_ep = static_cast<s16>(c.max_ep);
}

}  // namespace dbzlog2::vm
