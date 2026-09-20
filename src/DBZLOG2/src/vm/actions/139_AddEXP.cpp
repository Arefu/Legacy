// BytecodeVM_AddEXP -- action opcode 139, 0x0800AFF0 (0x1C bytes). HIGH.
// Pops an amount and awards that much EXP to the ACTIVE character (CharStats_AddEXP).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void CharStats_AddEXP(PartyState*, s32 active_character, s32 amount);   // 0x08003D58

void BytecodeVM_AddEXP(VmContext* vm, const u8**) {
    CharStats_AddEXP(&g_PartyState, g_PartyState.active_character_index, Pop(vm));
}

}  // namespace dbzlog2::vm
