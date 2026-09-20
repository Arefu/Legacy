// BytecodeVM_EntityCmd_802521C_1arg -- action opcode 137, 0x0800A3A2 (0x46 bytes). LOW.
// Pops (character, n) and enqueues a 20-byte command (vtable 0x0802521C): [8] = 0, [0xC] = n, [0x10] = n - 8. Not decoded.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityCmd_802521C_1arg(VmContext* vm, const u8**) {
    const s32 n = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(20));
    if (command != nullptr) {
        command[0] = 0x0802521C;
        command[1] = 0;
        command[3] = n;
        command[2] = 0;
        command[4] = n - 8;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
