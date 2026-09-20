// BytecodeVM_PushGlobalVar2 -- action opcode 3, 0x080093C8 (0x12 bytes). HIGH.
// Pushes script global variable 2 (0x03001FBC).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushGlobalVar2(VmContext* vm, const u8**) {
    Push(vm, g_VMGlobalVar[2]);
}

}  // namespace dbzlog2::vm
