// BytecodeVM_WaitFramesSimple -- action opcode 68, 0x0800A638 (0x24 bytes). HIGH.
// Pops N and blocks for N frames with an 8-byte {vtable 0x08023D2C, frames} object whose per-frame slot is `--frames == 0`. A second wait opcode (WaitFrames, 69) uses vtable 0x0802585C; why there are two is unknown.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_WaitFramesSimple(VmContext* vm, const u8**) {
    const s32 frames = Pop(vm);
    u32* object = static_cast<u32*>(ArenaAlloc(8));
    if (object != nullptr) {
        object[0] = 0x08023D2C;
        object[1] = frames;
    }
    GameLoop_ExecuteAndWait(object);
}

}  // namespace dbzlog2::vm
