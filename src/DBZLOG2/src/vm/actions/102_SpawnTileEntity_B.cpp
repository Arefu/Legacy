// BytecodeVM_SpawnTileEntity_B -- action opcode 102, 0x0800ACAC (0x3E bytes). LOW.
// Sibling of SpawnTileEntity_A: values reduced by 3, table 0x083B5CE8.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* TileEntity_Create(void* memory, const s32* xy, const void* table);   // 0x08012A68 (IDA: sub_8012A68)
extern const u8 g_TileEntityTableB[];   // 0x083B5CE8

void BytecodeVM_SpawnTileEntity_B(VmContext* vm, const u8**) {
    s32 xy[2];
    xy[1] = Pop(vm) - 3;
    xy[0] = Pop(vm) - 3;
    EntityList_Add(g_EntityList, TileEntity_Create(nullptr, xy, g_TileEntityTableB));
}

}  // namespace dbzlog2::vm
