// BytecodeVM_StackGetCharEP -- action opcode 106, 0x0800AD98 (0x1E bytes). HIGH.
// Replaces the top value (a party slot) with that character's current ep.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackGetCharEP(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = g_PartyState.characters[*top].current_ep;
}

}  // namespace dbzlog2::vm
