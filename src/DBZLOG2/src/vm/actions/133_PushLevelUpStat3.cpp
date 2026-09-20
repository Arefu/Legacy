// BytecodeVM_PushLevelUpStat3 -- action opcode 133, 0x0800948A (0x12 bytes). MEDIUM.
// Pushes the strength gained by the last level-up.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushLevelUpStat3(VmContext* vm, const u8**) {
    Push(vm, g_LevelUpStatDelta_STR);
}

}  // namespace dbzlog2::vm
