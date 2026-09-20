// BytecodeVM_EntityWaitFrames -- action opcode 58, 0x0800A044 (0x40 bytes). MEDIUM.
// Pops (character, frames) and enqueues a command whose completion test is `--frames == 0`: makes that character wait N frames before its next queued command.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityWaitFrames(VmContext* vm, const u8**) {
    const s32 arg = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(12));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08024878;
        command[2] = arg;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
