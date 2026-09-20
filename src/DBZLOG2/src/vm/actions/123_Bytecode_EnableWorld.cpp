// BytecodeVM_Bytecode_EnableWorld -- action opcode 123, 0x0800AEE4 (0xC bytes). HIGH.
// Resumes the world simulation (g_WorldSimulationActive = 1).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern u8 g_WorldSimulationActive;   // 0x0300208C

void BytecodeVM_EnableWorld(VmContext*, const u8**) {
    g_WorldSimulationActive = 1;
}

}  // namespace dbzlog2::vm
