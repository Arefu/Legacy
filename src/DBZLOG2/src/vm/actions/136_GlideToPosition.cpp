// BytecodeVM_GlideToPosition -- action opcode 136, 0x0800A346 (0x5C bytes). CONFIRMED (in-game).
// Pops (character, x, y, speed) and enqueues a 28-byte command (vtable 0x08024C04): [0x10] = x, [0x14] = y, [0x18] = speed. Its execute slot (0x0801D2BC)
// builds a WalkToBehavior_Init behaviour aimed at (x, y) with speed * 656 >> 8 and installs it on the entity, but the entity's animation is left alone,
// so it glides rather than plays a walk cycle (user-confirmed: no walking animation, unlike WalkToPosition). The only shipped use is Zone 14 Area 2
// Trigger[0] Dialog #22: Cell (sprite 30) attacks and Hercule (sprite 72 = Mr. Satan, see Roster-and-Abilities.md) is hit; after his hurt animation
// (SetEntityAnimation(72, 29)), GlideToPosition(72, 448, 850, 1000) knocks him back before he is despawned. The character must exist on the map.
// Layout read from the ROM's Thumb code.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_GlideToPosition(VmContext* vm, const u8**) {
    const s32 speed = Pop(vm);
    const s32 y = Pop(vm);
    const s32 x = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(28));
    if (command != nullptr) {
        command[1] = 0;
        command[2] = 0;
        command[3] = 0;
        command[4] = x;
        command[5] = y;
        command[0] = 0x08024C04;
        command[6] = speed;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
