// BytecodeVM_SetCharacterInPartyFlag -- action opcode 79, 0x0800A9B6 (0x24 bytes). HIGH.
// Pops a party slot and sets CharacterEntry.flags bit 0 = 'in the active party' (Character_GetActiveCount counts exactly this bit).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetCharacterInPartyFlag(VmContext* vm, const u8**) {
    characters::CharacterEntry& entry = g_PartyState.characters[Pop(vm)];
    entry.flags |= 1;
}

}  // namespace dbzlog2::vm
