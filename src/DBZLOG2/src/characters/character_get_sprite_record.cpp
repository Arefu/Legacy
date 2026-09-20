// Character_GetSpriteId (IDA) -- 0x08009324 (0x4C bytes). HIGH. The IDA name is misleading: it returns the SPRITE RECORD pointer (the value
// stored at entity+0x48), not an id. Suggested rename: Character_GetSpriteRecord.
//   id 0        the player entity's current record (or, with no player entity, the active party member's record)
//   id 1..6     party slot id-1 (dynamic: follows the slot's current display row)
//   id >= 7     g_CharacterSpriteIndex[id]  (no bounds check; the table base literal lives at file 0x959C)
#include "dbzlog2/characters.h"
#include "dbzlog2/entities.h"
#include "dbzlog2/sprites.h"

namespace dbzlog2::characters {

extern PartyStateHeader g_PartyState;                          // 0x03000E90
extern entities::EntityHeader* g_PlayerEntity;                 // 0x03001FA8
extern const RomPtr g_CharacterSpriteIndex[];                  // file 0x3B4E74 (relocatable, see sprites.h)
RomPtr CharacterEntry_GetSpriteRecord(const CharacterEntry* entry);   // 0x0802645C (sub_802645C): the record of the entry's current form

RomPtr Character_GetSpriteRecord(int id) {
    if (id == 0) {
        if (g_PlayerEntity) return g_PlayerEntity->sprite_record;
        return CharacterEntry_GetSpriteRecord(&g_PartyState.characters[g_PartyState.active_character_index]);
    }
    if (id < static_cast<int>(sprites::kSpriteIdFirstNpc)) return CharacterEntry_GetSpriteRecord(&g_PartyState.characters[id - 1]);
    return g_CharacterSpriteIndex[id];
}

}  // namespace dbzlog2::characters
