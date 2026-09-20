// BytecodeVM_FindEntityType16 -- action opcode 88, 0x0800AC00 (0x10 bytes). LOW.
// Looks up entity type 16 (index 0) in g_EntityList; the result is left in r0 (not pushed). Candidate: the player entity if type 16 is the player.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_FindEntityType16(VmContext*, const u8**) {
    EntityList_FindByTypeAndIndex(g_EntityList, 16, 0);
}

}  // namespace dbzlog2::vm
