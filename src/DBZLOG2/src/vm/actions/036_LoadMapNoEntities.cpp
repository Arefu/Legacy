// BytecodeVM_LoadMapNoEntities -- action opcode 36, 0x080098F2 (0xA0 bytes). HIGH.
// Pops (zone, area, variation, x, y). Same as LoadMapWithEntities but Map_LoadCutscene is called with flag 0 (no entities).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern s32 g_SavedActiveEntityContext;   // 0x03001FC8
extern entities::EntityHeader* g_SavedActiveEntity;   // 0x03001FCC

void BytecodeVM_LoadMapNoEntities(VmContext* vm, const u8**) {
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
    Map_LoadCutscene(&g_MapState, zone, area, variation, spawn, 0);
    GameLoop_ExecuteAndWait(NewCommand12(kVTableScreenFade, 0, 16));
}

}  // namespace dbzlog2::vm
