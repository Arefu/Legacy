// BytecodeVM_PushActiveCharName -- action opcode 12, 0x080094B0 (0x1C bytes). HIGH.
// Pushes a POINTER to the active character's name string (g_StringTable[48 + slot]).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushActiveCharName(VmContext* vm, const u8**) {
    Push(vm, reinterpret_cast<s32>(g_StringTable[g_PartyState.active_character_index + 48]));
}

}  // namespace dbzlog2::vm
