// BytecodeVM_EntityCmd_8024A50_Arc -- action opcode 99, 0x0800A210 (0x66 bytes). LOW.
// Pops 4 values (character, a, b, c) and enqueues a 32-byte command (vtable 0x08024A50): [8] = a, [0xC] = b, [0x10] = c * 1966, [0x14] = 32, [0x18] = 64. The most complex unresolved command; candidate: a curved / arcing move. IDA still calls it sub_800A210.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityCmd_8024A50_Arc(VmContext* vm, const u8**) {
    const s32 c = Pop(vm);
    const s32 b = Pop(vm);
    const s32 a = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(32));
    if (command != nullptr) {
        command[1] = 0;
        command[2] = a;
        command[3] = b;
        command[0] = 0x08024A50;
        command[5] = 32;
        command[4] = 1966 * c;
        command[7] = 0;
        command[6] = 64;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
