// BytecodeVM_SetCharacterFlag_Bit5 -- action opcode 82, 0x0800AA58 (0x24 bytes). MEDIUM.
// Pops a party slot and sets flags bit 5 (meaning not decoded).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetCharacterFlag_Bit5(VmContext* vm, const u8**) {
    characters::CharacterEntry& entry = g_PartyState.characters[Pop(vm)];
    entry.flags |= 0x20;
}

}  // namespace dbzlog2::vm
