// BytecodeVM_DrawBossHealthBarRange -- action opcode 90, 0x0800AB44 (0x3E bytes). MEDIUM.
// Pops (character, minPercent, maxPercent) and adds a boss health bar effect attached to that entity (EffectAttached_Create, 0x080104C0). Opcode 85
// (DrawBossHealthBar, 0x0800AB1A, was misnamed FadeIn) creates the same effect with the range fixed at (0, 100). The effect stores base = 60 * minPercent / 100 and
// range = 60 * (maxPercent - minPercent) / 100, and each frame draws a 60-step bar filled to base + range * currentHP / maxHP, where the entity's current
// HP is at +392 and its stat entry (at +384) holds the maximum at +4 -- exactly what CombatEntity_Create (map-placed enemies) sets up. The entity must be
// on screen (user-confirmed).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* EffectAttached_Create(void* memory, entities::EntityHeader* target, s32 min_percent, s32 max_percent);   // 0x080104C0 (IDA: BossHealthBar_Create)

void BytecodeVM_DrawBossHealthBarRange(VmContext* vm, const u8**) {
    const s32 max_percent = Pop(vm);
    const s32 min_percent = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    EntityList_Add(g_EntityList, EffectAttached_Create(nullptr, entity, min_percent, max_percent));
}

}  // namespace dbzlog2::vm
