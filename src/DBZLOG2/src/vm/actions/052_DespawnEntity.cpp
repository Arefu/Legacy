// BytecodeVM_DespawnEntity -- action opcode 52, 0x08009D00 (0x4A bytes). HIGH.
// Pops a character index, marks the player as despawned if it is the map player, and enqueues a 'remove' command (8-byte object, vtable 0x08024A78) for that entity.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_DespawnEntity(VmContext* vm, const u8**) {
    const s32 index = Pop(vm);
    if (Entity_GetByCharIndex(index) == g_CurrentMapPlayerEntity) g_PlayerWasDespawned = 1;
    entities::EntityHeader* target = Entity_GetByCharIndex(index);
    u32* command = static_cast<u32*>(ArenaAlloc(8));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08024A64 + 5 * 4;   // &dword_8024A64[5]
    }
    Entity_EnqueueCommand(g_CommandQueue, command, target);
}

}  // namespace dbzlog2::vm
