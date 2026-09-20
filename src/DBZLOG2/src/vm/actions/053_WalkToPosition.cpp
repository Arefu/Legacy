// BytecodeVM_WalkToPosition -- action opcode 53, 0x08009D4A (0x4A bytes). HIGH.
// Pops (character index, x, y): enqueues a ground-pathing walk (constructor 0x08026620) on that character's entity.
// Opposite of FlyToPosition (opcode 54).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* WalkCommand_Create(void* memory, const s32* target_xy);   // 0x08026620 (IDA: sub_8026620)

void BytecodeVM_WalkToPosition(VmContext* vm, const u8**) {
    s32 target[2];
    target[1] = Pop(vm);
    target[0] = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    Entity_EnqueueCommand(g_CommandQueue, WalkCommand_Create(nullptr, target), entity);
}

}  // namespace dbzlog2::vm
