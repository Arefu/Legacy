// BytecodeVM_EnableDragonRadar -- action opcode 114, 0x0800AEC8 (0x10 bytes). HIGH.
// Sets PartyState.save_exists bit 3 (|= 8): unlocks the Dragon Radar / world map (confirmed in game; read by the overview-map draw routine 0x08017ACC). (OpCodes.csv: SetSaveExistsFlag_Bit3.)
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EnableDragonRadar(VmContext*, const u8**) {
    g_PartyState.save_exists |= 8;
}

}  // namespace dbzlog2::vm
