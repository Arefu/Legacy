// BytecodeVM_SetCharacterFlag_Dynamic -- action opcode 83, 0x0800AA7C (0x32 bytes). MEDIUM.
// Pops (slot, bit) and sets flags |= 4 << bit: a generic setter for bits 2 and up. The purpose of that bit range is not confirmed.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SetCharacterFlag_Dynamic(VmContext* vm, const u8**) {
    const s32 bit = Pop(vm);
    g_PartyState.characters[Pop(vm)].flags |= 4u << bit;
}

}  // namespace dbzlog2::vm
