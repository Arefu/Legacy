// BytecodeVM_SpawnCharacterEntity -- action opcode 51, 0x08009CC4 (0x3C bytes). HIGH.
// Pops (sprite id, x, y), fills the shared spawn record `rec` (0x03000C04) with them and creates a script-driven actor entity (MapScript_CreateCharacter), adding it to g_EntityList.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

struct SpawnRecord {   // 'rec', 0x03000C04 (the same layout MapScript_CreateCharacter reads from map data)
    u8 unknown[0x8];
    s16 x, y;          // +0x08 position (IDA: position.x / position.y)
    s32 sprite_id;     // +0x0C
};
extern SpawnRecord g_SpawnRecord;

void BytecodeVM_SpawnCharacterEntity(VmContext* vm, const u8**) {
    const s32 y = Pop(vm);
    const s32 x = Pop(vm);
    const s32 sprite = Pop(vm);
    g_SpawnRecord.x = static_cast<s16>(x);
    g_SpawnRecord.y = static_cast<s16>(y);
    g_SpawnRecord.sprite_id = sprite;
    EntityList_Add(g_EntityList, MapScript_CreateCharacter(&g_SpawnRecord));
}

}  // namespace dbzlog2::vm
