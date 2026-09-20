// BytecodeVM_SetEntityFacing -- action opcode 55, 0x08009EFC (0x40 bytes). HIGH.
// Pops (character, facing) and enqueues a 'set facing' command (vtable EntityCommandVTable_SetFacing 0x08025E34; the facing is a BYTE at +8).
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetEntityFacing(VmContext* vm, const u8**) {
    const s32 arg = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(12));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025E34;
        *reinterpret_cast<u8*>(&command[2]) = static_cast<u8>(arg);
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
