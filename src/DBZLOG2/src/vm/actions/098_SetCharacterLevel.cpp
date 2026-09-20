// BytecodeVM_SetCharacterLevel -- action opcode 98, 0x0800AC10 (0x4C bytes). HIGH.
// Pops (slot, level): the level is capped at 50; if the character is below it, applies the level-ups (CharStats_LevelUpRange) and sets the EXP to that level's threshold.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void CharStats_LevelUpRange(PartyState*, s32 slot, s32 from_level, s32 to_level);   // 0x08003CA0
void CharStats_SetEXPToLevel(characters::CharacterEntry*, s32 level);              // 0x08004276

void BytecodeVM_SetCharacterLevel(VmContext* vm, const u8**) {
    s32 target = Pop(vm);
    const s32 slot = Pop(vm);
    if (target > 50) target = 50;
    const s32 current = static_cast<s32>(CharStats_GetLevel(&g_PartyState.characters[slot]));
    if (current < target) {
        CharStats_LevelUpRange(&g_PartyState, slot, current, target);
        CharStats_SetEXPToLevel(&g_PartyState.characters[slot], target);
    }
}

}  // namespace dbzlog2::vm
