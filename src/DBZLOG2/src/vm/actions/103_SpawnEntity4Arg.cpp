// BytecodeVM_SpawnEntity4Arg -- action opcode 103, 0x0800ACEA (0x54 bytes). LOW.
// Pops 4 raw parameters (in push order p0..p3) and spawns an entity with sub_08012B38(params) into g_EntityList. Candidate: a generic custom map-entity spawn.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* GenericEntity_Create(void* memory, const s32* params);   // 0x08012B38 (IDA: sub_8012B38)

void BytecodeVM_SpawnEntity4Arg(VmContext* vm, const u8**) {
    s32 params[4];
    params[3] = Pop(vm);
    params[2] = Pop(vm);
    params[1] = Pop(vm);
    params[0] = Pop(vm);
    EntityList_Add(g_EntityList, GenericEntity_Create(nullptr, params));
}

}  // namespace dbzlog2::vm
