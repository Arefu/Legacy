// BytecodeVM_NPCFireKiBlast -- action opcode 99, 0x0800A210 (0x66 bytes). LOW.
// Pops 4 values (character, x, y, speed) and enqueues a 32-byte command (vtable 0x08024A50): [8] = x, [0xC] = y, [0x10] = speed * 1966, [0x14] = 32, [0x18] = 64. In-game test: with Gohan's character index it makes him fire a ki blast (not a movement command); the firing actor is an NPC, x/y are the destination and speed is the projectile speed. IDA name: sub_800A210.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_NPCFireKiBlast(VmContext* vm, const u8**) {
    const s32 speed = Pop(vm);
    const s32 y = Pop(vm);
    const s32 x = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(32));
    if (command != nullptr) {
        command[1] = 0;
        command[2] = x;
        command[3] = y;
        command[0] = 0x08024A50;
        command[5] = 32;
        command[4] = 1966 * speed;
        command[7] = 0;
        command[6] = 64;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
