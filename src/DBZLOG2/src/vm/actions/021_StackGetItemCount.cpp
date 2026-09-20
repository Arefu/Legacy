// BytecodeVM_StackGetItemCount -- action opcode 21, 0x080095E4 (0x24 bytes). HIGH.
// Replaces the top value (an item id) with how many of that item the party has: the inventory count plus the number of matching item entities lying in the current map (type-23 collision handlers).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_StackGetItemCount(VmContext* vm, const u8**) {
    s32* top = &vm->stack[vm->stack_count - 1];
    const s32 in_inventory = g_PartyState.inventory[*top];
    *top = in_inventory + EntityList_CountCollisionHandlers(g_EntityList, 23, *top);
}

}  // namespace dbzlog2::vm
