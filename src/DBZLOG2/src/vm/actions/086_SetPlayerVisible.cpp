// BytecodeVM_SetPlayerVisible -- action opcode 86, 0x0800AB82 (0x22 bytes). MEDIUM.
// Pops a flag and passes (flag != 0) to sub_080040AA(&g_PartyState, ...): shows / hides the player (the callee sets a visibility bit; not decoded).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void PartyState_SetPlayerVisible(PartyState*, bool visible);   // 0x080040AA (IDA: sub_80040AA)

void BytecodeVM_SetPlayerVisible(VmContext* vm, const u8**) {
    PartyState_SetPlayerVisible(&g_PartyState, Pop(vm) != 0);
}

}  // namespace dbzlog2::vm
