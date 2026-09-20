// BytecodeVM_SetEntityAnimationNoWait -- action opcode 121, 0x0800A004 (0x40 bytes). CONFIRMED (in-game).
// Pops (character, animId) and enqueues a 12-byte command (vtable 0x08025ED4) with animId at +8. Its execute slot (0x0800DF60) is the SAME routine
// SetEntityAnimation's command uses: it calls the entity's own vtable slot 10 with animId (CharacterEntity_SetAnimation / CombatEntity_SetAnimation,
// which look the id up in a per-type animation table). The only difference is the "is it done?" slot: SetEntityAnimation's (0x0800DF74) keeps the
// entity's queue waiting until the animation stops playing, while this command's returns done at once. So the animation starts and the next queued
// command runs immediately (user-confirmed: the animation is too quick to see next to a sound in the same batch).
// Layout read from the ROM's Thumb code.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetEntityAnimationNoWait(VmContext* vm, const u8**) {
    const s32 arg = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(12));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025ED4;
        command[2] = arg;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
