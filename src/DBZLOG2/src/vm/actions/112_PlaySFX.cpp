// BytecodeVM_PlaySFX -- action opcode 112, 0x0800AE76 (0x42 bytes). MEDIUM.
// Pops (character, sfxId) and enqueues a 12-byte command (vtable 0x08025244) with sfxId at +8. The command's execute slot (0x08016310)
// is SFX_Queue(g_SFXPool, &g_SFXDefTable[sfxId], volume 64, pan 0), so this plays sound effect sfxId when the entity's queue reaches it (inside a
// command batch it lines up with that entity's other queued commands). The sound never uses the entity: the character only selects the queue,
// and it must exist on the current map (Entity_GetByCharIndex only finds live entities). Seen in ~134 shipped script calls with ids 20, 21, 26, 31, 34-36, 59.
// Layout read from the ROM's Thumb code.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PlaySFX(VmContext* vm, const u8**) {
    const s32 sfxId = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(12));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025244;
        command[2] = sfxId;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
