// BytecodeVM_LoadMapWithEntities -- action opcode 35, 0x08009852 (0xA0 bytes). HIGH.
// Pops (zone, area, variation, x, y): fades out, optionally saves the current map for restoring, loads the new map as a cutscene (with entities) at (x, y) and fades the new map in.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern s32 g_SavedActiveEntityContext;   // 0x03001FC8
extern entities::EntityHeader* g_SavedActiveEntity;   // 0x03001FCC

void BytecodeVM_LoadMapWithEntities(VmContext* vm, const u8**) {
    s32 spawn[2];
    spawn[1] = Pop(vm);
    spawn[0] = Pop(vm);
    const s32 variation = Pop(vm);
    const s32 area = Pop(vm);
    const s32 zone = Pop(vm);
    GameLoop_ExecuteAndWait(FadeOut_Create(nullptr, 64));
    if (g_MapRestoreFlag != 0) {
        MapState_SaveForRestore(&g_MapState);
        g_SavedActiveEntityContext = reinterpret_cast<intptr_t>(g_ActiveEntityContext);
        g_SavedActiveEntity = g_ActiveEntity;
        g_MapRestoreFlag = 0;
    }
    Map_LoadCutscene(&g_MapState, zone, area, variation, spawn, 1);
    u16* transition = static_cast<u16*>(ArenaAlloc(12));
    if (transition != nullptr) {
        *reinterpret_cast<u32*>(transition) = g_ScreenFadeVTable;       // 0x0802542C
        reinterpret_cast<u32*>(transition)[2] = 16;
        reinterpret_cast<u32*>(transition)[1] = 0;
    }
    GameLoop_ExecuteAndWait(transition);
}

}  // namespace dbzlog2::vm
