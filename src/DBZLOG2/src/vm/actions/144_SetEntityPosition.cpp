// BytecodeVM_SetEntityPosition -- action opcode 144, 0x08009EBA (0x42 bytes). MEDIUM.
// Pops (character, packed position) where the position is (x << 16) | y, unpacks it and enqueues a WALK command (constructor 0x08026620, the same one WalkToPosition uses) on that entity.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* WalkCommand_Create(void* memory, const s32* target_xy);   // 0x08026620

void BytecodeVM_SetEntityPosition(VmContext* vm, const u8**) {
    const s32 packed = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    s32 target[2];
    target[1] = packed;
    target[0] = packed >> 16;
    Entity_EnqueueCommand(g_CommandQueue, WalkCommand_Create(nullptr, target), entity);
}

}  // namespace dbzlog2::vm
