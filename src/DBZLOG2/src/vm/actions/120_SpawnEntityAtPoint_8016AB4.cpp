// BytecodeVM_SpawnEntityAtPoint_8016AB4 -- action opcode 120, 0x0800AD3E (0x3C bytes). LOW.
// Pops (x, y), adds 15 to each and spawns an entity with sub_08016AB4 at that point into g_EntityList.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* PointEntity_Create(void* memory, const s32* xy);   // 0x08016AB4 (IDA: sub_8016AB4)

void BytecodeVM_SpawnEntityAtPoint_8016AB4(VmContext* vm, const u8**) {
    s32 xy[2];
    xy[1] = Pop(vm) + 15;
    xy[0] = Pop(vm) + 15;
    EntityList_Add(g_EntityList, PointEntity_Create(nullptr, xy));
}

}  // namespace dbzlog2::vm
