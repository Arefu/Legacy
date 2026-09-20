// BytecodeVM_PlayDamageEffect -- action opcode 95, 0x0800A572 (0x34 bytes). MEDIUM.
// Pops a character and enqueues an 8-byte command (vtable 0x08024DD0), no payload. The command's execute slot (0x08011A7C) allocates an effect handler
// (vtable 0x0802459C, counter 12) and installs it with Entity_SetEffectHandler; every 4 frames the handler swaps the entity's palette through a remap
// table (off_86D0DC4) and after 12 frames clears itself, so this is the "damage taken" colour flash (in-game: the effect, not the hit animation, which is
// SetEntityAnimation). Used in boss-hit sequences next to PlaySFX / ScreenShake. The character must exist on the current map (Entity_GetByCharIndex).
// Layout read from the ROM's Thumb code.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PlayDamageEffect(VmContext* vm, const u8**) {
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(8));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08024DD0;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
