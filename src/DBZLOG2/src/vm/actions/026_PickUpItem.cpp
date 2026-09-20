// BytecodeVM_PickUpItem -- action opcode 26, 0x080096AA (0x20 bytes). HIGH.
// Pops an item id and runs that item's OnItemPickUp handler (g_ItemsInGame[id].on_item_pickup) with the id as its argument.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PickUpItem(VmContext* vm, const u8**) {
    const s32 item = Pop(vm);
    CallFunctionPointer(item, g_ItemsInGame[item].on_item_pickup);
}

}  // namespace dbzlog2::vm
