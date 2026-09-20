// BytecodeVM_NPCFireBigBangAtEntity -- action opcode 142, 0x0800A1B4 (0x5C bytes). MEDIUM.
// Pops (character, target character, speed) and enqueues a 20-byte command (vtable 0x08024BDC) carrying 32, the target entity and speed * 1966.
// The command's update slot (0x0801EC16) is the same as opcode 96's: it counts down to 16, then aims a projectile at the target's hit-rect centre and
// spawns it (sub_801EA8C). The differences are the projectile graphics table and a 1024-unit sprite reservation (96 reserves 256), so this is the big
// blast: Vegeta's Big Bang Attack (identified by the user from the finale script). Non-blocking, like 96. A speed of 0 divides by zero, so nothing
// moves. Both characters must exist on the current map. IDA name: BytecodeVM_NPCFireBigBangAtEntity (was sub_800A1B4).
// Layout read from the ROM's Thumb code.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_NPCFireBigBangAtEntity(VmContext* vm, const u8**) {
    const s32 speed = Pop(vm);
    const s32 target_index = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    entities::EntityHeader* target = Entity_GetByCharIndex(target_index);
    u32* command = static_cast<u32*>(ArenaAlloc(20));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08024BDC;
        command[2] = 32;
        command[3] = reinterpret_cast<uintptr_t>(target);
        command[4] = 1966 * speed;                   // 1966 = 0x7AE: the speed scale (also used by opcodes 96 and 99)
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
