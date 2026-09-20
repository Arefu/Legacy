// BytecodeVM_WhiteFadeOut -- action opcode 43, 0x08009B94 (0x28 bytes). LOW.
// Pops a frame count and runs a 12-byte blocking scene command (vtable 0x0802545C, param at +8). The scene keeps updating and drawing while it runs; probably a timed transition, exact effect unknown.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_WhiteFadeOut(VmContext* vm, const u8**) {
    const s32 frames = Pop(vm);
    GameLoop_ExecuteAndWait(NewCommand12(kVTableTimedSceneWaitA, 0, frames));
}

}  // namespace dbzlog2::vm
