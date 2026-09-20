// BytecodeVM_ClearCharacterFlag_Bit1 -- action opcode 81, 0x0800AA34 (0x24 bytes). MEDIUM.
// Pops a party slot and clears flags bit 1.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_ClearCharacterFlag_Bit1(VmContext* vm, const u8**) {
    characters::CharacterEntry& entry = g_PartyState.characters[Pop(vm)];
    entry.flags &= ~2u;
}

}  // namespace dbzlog2::vm
