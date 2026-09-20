// BytecodeVM_SetActiveCharacterNoReset -- action opcode 115, 0x0800A680 (0x8C bytes). HIGH.
// Pops a party slot and makes it the active character: tells the old form's behaviour object to leave (vtable slot 3), stores the new index, refreshes the player entity's sprite record + animation + sprite, then tells the new form's behaviour object to enter (slot 2). Does NOT reset the other slots' transformations.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern characters::CharacterDisplayData g_CharacterDisplayData[];   // 0x081D967C
void BehaviourObject_Call(RomPtr* object, int vtable_slot);           // BytecodeVM_CallFuncPtr(object, *object + (*object)[slot])
RomPtr CharacterEntry_GetSpriteRecord(const characters::CharacterEntry*);   // 0x0802645C
void PlayerEntity_RefreshSprite(entities::EntityHeader*);                 // 0x080086A2

void BytecodeVM_SetActiveCharacterNoReset(VmContext* vm, const u8**) {
    const s32 slot = Pop(vm);
    auto* behaviour = [](u8 index) { return reinterpret_cast<RomPtr*>(static_cast<uintptr_t>(g_CharacterDisplayData[index].behaviour_object)); };
    BehaviourObject_Call(behaviour(g_PartyState.characters[g_PartyState.active_character_index].display_data_index), 3);
    g_PartyState.active_character_index = static_cast<u8>(slot);
    entities::EntityHeader* player = g_PlayerObject;
    if (player != nullptr) {
        player->sprite_record = CharacterEntry_GetSpriteRecord(&g_PartyState.characters[g_PartyState.active_character_index]);
        CallSlotOfEntity(player, 0, 10);
        PlayerEntity_RefreshSprite(player);
    }
    BehaviourObject_Call(behaviour(g_PartyState.characters[g_PartyState.active_character_index].display_data_index), 2);
}

}  // namespace dbzlog2::vm
