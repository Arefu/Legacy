// BytecodeVM_PushScriptArg2 -- action opcode 6, 0x08009402 (0x14 bytes). HIGH.
// Pushes argument 2 of the running script.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushScriptArg2(VmContext* vm, const u8**) {
    Push(vm, ScriptArg(2));
}

}  // namespace dbzlog2::vm
