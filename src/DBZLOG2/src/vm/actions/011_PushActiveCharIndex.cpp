// BytecodeVM_PushActiveCharIndex -- action opcode 11, 0x0800949C (0x14 bytes). HIGH.
// Pushes the active party slot (0-5).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_PushActiveCharIndex(VmContext* vm, const u8**) {
    Push(vm, g_PartyState.active_character_index);
}

}  // namespace dbzlog2::vm
