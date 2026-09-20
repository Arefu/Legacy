// BytecodeVM_SpawnTileEntity_A -- action opcode 101, 0x0800AC5C (0x3E bytes). LOW.
// Pops two values (each reduced by 1) and spawns an entity with factory sub_08012A68 using the table at 0x083B5CA8; SpawnTileEntity_B uses the table at 0x083B5CE8 with each value reduced by 3. Entity class unknown.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* TileEntity_Create(void* memory, const s32* xy, const void* table);   // 0x08012A68 (IDA: sub_8012A68)
extern const u8 g_TileEntityTableA[];   // 0x083B5CA8

void BytecodeVM_SpawnTileEntity_A(VmContext* vm, const u8**) {
    s32 xy[2];
    xy[1] = Pop(vm) - 1;
    xy[0] = Pop(vm) - 1;
    EntityList_Add(g_EntityList, TileEntity_Create(nullptr, xy, g_TileEntityTableA));
}

}  // namespace dbzlog2::vm
