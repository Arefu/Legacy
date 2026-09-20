// Stock Transformation ability (g_AbilityTable entries 11 and 12; the two share IsReady and have byte-identical OnUse).
//   IsReady: 0x080134F2 (0x4A bytes)      OnUse (11): 0x080134C0      OnUse (12): 0x0801353C.    Read from the ROM (Thumb), MEDIUM.
// It does NOT switch the form itself: it asks the player entity to play a transformation animation, and that animation reaches whatever row the
// current row's transformed / detransformed links say. DBZKit's "Add transformation ability" generates a different pair that calls
// BytecodeVM_SetCharacterTransformation directly (see DBZKit/DrGero/TransformationAbility.cs).
#include "dbzlog2/characters.h"
#include "dbzlog2/entities.h"

namespace dbzlog2::characters {

extern const CharacterDisplayData g_CharacterDisplayData[];   // 0x081D967C
extern const u32* const g_RomPointerTable;                     // 0x083B5C78: pointers to globals (+0x20 = &g_PartyState, +0x08, +0x0C below)
void PlayerEntity_SetAnimation(entities::EntityHeader* entity, u8 animation);   // vtable slot 10 of the entity

bool Transformation_IsReady(CharacterEntry* entry) {
    const CharacterDisplayData& row = g_CharacterDisplayData[entry->display_data_index];
    if (row.flags & kDisplayFlagCannotTransform) return false;
    if (row.flags & kDisplayFlagTransformed) return true;      // already transformed: reverting is always allowed
    // base form: needs two world conditions (globals reached through g_RomPointerTable[2] and [3]; meaning not yet identified) and 10 EP
    // TODO(unknown): g_RomPointerTable[2]->+0x0C must be non-zero, g_RomPointerTable[3]->+0x08 must be zero
    return entry->current_ep >= 10;
}

void Transformation_OnUse(CharacterEntry* entry, entities::EntityHeader* entity) {
    const CharacterDisplayData& row = g_CharacterDisplayData[entry->display_data_index];
    PlayerEntity_SetAnimation(entity, (row.flags & kDisplayFlagTransformed) ? entities::kAnimRevertTransformation : entities::kAnimTransformation);
}

}  // namespace dbzlog2::characters
