// BytecodeVM_LockEntityPose -- action opcode 140, 0x08009F3C (0x40 bytes). MEDIUM (name and behaviour from in-game tests, mechanism not decoded).
// Pops (character, value) and enqueues a 12-byte command (vtable 0x0802553C, slot group 15 of the table at 0x08025500) with value at +8. Its execute slot
// (0x0801EA84) stores the value as a byte at entity + 8, a field IDA has not named yet; what reads it is not traced. In-game: it holds the entity in a
// pose that does not reset, and the scene stays locked; values 27 and 42 raise the arm, and values 0, 10, 20, 30 and 50 showed nothing. It is not a plain
// sprite frame index (tested). The only shipped use is Zone 12 Area 4 (Cell absorbing an android): SetEntityAnimation(11, 29); LockEntityPose(11, 41).
// Layout read from the ROM's Thumb code.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_LockEntityPose(VmContext* vm, const u8**) {
    const s32 value = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(12));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x0802553C;
        command[2] = value;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
