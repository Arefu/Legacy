// BytecodeVM_PushActiveCharNameUpper -- action opcode 75, 0x080094CC (0x1C bytes). HIGH.
// Pushes a POINTER to the active character's upper-case name string: g_StringTable[active + 54] (PushActiveCharName uses index + 48).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushActiveCharNameUpper(VmContext* vm, const u8**) {
    Push(vm, reinterpret_cast<s32>(g_StringTable[g_PartyState.active_character_index + 54]));
}

}  // namespace dbzlog2::vm
