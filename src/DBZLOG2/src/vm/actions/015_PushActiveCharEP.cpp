// BytecodeVM_PushActiveCharEP -- action opcode 15, 0x08009528 (0x20 bytes). HIGH.
// Pushes the active character's current EP.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushActiveCharEP(VmContext* vm, const u8**) {
    Push(vm, g_PartyState.characters[g_PartyState.active_character_index].current_ep);
}

}  // namespace dbzlog2::vm
