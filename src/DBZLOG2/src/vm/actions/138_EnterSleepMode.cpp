// BytecodeVM_EnterSleepMode -- action opcode 138, 0x0800AFE8 (0x8 bytes). HIGH.
// Calls Game_EnterSleepMode (0x08021388): the GBA sleep / low-power routine.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

int Game_EnterSleepMode();   // 0x08021388

void BytecodeVM_EnterSleepMode(VmContext*, const u8**) {
    Game_EnterSleepMode();
}

}  // namespace dbzlog2::vm
