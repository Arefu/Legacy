// BytecodeVM_GetActiveEntityContext -- action opcode 119, 0x08009840 (0xA bytes). MEDIUM.
// Resets the active entity context to the second default context (0x030020F4).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern u8 g_DefaultEntityContext2[];   // 0x030020F4

void BytecodeVM_GetActiveEntityContext(VmContext*, const u8**) {
    g_ActiveEntityContext = g_DefaultEntityContext2;
}

}  // namespace dbzlog2::vm
