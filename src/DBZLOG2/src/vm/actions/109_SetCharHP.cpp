// BytecodeVM_SetCharHP -- action opcode 109, 0x0800ADF2 (0x40 bytes). HIGH.
// Pops (slot, hp) and sets the character's current HP clamped to [0, max HP].
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetCharHP(VmContext* vm, const u8**) {
    const s32 hp = Pop(vm);
    characters::CharacterEntry& c = g_PartyState.characters[Pop(vm)];
    if (c.max_hp >= hp) c.current_hp = (hp >= 0) ? static_cast<u16>(hp) : 0;
    else c.current_hp = c.max_hp;
}

}  // namespace dbzlog2::vm
