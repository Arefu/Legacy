// BytecodeVM_RestoreMap -- action opcode 37, 0x08009992 (0x40 bytes). HIGH.
// Fades out, restores the map saved by SetMapRestoreFlag / LoadMapWithEntities (MapState_Restore, IDA sub_800C7C6), fades back in and restores the saved active entity + context.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern s32 g_SavedActiveEntityContext;   // 0x03001FC8
extern entities::EntityHeader* g_SavedActiveEntity;   // 0x03001FCC

void BytecodeVM_RestoreMap(VmContext*, const u8**) {
    GameLoop_ExecuteAndWait(FadeOut_Create(nullptr, 16));
    MapState_Restore(&g_MapState);
    u16* transition = static_cast<u16*>(ArenaAlloc(12));
    if (transition != nullptr) {
        *reinterpret_cast<u32*>(transition) = g_ScreenFadeVTable;
        reinterpret_cast<u32*>(transition)[2] = 16;
        reinterpret_cast<u32*>(transition)[1] = 0;
    }
    GameLoop_ExecuteAndWait(transition);
    g_ActiveEntity = g_SavedActiveEntity;
    g_ActiveEntityContext = reinterpret_cast<void*>(g_SavedActiveEntityContext);
}

}  // namespace dbzlog2::vm
