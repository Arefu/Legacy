// BytecodeVM_SpawnItem -- action opcode 87, 0x0800ABA4 (0x32 bytes). MEDIUM.
// Pops an item id; if the entity list still has sprite-memory room (ObjVram_GetHighWater >= 1024) spawns that item entity (sub_08011802 with the shared spawn record at 0x03001FD0) into g_EntityList. Opcodes 87 and 92 are the same function.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

s32 ObjVram_GetHighWater(void* list);   // 0x08006D70
extern u8 g_ItemSpawnRecord[];          // 0x03001FD0
void* ItemEntity_Create(void* memory, s32 item, const void* record);   // 0x08011802 (IDA: sub_8011802)

void BytecodeVM_SpawnItem(VmContext* vm, const u8**) {
    const s32 item = Pop(vm);
    if (ObjVram_GetHighWater(g_EntityList) >= 1024) EntityList_Add(g_EntityList, ItemEntity_Create(nullptr, item, g_ItemSpawnRecord));
}

}  // namespace dbzlog2::vm
