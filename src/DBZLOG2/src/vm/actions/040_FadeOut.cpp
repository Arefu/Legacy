// BytecodeVM_FadeOut -- action opcode 40, 0x08009B1C (0x1A bytes). HIGH.
// Pops a frame count and fades the screen out over that many frames (blocks until done).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_FadeOut(VmContext* vm, const u8**) {
    GameLoop_ExecuteAndWait(FadeOut_Create(nullptr, Pop(vm)));
}

}  // namespace dbzlog2::vm
