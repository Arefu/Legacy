// BytecodeVM_RemoveItems -- action opcode 77, 0x080096E8 (0x28 bytes). HIGH.
// Pops (item id, count) and subtracts count from the party inventory count for that item.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_RemoveItems(VmContext* vm, const u8**) {
    const s32 count = Pop(vm);
    const s32 item = Pop(vm);
    g_PartyState.inventory[item] -= static_cast<u8>(count);
}

}  // namespace dbzlog2::vm
