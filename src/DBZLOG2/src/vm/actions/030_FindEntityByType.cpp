// BytecodeVM_FindEntityByType -- action opcode 30, 0x0800975E (0x1C bytes). HIGH.
// Pops an index and looks up entity type 4 with index 2 * value in g_EntityList (result in r0, not pushed).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_FindEntityByType(VmContext* vm, const u8**) {
    const s32 index = Pop(vm);
    EntityList_FindByTypeAndIndex(g_EntityList, 4, 2 * index);
}

}  // namespace dbzlog2::vm
