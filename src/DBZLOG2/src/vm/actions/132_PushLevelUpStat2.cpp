// BytecodeVM_PushLevelUpStat2 -- action opcode 132, 0x08009478 (0x12 bytes). MEDIUM.
// Pushes the power gained by the last level-up.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushLevelUpStat2(VmContext* vm, const u8**) {
    Push(vm, g_LevelUpStatDelta_POW);
}

}  // namespace dbzlog2::vm
