// BytecodeVM_SetCharacterFlag_Bit1 -- action opcode 80, 0x0800AA10 (0x24 bytes). MEDIUM.
// Pops a party slot and sets flags bit 1 (meaning not decoded).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetCharacterFlag_Bit1(VmContext* vm, const u8**) {
    characters::CharacterEntry& entry = g_PartyState.characters[Pop(vm)];
    entry.flags |= 2;
}

}  // namespace dbzlog2::vm
