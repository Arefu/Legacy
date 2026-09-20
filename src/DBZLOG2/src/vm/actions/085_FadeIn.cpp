// BytecodeVM_FadeIn -- action opcode 85, 0x0800AB1A (0x2A bytes). MEDIUM.
// Pops a character and spawns a fade-in effect entity for it (sub_080104C0(entity, 0, 100)) into g_EntityList. Despite the name (kept from IDA) this is an entity effect, not a screen fade.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* EffectAttached_Create(void* memory, entities::EntityHeader* target, s32 a, s32 b);   // 0x080104C0 (IDA: sub_80104C0)

void BytecodeVM_FadeIn(VmContext* vm, const u8**) {
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    EntityList_Add(g_EntityList, EffectAttached_Create(nullptr, entity, 0, 100));
}

}  // namespace dbzlog2::vm
