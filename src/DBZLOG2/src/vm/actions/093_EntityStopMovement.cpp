// BytecodeVM_EntityStopMovement -- action opcode 93, 0x0800A49A (0x40 bytes). MEDIUM.
// Pops a character and enqueues the velocity command (vtable 0x08024BF0) with both payload words 0: stops the entity's movement.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityStopMovement(VmContext* vm, const u8**) {
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(16));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08024BF0;
        command[3] = 0;
        command[2] = 0;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
