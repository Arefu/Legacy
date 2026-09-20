// BytecodeVM_WarpToMapNoEntities -- action opcode 39, 0x08009A88 (0x74 bytes). HIGH.
// Pops (zone, area, variation, x, y): despawns the player and loads the map WITHOUT entities and without a fade.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_WarpToMapNoEntities(VmContext* vm, const u8**) {
    s32 spawn[2];
    spawn[1] = Pop(vm);
    spawn[0] = Pop(vm);
    const s32 variation = Pop(vm);
    const s32 area = Pop(vm);
    const s32 zone = Pop(vm);
    if (g_PlayerObject != nullptr) CallSlotOfEntity(g_PlayerObject, 0, 10);
    g_PlayerWasDespawned = 1;
    Map_Load(&g_MapState, zone, area, spawn, variation, 0);
}

}  // namespace dbzlog2::vm
