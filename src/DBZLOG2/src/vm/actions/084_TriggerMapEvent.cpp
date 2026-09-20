// BytecodeVM_TriggerMapEvent -- action opcode 84, 0x0800AAAE (0x18 bytes). MEDIUM.
// Pops an event id and passes it to sub_0800C73A(&g_MapState, id) (fires a map-defined event / trigger). IDA still names the callee sub_800C73A.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void MapState_TriggerEvent(MapState*, s32 id);   // 0x0800C73A (IDA: sub_800C73A)

void BytecodeVM_TriggerMapEvent(VmContext* vm, const u8**) {
    MapState_TriggerEvent(&g_MapState, Pop(vm));
}

}  // namespace dbzlog2::vm
