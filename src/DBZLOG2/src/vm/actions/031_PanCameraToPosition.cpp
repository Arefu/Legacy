// BytecodeVM_PanCameraToPosition -- action opcode 31, 0x0800977A (0x3A bytes). MEDIUM.
// Pops 3 values (speed, y, x) and runs a camera pan, blocking until it finishes. In-game test: the old guess ('ShowChoicePrompt', a yes/no menu) was wrong; it is a
// camera pan (same constructor, CameraPan_Create 0x0800DF1C, that CenterCameraOnChar uses). x / y are WORLD PIXELS of the camera's TOP-LEFT corner (the view is 240x160), not
// the centre: CenterCameraOnChar passes (entityCentre - 120, entityCentre - 80). To centre on world point (wx, wy) pass x = wx - 120, y = wy - 80. Internally x/y are <<10 fixed point and
// subtracted from the current camera position g_MapRenderer[1..2]. The third value is a SPEED (in-game: bigger = faster); it is divided against the larger of |dx|, |dy| to get the step count.
// IDA name: sub_800977A.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PanCameraToPosition(VmContext* vm, const u8**) {
    const s32 speed = Pop(vm);
    s32 xy[2];
    xy[1] = Pop(vm);
    xy[0] = Pop(vm);
    GameLoop_ExecuteAndWait(CameraPan_Create(nullptr, xy, speed));
}

}  // namespace dbzlog2::vm
