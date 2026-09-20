// BytecodeVM_SetEntityAnimation -- action opcode 57, 0x08009FC4 (0x40 bytes). HIGH.
// Pops (character, animation id) and enqueues a 'play animation' command (vtable 0x08025BAC).
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetEntityAnimation(VmContext* vm, const u8**) {
    const s32 arg = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(12));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025BAC;
        command[2] = arg;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
