// BytecodeVM_WaitForConfirmButton -- action opcode 67, 0x0800A622 (0x16 bytes). LOW.
// Runs a fixed blocking screen object (4 bytes, vtable 0x08025C88 = table 0x08025B98 entry 60), no arguments.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_WaitForConfirmButton(VmContext*, const u8**) {
    u32* object = static_cast<u32*>(ArenaAlloc(4));
    if (object != nullptr) object[0] = 0x08025B98 + 60 * 4;
    GameLoop_ExecuteAndWait(object);
}

}  // namespace dbzlog2::vm
