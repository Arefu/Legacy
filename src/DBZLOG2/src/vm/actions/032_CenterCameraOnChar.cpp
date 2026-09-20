// BytecodeVM_CenterCameraOnChar -- action opcode 32, 0x080097B4 (0x60 bytes). HIGH.
// Pops (character slot, frames), reads that character's entity bounds and pans the camera so the entity's centre is at the screen centre (240x160), taking `frames` frames.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_CenterCameraOnChar(VmContext* vm, const u8**) {
    const s32 frames = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    s32 bounds[4];
    Entity_GetBounds(bounds, entity);
    s32 target[2];
    target[0] = ((bounds[0] + bounds[2]) >> 1) - 120;
    target[1] = ((bounds[1] + bounds[3]) >> 1) - 80;
    GameLoop_ExecuteAndWait(CameraPan_Create(nullptr, target, frames));
}

}  // namespace dbzlog2::vm
