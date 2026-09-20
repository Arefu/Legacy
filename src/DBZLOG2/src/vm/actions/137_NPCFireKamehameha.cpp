// BytecodeVM_NPCFireKamehameha -- action opcode 137, 0x0800A3A2 (0x46 bytes). CONFIRMED (in-game: NPCFireKamehameha(0, 140) fires a Kamehameha).
// Pops (character, frames) and enqueues a 20-byte command (vtable 0x0802521C): [0xC] = frames (a countdown), [0x10] = frames - 8. Its execute slot
// (0x0801D788) creates the beam effect object at the entity's hit-rect centre (sub_0801D84C) and sets the entity's animation to 30 through its own
// SetAnimation slot. Its update slot (0x0801D7DC) counts the countdown down each frame: when it reaches frames - 8 (8 frames in) it launches the beam
// (sub_08014478, using the entity's facing at +0x10 to pick the beam graphics and a start offset from the table at 0x08701DF0), and at 0 it ends it
// (sub_0801451C). A value of 8 or less therefore never fires (user-confirmed with 5). The attack type is hard-coded, so the number is only a duration.
// Shipped uses: Zone 10 Area 20, (28, 140) and (0, 140). The beam follows the entity's facing, so set it first with SetEntityFacing.
// Layout read from the ROM's Thumb code.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_NPCFireKamehameha(VmContext* vm, const u8**) {
    const s32 frames = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(20));
    if (command != nullptr) {
        command[0] = 0x0802521C;
        command[1] = 0;
        command[3] = frames;
        command[2] = 0;
        command[4] = frames - 8;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
