// BytecodeVM_PushActiveCharHP -- action opcode 13, 0x080094E8 (0x20 bytes). HIGH.
// Pushes the active character's current HP.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushActiveCharHP(VmContext* vm, const u8**) {
    Push(vm, g_PartyState.characters[g_PartyState.active_character_index].current_hp);
}

}  // namespace dbzlog2::vm
