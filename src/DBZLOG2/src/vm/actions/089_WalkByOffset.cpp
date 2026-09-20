// BytecodeVM_WalkByOffset -- action opcode 89, 0x08009D94 (0x5C bytes). CONFIRMED (in-game).
// Pops (character, other character, offX, offY) and enqueues a 28-byte command (vtable 0x08025A98, built by sub_0801094C) on the first character with the
// other entity at +0x10 and the offsets at +0x14 / +0x18. Its execute slot (0x08010976) takes the entity's own position, adds (offX, offY), and starts a
// walk-to behaviour toward that point (sub_08010898 -> WalkToBehavior). User-confirmed: WalkByOffset(54, 0, 0, 100) walks 100 pixels down and
// (7, 0, 100, 0) walks 100 pixels right, with the walk animation; left, up and down behave the same way. The second character (stored at +0x10) is
// never read by any of the command's routines and any value gave the same result, so it is ignored. The update slot (0x0801090A) waits for the walk to
// finish and then calls the entity's slot 13 routine and SetAnimation(0), so it ends idle; with an offset of (0, 0) the walk ends at once and the entity
// just returns to idle facing right (user-observed with (54, 100, 0, 0)).
// Shipped uses: Zone 4 Areas 85, 87, 97 and 99, with an offset of (0, 0), just before the NPC is despawned.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* WalkByOffsetCmd_Create(void* memory, entities::EntityHeader* other, const s32* params);   // 0x0801094C

void BytecodeVM_WalkByOffset(VmContext* vm, const u8**) {
    s32 params[2];
    params[1] = Pop(vm);   // offY
    params[0] = Pop(vm);   // offX
    const s32 other_index = Pop(vm);
    entities::EntityHeader* target = Entity_GetByCharIndex(Pop(vm));
    entities::EntityHeader* other = Entity_GetByCharIndex(other_index);
    Entity_EnqueueCommand(g_CommandQueue, WalkByOffsetCmd_Create(nullptr, other, params), target);
}

}  // namespace dbzlog2::vm
