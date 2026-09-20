// BytecodeVM_ReloadMap -- action opcode 74, 0x0800A998 (0x1E bytes). HIGH.
// Re-initialises the current map's triggers (Map_InitTriggers(map, isNewMap = false)); if the map state's flag word at 0x0300206C is non-zero the player is marked as despawned first.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern u32 g_MapStateWord206C;   // 0x0300206C (IDA: unk_300206C; inside/next to g_MapState)  TODO(unknown)
void Map_InitTriggers(MapState*, bool is_new_map);   // 0x0800C2F8

void BytecodeVM_ReloadMap(VmContext*, const u8**) {
    if (g_MapStateWord206C != 0) g_PlayerWasDespawned = 1;
    Map_InitTriggers(&g_MapState, false);
}

}  // namespace dbzlog2::vm
