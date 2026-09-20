// BytecodeVM_PushActiveCharMaxEP -- action opcode 16, 0x08009548 (0x20 bytes). HIGH.
// Pushes the active character's max EP.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushActiveCharMaxEP(VmContext* vm, const u8**) {
    Push(vm, g_PartyState.characters[g_PartyState.active_character_index].max_ep);
}

}  // namespace dbzlog2::vm
