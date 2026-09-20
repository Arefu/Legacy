// BytecodeVM_PushScriptArg0 -- action opcode 4, 0x080093DA (0x14 bytes). HIGH.
// Pushes argument 0 of the running script (word 0 of the caller-supplied context).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushScriptArg0(VmContext* vm, const u8**) {
    Push(vm, ScriptArg(0));
}

}  // namespace dbzlog2::vm
