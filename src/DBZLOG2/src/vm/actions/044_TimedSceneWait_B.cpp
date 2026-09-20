// BytecodeVM_TimedSceneWait_B -- action opcode 44, 0x08009BBC (0x28 bytes). LOW.
// Sibling of TimedSceneWait_A with vtable 0x08024778.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_TimedSceneWait_B(VmContext* vm, const u8**) {
    const s32 frames = Pop(vm);
    GameLoop_ExecuteAndWait(NewCommand12(kVTableTimedSceneWaitB, 0, frames));
}

}  // namespace dbzlog2::vm
