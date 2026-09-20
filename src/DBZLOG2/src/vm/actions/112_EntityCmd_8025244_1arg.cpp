// BytecodeVM_EntityCmd_8025244_1arg -- action opcode 112, 0x0800AE76 (0x42 bytes). LOW.
// Pops (character, arg) and enqueues a 12-byte command with vtable 0x08025244, arg at +8. Not decoded.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityCmd_8025244_1arg(VmContext* vm, const u8**) {
    const s32 arg = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(12));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025244;
        command[2] = arg;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
