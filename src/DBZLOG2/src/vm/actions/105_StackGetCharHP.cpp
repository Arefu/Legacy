// BytecodeVM_StackGetCharHP -- action opcode 105, 0x0800AD7A (0x1E bytes). HIGH.
// Replaces the top value (a party slot) with that character's current hp.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackGetCharHP(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = g_PartyState.characters[*top].current_hp;
}

}  // namespace dbzlog2::vm
