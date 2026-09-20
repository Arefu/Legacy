// BytecodeVM_StackGetCharMaxHP -- action opcode 107, 0x0800ADB6 (0x1E bytes). HIGH.
// Replaces the top value (a party slot) with that character's max hp.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackGetCharMaxHP(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = g_PartyState.characters[*top].max_hp;
}

}  // namespace dbzlog2::vm
