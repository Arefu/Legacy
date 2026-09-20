// BytecodeVM_EnableMenuAccess -- action opcode 113, 0x0800AEB8 (0x10 bytes). MEDIUM.
// Sets PartyState.save_exists bit 2 (|= 4): the overworld's SELECT / START menu shortcuts are gated on it (sub_08004CAC), so this looks like 'the pause / status menu may be opened'. (OpCodes.csv: SetSaveExistsFlag_Bit2.)
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EnableMenuAccess(VmContext*, const u8**) {
    g_PartyState.save_exists |= 4;
}

}  // namespace dbzlog2::vm
