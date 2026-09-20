// BytecodeVM_EntityCmd_801094C_Link -- action opcode 89, 0x08009D94 (0x5C bytes). LOW.
// Pops 4 values: two character indices and two parameters; builds an entity command with sub_0801094C and enqueues it on the first character. Candidate: 'look at' or 'attach to' another character. Name kept until the constructor is decoded.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* EntityCmd_801094C_Create(void* memory, entities::EntityHeader* other, const s32* params);   // 0x0801094C

void BytecodeVM_EntityCmd_801094C_Link(VmContext* vm, const u8**) {
    s32 params[2];
    params[1] = Pop(vm);
    params[0] = Pop(vm);
    const s32 other_index = Pop(vm);
    entities::EntityHeader* target = Entity_GetByCharIndex(Pop(vm));
    entities::EntityHeader* other = Entity_GetByCharIndex(other_index);
    Entity_EnqueueCommand(g_CommandQueue, EntityCmd_801094C_Create(nullptr, other, params), target);
}

}  // namespace dbzlog2::vm
