// BytecodeVM_PushEntityPosition -- action opcode 143, 0x08009E80 (0x3A bytes). HIGH.
// Pops a character index and pushes that entity's position packed as (x << 16) | (y & 0xFFFF).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void Entity_GetPosition(s32 out_xy[2], entities::EntityHeader* entity);   // 0x0800791A

void BytecodeVM_PushEntityPosition(VmContext* vm, const u8**) {
    s32 position[2];
    Entity_GetPosition(position, Entity_GetByCharIndex(Pop(vm)));
    Push(vm, static_cast<s32>((static_cast<u32>(position[0]) << 16) | (static_cast<u32>(position[1]) & 0xFFFF)));
}

}  // namespace dbzlog2::vm
