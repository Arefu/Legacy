// BytecodeVM_SpawnEntityForChar_80104C0 -- action opcode 90, 0x0800AB44 (0x3E bytes). LOW.
// Pops (character, a, b) and spawns the same kind of entity as FadeIn (sub_080104C0(entity, a, b)) into g_EntityList. Candidate: an effect / projectile attached to a character.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* EffectAttached_Create(void* memory, entities::EntityHeader* target, s32 a, s32 b);   // 0x080104C0 (IDA: sub_80104C0)

void BytecodeVM_SpawnEntityForChar_80104C0(VmContext* vm, const u8**) {
    const s32 b = Pop(vm);
    const s32 a = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    EntityList_Add(g_EntityList, EffectAttached_Create(nullptr, entity, a, b));
}

}  // namespace dbzlog2::vm
