// BytecodeVM_DrawBossHealthBar -- action opcode 85, 0x0800AB1A (0x2A bytes). MEDIUM.
// Pops a character and adds the boss health bar effect for it (BossHealthBar_Create(entity, 0, 100)) to g_EntityList. It was named FadeIn before; the
// user confirmed in-game that it drops the boss health bar in at the top right (FadeIn(48) at the start of Zone 2 Area 8's boss fight). Opcode 90
// (DrawBossHealthBarRange) is the same effect with a custom percentage range.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* EffectAttached_Create(void* memory, entities::EntityHeader* target, s32 min_percent, s32 max_percent);   // 0x080104C0 (IDA: BossHealthBar_Create)

void BytecodeVM_DrawBossHealthBar(VmContext* vm, const u8**) {
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    EntityList_Add(g_EntityList, EffectAttached_Create(nullptr, entity, 0, 100));
}

}  // namespace dbzlog2::vm
