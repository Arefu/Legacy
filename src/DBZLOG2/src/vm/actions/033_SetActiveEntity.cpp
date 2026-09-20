// BytecodeVM_SetActiveEntity -- action opcode 33, 0x08009814 (0x20 bytes). HIGH.
// Pops a character index and makes that character's entity the 'active entity' for following entity commands; the active context is reset to the default one (0x030020C0).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern u8 g_DefaultEntityContext[];   // 0x030020C0

void BytecodeVM_SetActiveEntity(VmContext* vm, const u8**) {
    g_ActiveEntity = Entity_GetByCharIndex(Pop(vm));
    g_ActiveEntityContext = g_DefaultEntityContext;
}

}  // namespace dbzlog2::vm
