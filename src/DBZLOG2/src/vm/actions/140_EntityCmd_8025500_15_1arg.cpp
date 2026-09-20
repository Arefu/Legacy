// BytecodeVM_EntityCmd_8025500_15_1arg -- action opcode 140, 0x08009F3C (0x40 bytes). LOW.
// Pops (character, arg) and enqueues a command with vtable 0x0802553C (slot group 15 of the table at 0x08025500). Not decoded.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityCmd_8025500_15_1arg(VmContext* vm, const u8**) {
    const s32 arg = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(12));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x0802553C;
        command[2] = arg;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
