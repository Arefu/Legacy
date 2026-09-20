// BytecodeVM_EntityCmd_8024C04_3arg -- action opcode 136, 0x0800A346 (0x5C bytes). LOW.
// Pops (character, a, b, c) and enqueues a 28-byte command (vtable 0x08024C04): [8] = 0, [0xC] = 0, [0x10] = a, [0x14] = b, [0x18] = c. Not decoded.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityCmd_8024C04_3arg(VmContext* vm, const u8**) {
    const s32 c = Pop(vm);
    const s32 b = Pop(vm);
    const s32 a = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(28));
    if (command != nullptr) {
        command[1] = 0;
        command[2] = 0;
        command[3] = 0;
        command[4] = a;
        command[5] = b;
        command[0] = 0x08024C04;
        command[6] = c;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
