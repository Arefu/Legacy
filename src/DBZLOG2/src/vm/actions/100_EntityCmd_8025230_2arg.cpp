// BytecodeVM_EntityCmd_8025230_2arg -- action opcode 100, 0x0800A4DA (0x4C bytes). LOW.
// Pops (character, a, b) and enqueues a command (vtable 0x08025230) with payload (a, b). Candidate: scale or tint. Not decoded.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityCmd_8025230_2arg(VmContext* vm, const u8**) {
    const s32 b = Pop(vm);
    const s32 a = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(16));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025230;
        command[2] = a;
        command[3] = b;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
