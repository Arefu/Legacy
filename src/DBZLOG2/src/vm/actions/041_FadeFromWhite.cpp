// BytecodeVM_FadeFromWhite -- action opcode 41, 0x08009B36 (0x28 bytes). HIGH.
// Pops a frame count and fades in from white (the 12-byte object with vtable 0x0802542C). Same object as the map transitions use.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_FadeFromWhite(VmContext* vm, const u8**) {
    const s32 frames = Pop(vm);
    GameLoop_ExecuteAndWait(NewCommand12(kVTableScreenFade, 0, frames));
}

}  // namespace dbzlog2::vm
