// BytecodeVM_NPCFireKiBlastAtEntity -- action opcode 96, 0x0800A158 (0x5C bytes). MEDIUM.
// Pops (character, target character, speed) and enqueues a 20-byte command (vtable 0x08025514) carrying 32, the target entity and speed * 1966.
// The command's update slot (0x08011AE2) counts down; when it reaches 16 it reads the target's hit-rect centre and the entity's position, works out a
// velocity (delta scaled by the larger axis and the speed) and spawns a moving object (sub_8011BC4, 88 bytes) plus a random SFX 13/14: the NPC fires a
// blast at the target. Tested in the tutorial fight (Zone 1 Area 2): 11 and 9 fire at 54, then EntityWaitTouchPlayer(54) waits for the hit. The third
// value is the projectile speed (the shipped scripts use 100 or 120). Unlike NPCFireKiBlast (opcode 99) it does NOT block the script. Both characters
// must exist on the current map (Entity_GetByCharIndex). IDA name: BytecodeVM_NPCFireKiBlastAtEntity (was sub_800A158).
// Layout read from the ROM's Thumb code.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_NPCFireKiBlastAtEntity(VmContext* vm, const u8**) {
    const s32 speed = Pop(vm);
    const s32 target_index = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    entities::EntityHeader* target = Entity_GetByCharIndex(target_index);
    u32* command = static_cast<u32*>(ArenaAlloc(20));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025514;
        command[2] = 32;
        command[3] = reinterpret_cast<uintptr_t>(target);
        command[4] = 1966 * speed;                   // 1966 = 0x7AE: the speed scale (also used by opcode 99)
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
