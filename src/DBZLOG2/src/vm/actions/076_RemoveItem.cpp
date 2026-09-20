// BytecodeVM_RemoveItem -- action opcode 76, 0x080096CA (0x1E bytes). HIGH.
// Pops an item id and decrements the party inventory count for it (no underflow check).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_RemoveItem(VmContext* vm, const u8**) {
    const s32 item = Pop(vm);
    g_PartyState.inventory[item] -= 1;
}

}  // namespace dbzlog2::vm
