// BytecodeVM_PushActiveCharIsTransformed -- action opcode 126, 0x0800AF72 (0x2E bytes). HIGH.
// Pushes 1 if the active character's current display row is flagged 'transformed' (row.flags bit 0), else 0.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern characters::CharacterDisplayData g_CharacterDisplayData[];   // 0x081D967C

void BytecodeVM_PushActiveCharIsTransformed(VmContext* vm, const u8**) {
    const characters::CharacterEntry& c = g_PartyState.characters[g_PartyState.active_character_index];
    Push(vm, g_CharacterDisplayData[c.display_data_index].flags & 1);
}

}  // namespace dbzlog2::vm
