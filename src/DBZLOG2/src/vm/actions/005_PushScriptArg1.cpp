// BytecodeVM_PushScriptArg1 -- action opcode 5, 0x080093EE (0x14 bytes). HIGH.
// Pushes argument 1 of the running script.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushScriptArg1(VmContext* vm, const u8**) {
    Push(vm, ScriptArg(1));
}

}  // namespace dbzlog2::vm
