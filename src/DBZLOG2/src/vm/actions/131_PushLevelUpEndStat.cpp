// BytecodeVM_PushLevelUpEndStat -- action opcode 131, 0x08009466 (0x12 bytes). HIGH.
// Pushes the endurance gained by the last level-up (used by level-up scripts).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushLevelUpEndStat(VmContext* vm, const u8**) {
    Push(vm, g_LevelUpStatDelta_END);
}

}  // namespace dbzlog2::vm
