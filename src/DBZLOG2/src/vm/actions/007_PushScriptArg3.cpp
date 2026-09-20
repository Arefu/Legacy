// BytecodeVM_PushScriptArg3 -- action opcode 7, 0x08009416 (0x14 bytes). HIGH.
// Pushes argument 3 of the running script.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushScriptArg3(VmContext* vm, const u8**) {
    Push(vm, ScriptArg(3));
}

}  // namespace dbzlog2::vm
