// BytecodeVM_FindPlayerEntity -- action opcode 88, 0x0800AC00 (0x10 bytes). MEDIUM.
// Looks up entity kind 16 (index 0) in g_EntityList; the result is left in r0 and DISCARDED -- unlike the Push* handlers this one never writes to the VM
// stack. In-game test: no visible effect. Every shipped boss-fight script calls it just before AddEXP, which pops only the amount and awards it to the
// active party character (see BytecodeVM_AddEXP), so the lookup does not affect who gets the EXP. The kind is assumed to be the player.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_FindPlayerEntity(VmContext*, const u8**) {
    EntityList_FindByTypeAndIndex(g_EntityList, 16, 0);
}

}  // namespace dbzlog2::vm
