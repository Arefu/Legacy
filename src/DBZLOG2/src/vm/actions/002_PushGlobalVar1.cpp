// BytecodeVM_PushGlobalVar1 -- action opcode 2, 0x080093B6 (0x12 bytes). HIGH.
// Pushes script global variable 1 (0x03001FB8).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushGlobalVar1(VmContext* vm, const u8**) {
    Push(vm, g_VMGlobalVar[1]);
}

}  // namespace dbzlog2::vm
