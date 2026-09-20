// BytecodeVM_StackGetCharMaxEP -- action opcode 108, 0x0800ADD4 (0x1E bytes). HIGH.
// Replaces the top value (a party slot) with that character's max ep.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackGetCharMaxEP(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = g_PartyState.characters[*top].max_ep;
}

}  // namespace dbzlog2::vm
