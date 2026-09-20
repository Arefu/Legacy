// BytecodeVM_WaitFrames -- action opcode 69, 0x0800A65C (0x24 bytes). MEDIUM.
// Pops N and blocks for N frames with an 8-byte {vtable 0x0802585C, frames} object (table 0x080256D8 entry 97). See WaitFramesSimple (68) for the other wait.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_WaitFrames(VmContext* vm, const u8**) {
    const s32 frames = Pop(vm);
    u32* object = static_cast<u32*>(ArenaAlloc(8));
    if (object != nullptr) {
        object[0] = 0x080256D8 + 97 * 4;
        object[1] = frames;
    }
    GameLoop_ExecuteAndWait(object);
}

}  // namespace dbzlog2::vm
