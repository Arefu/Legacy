// BytecodeVM_SpawnMapEntity -- action opcode 127, 0x0800ABD6 (0x2A bytes). MEDIUM.
// Pops a graphic-object id; if there is sprite-memory room (ObjVram_GetHighWater >= 1024) asks the map state to spawn it (MapState_SpawnGraphicObject with the shared record at 0x03001FD0).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

s32 ObjVram_GetHighWater(void* list);   // 0x08006D70
extern u8 g_ItemSpawnRecord[];          // 0x03001FD0
void MapState_SpawnGraphicObject(MapState*, s32 id, const void* record);   // 0x0800C718

void BytecodeVM_SpawnMapEntity(VmContext* vm, const u8**) {
    const s32 id = Pop(vm);
    if (ObjVram_GetHighWater(g_EntityList) >= 1024) MapState_SpawnGraphicObject(&g_MapState, id, g_ItemSpawnRecord);
}

}  // namespace dbzlog2::vm
