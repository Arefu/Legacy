// BytecodeVM_SpawnEntity_801C4B0 -- action opcode 130, 0x0800AFD4 (0x14 bytes). LOW.
// Spawns the entity built by sub_0801C4B0 (no arguments) into g_EntityList. Not decoded.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* Entity_801C4B0_Create(void* memory);   // 0x0801C4B0 (IDA: sub_801C4B0)

void BytecodeVM_SpawnEntity_801C4B0(VmContext*, const u8**) {
    EntityList_Add(g_EntityList, Entity_801C4B0_Create(nullptr));
}

}  // namespace dbzlog2::vm
