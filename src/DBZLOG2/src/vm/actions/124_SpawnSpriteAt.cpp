// BytecodeVM_SpawnSpriteAt -- action opcode 124, 0x0800AEF0 (0x4A bytes). MEDIUM.
// Pops (sprite id, x, y), resolves the sprite record with Character_GetSpriteRecord and spawns a plain positioned sprite entity (no behaviour list) into g_EntityList: a script-placed decoration / prop.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

RomPtr Character_GetSpriteRecord(s32 id);   // 0x08009324
void* SpriteEntity_CreateAt(void* memory, RomPtr sprite_record, const s32* xy);   // 0x08019F5C

void BytecodeVM_SpawnSpriteAt(VmContext* vm, const u8**) {
    s32 xy[2];
    xy[1] = Pop(vm);
    xy[0] = Pop(vm);
    const RomPtr record = Character_GetSpriteRecord(Pop(vm));
    EntityList_Add(g_EntityList, SpriteEntity_CreateAt(nullptr, record, xy));
}

}  // namespace dbzlog2::vm
