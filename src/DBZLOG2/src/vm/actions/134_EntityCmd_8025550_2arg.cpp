// BytecodeVM_EntityCmd_8025550_2arg -- action opcode 134, 0x0800A276 (0x4C bytes). LOW.
// Pops (character, a, b) and enqueues a 16-byte command (vtable 0x08025550): [8] = a, [0xC] = b. Not decoded.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityCmd_8025550_2arg(VmContext* vm, const u8**) {
    const s32 b = Pop(vm);
    const s32 a = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(16));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025550;
        command[3] = b;
        command[2] = a;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
