// BytecodeVM_WarpToMapWithEntities -- action opcode 38, 0x080099D2 (0xB6 bytes). HIGH.
// Pops (zone, area, variation, x, y): despawns the player, fades out, loads the map (with entities) at (x, y), tells the new player entity to start (slot 10, arg 38), fades in, then starts it (arg 0).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern RomPtr g_WarpTransitionVTable;   // 0x08023990

void BytecodeVM_WarpToMapWithEntities(VmContext* vm, const u8**) {
    s32 spawn[2];
    spawn[1] = Pop(vm);
    spawn[0] = Pop(vm);
    const s32 variation = Pop(vm);
    const s32 area = Pop(vm);
    const s32 zone = Pop(vm);
    g_PlayerWasDespawned = 1;
    if (g_PlayerObject != nullptr) CallSlotOfEntity(g_PlayerObject, 0, 10);
    GameLoop_ExecuteAndWait(FadeOut_Create(nullptr, 64));
    entities::EntityHeader* player = Map_Load(&g_MapState, zone, area, spawn, variation, 1);
    CallSlotOfEntity(player, 38, 10);
    GameLoop_ExecuteAndWait(NewCommand12(kVTableWarpTransition, 0, 4));
    CallSlotOfEntity(player, 0, 10);
}

}  // namespace dbzlog2::vm
