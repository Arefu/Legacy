// BytecodeVM_EntityCmd_8025528_4arg -- action opcode 135, 0x0800A2C2 (0x84 bytes). LOW.
// Pops (character, a, b, c, d) and enqueues a 36-byte command (vtable 0x08025528): [8] = a, [0xC] = b, [0x10] = c, [0x14] = d, [0x18] = d, [0x1C] = d / 2, [0x20] = 256 / (d / 2). Not decoded (looks like a timed interpolation: d frames, per-frame step 256 / (d/2)).
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

s32 Div32(s32 divisor, s32 dividend);   // 0x08021F30: dividend / divisor

void BytecodeVM_EntityCmd_8025528_4arg(VmContext* vm, const u8**) {
    const s32 d = Pop(vm);
    const s32 c = Pop(vm);
    const s32 b = Pop(vm);
    const s32 a = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(36));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025528;
        command[2] = a;
        command[3] = b;
        command[4] = c;
        command[5] = d;
        command[7] = d >> 1;
        command[6] = d;
        command[8] = Div32(d >> 1, 0x100);
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
