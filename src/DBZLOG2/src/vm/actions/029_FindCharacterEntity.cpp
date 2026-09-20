// BytecodeVM_FindCharacterEntity -- action opcode 29, 0x08009740 (0x1E bytes). HIGH.
// Pops a party slot and pushes... nothing: it returns the entity pointer in r0 (the value is not pushed; the script uses SetActiveEntity afterwards).
// Looks up entity type 4 with index 2 * slot + 1 in g_EntityList.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_FindCharacterEntity(VmContext* vm, const u8**) {
    const s32 slot = Pop(vm);
    EntityList_FindByTypeAndIndex(g_EntityList, 4, 2 * slot + 1);
}

}  // namespace dbzlog2::vm
