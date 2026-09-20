// BytecodeVM_SetEntityPositionInstant -- action opcode 62, 0x0800A44E (0x4C bytes). MEDIUM.
// Pops (character, x, y) and enqueues a command (vtable 0x08024DE4) whose run slot writes the entity's position words (+344 / +348) = x << 8, y << 8 (fixed point 24.8) between entity slots 12 / 13: an instantaneous teleport. Different from SetEntityPosition (144), which walks.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetEntityPositionInstant(VmContext* vm, const u8**) {
    const s32 b = Pop(vm);
    const s32 a = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(16));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08024DE4;
        command[2] = a;
        command[3] = b;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
