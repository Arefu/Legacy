// BytecodeVM_SetEntityFollow -- action opcode 56, 0x08009F7C (0x48 bytes). MEDIUM.
// Pops (character, followee) and enqueues a 'follow' command (vtable 0x08024A64) that stores the followee's ENTITY POINTER at +8.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetEntityFollow(VmContext* vm, const u8**) {
    const s32 followee_index = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    entities::EntityHeader* followee = Entity_GetByCharIndex(followee_index);
    u32* command = static_cast<u32*>(ArenaAlloc(12));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08024A64;
        command[2] = reinterpret_cast<uintptr_t>(followee);
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
