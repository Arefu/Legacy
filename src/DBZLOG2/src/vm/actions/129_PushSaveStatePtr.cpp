// BytecodeVM_PushSaveStatePtr -- action opcode 129, 0x0800AFC2 (0x12 bytes). MEDIUM.
// Pushes the value of g_SaveState (0x03001FC0; the boot code sets it to 1 after probing the save chip).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern u32 g_SaveState;   // 0x03001FC0

void BytecodeVM_PushSaveStatePtr(VmContext* vm, const u8**) {
    Push(vm, static_cast<s32>(g_SaveState));
}

}  // namespace dbzlog2::vm
