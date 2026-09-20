// BytecodeVM_EntityWaitTouchPlayer -- action opcode 60, 0x0800A09A (0x34 bytes). LOW.
// Pops a character and enqueues an 8-byte command (vtable 0x08025258). Its completion slot tests whether the entity's directional hit rectangle overlaps an entity of list-type 17: probably 'wait until this character touches a type-17 entity'.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityWaitTouchPlayer(VmContext* vm, const u8**) {
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(8));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025258;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
