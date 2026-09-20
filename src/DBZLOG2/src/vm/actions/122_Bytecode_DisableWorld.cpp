// BytecodeVM_Bytecode_DisableWorld -- action opcode 122, 0x0800AED8 (0xC bytes). HIGH.
// Stops the world simulation (g_WorldSimulationActive = 0), e.g. during cutscenes.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern u8 g_WorldSimulationActive;   // 0x0300208C

void BytecodeVM_DisableWorld(VmContext*, const u8**) {
    g_WorldSimulationActive = 0;
}

}  // namespace dbzlog2::vm
