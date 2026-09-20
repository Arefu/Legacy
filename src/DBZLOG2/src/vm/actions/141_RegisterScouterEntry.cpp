// BytecodeVM_RegisterScouterEntry -- action opcode 141, 0x0800B00C (0x36 bytes). MEDIUM.
// Pops a character / sprite id, looks up its Scouter database entry (Scouter_FindEntryBySpriteId) and, if found, sets that entry's bit in the story-flag block at +102 (story_flags[102 + n / 8] |= 1 << (n & 7)): 'this enemy has been scanned'.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

RomPtr Character_GetSpriteRecord(s32 id);   // 0x08009324
s32 Scouter_FindEntryBySpriteId(RomPtr sprite_record);   // 0x08019102

void BytecodeVM_RegisterScouterEntry(VmContext* vm, const u8**) {
    const s32 entry = Scouter_FindEntryBySpriteId(Character_GetSpriteRecord(Pop(vm)));
    if (entry != -1) g_PartyState.story_flags[102 + (entry >> 3)] |= static_cast<u8>(1 << (entry & 7));
}

}  // namespace dbzlog2::vm
