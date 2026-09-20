// BytecodeVM_ShowChoicePrompt -- action opcode 31, 0x0800977A (0x3A bytes). LOW.
// Pops 3 values (c, b, a), builds a modal object from the pair (a, b) with c frames and runs it to completion. The 'choice prompt' name is a guess: it is the same camera-pan constructor CenterCameraOnChar uses (0x0800DF1C), so it may really be a camera move to (a, b).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_ShowChoicePrompt(VmContext* vm, const u8**) {
    const s32 frames = Pop(vm);
    s32 xy[2];
    xy[1] = Pop(vm);
    xy[0] = Pop(vm);
    GameLoop_ExecuteAndWait(CameraPan_Create(nullptr, xy, frames));
}

}  // namespace dbzlog2::vm
