// BytecodeVM_PushActiveCharMaxHP -- action opcode 14, 0x08009508 (0x20 bytes). HIGH.
// Pushes the active character's max HP.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushActiveCharMaxHP(VmContext* vm, const u8**) {
    Push(vm, g_PartyState.characters[g_PartyState.active_character_index].max_hp);
}

}  // namespace dbzlog2::vm
