// BytecodeVM_SetMapRestoreFlag -- action opcode 34, 0x0800984A (0x8 bytes). HIGH.
// Marks that the next map load should save the current map so a later RestoreMap can come back to it.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetMapRestoreFlag(VmContext*, const u8**) {
    g_MapRestoreFlag = 1;
}

}  // namespace dbzlog2::vm
