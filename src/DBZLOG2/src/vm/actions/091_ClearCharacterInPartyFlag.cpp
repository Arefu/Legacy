// BytecodeVM_ClearCharacterInPartyFlag -- action opcode 91, 0x0800A9DA (0x24 bytes). HIGH.
// Pops a party slot and clears flags bit 0: removes the character from the active party.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_ClearCharacterInPartyFlag(VmContext* vm, const u8**) {
    characters::CharacterEntry& entry = g_PartyState.characters[Pop(vm)];
    entry.flags &= ~1u;
}

}  // namespace dbzlog2::vm
