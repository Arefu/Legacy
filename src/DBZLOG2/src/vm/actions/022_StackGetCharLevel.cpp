// BytecodeVM_StackGetCharLevel -- action opcode 22, 0x08009608 (0x20 bytes). HIGH.
// Replaces the top value (a party slot) with that character's level.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackGetCharLevel(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    *top = static_cast<s32>(CharStats_GetLevel(&g_PartyState.characters[*top]));
}

}  // namespace dbzlog2::vm
