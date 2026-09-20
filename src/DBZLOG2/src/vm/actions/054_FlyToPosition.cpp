// BytecodeVM_FlyToPosition -- action opcode 54, 0x08009E30 (0x50 bytes). HIGH.
// Pops (character index, x, y): enqueues a direct (airborne) move to (x, y). The command is a 24-byte object with vtable 0x08024864 whose +0x10 / +0x14 hold the target.
// Opposite of WalkToPosition (opcode 53).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_FlyToPosition(VmContext* vm, const u8**) {
    const s32 y = Pop(vm);
    const s32 x = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(24));
    if (command != nullptr) {
        command[1] = 0;
        command[2] = 0;
        command[3] = 0;
        command[4] = x;
        command[5] = y;
        command[0] = 0x08024864;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
