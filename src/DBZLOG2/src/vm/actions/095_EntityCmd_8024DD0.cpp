// BytecodeVM_EntityCmd_8024DD0 -- action opcode 95, 0x0800A572 (0x34 bytes). LOW.
// Pops a character and enqueues an 8-byte command (vtable 0x08024DD0), no payload. Not decoded.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityCmd_8024DD0(VmContext* vm, const u8**) {
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(8));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08024DD0;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
