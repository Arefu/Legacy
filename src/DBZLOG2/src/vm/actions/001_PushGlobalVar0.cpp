// BytecodeVM_PushGlobalVar0 -- action opcode 1, 0x080093A4 (0x12 bytes). HIGH.
// Pushes script global variable 0 (0x03001FB4).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushGlobalVar0(VmContext* vm, const u8**) {
    Push(vm, g_VMGlobalVar[0]);
}

}  // namespace dbzlog2::vm
