// BytecodeVM_SetCharacterTransformation -- action opcode 73, 0x0800A8EC (0xAC bytes). MEDIUM.
// Script: PushByte slot, PushByte spriteId, Step 73. Pops spriteId then slot, switches that party slot to the display row whose sprite id matches (CharacterData_FindBySprite, first match) and refreshes the player entity. For the active character the old row's behaviour object is told to leave the form (vtable slot 3) and the new one to enter it (slot 2). Read from the ROM's Thumb code.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern characters::CharacterDisplayData g_CharacterDisplayData[];   // 0x081D967C
void BehaviourObject_Call(RomPtr* object, int vtable_slot);           // BytecodeVM_CallFuncPtr(object, *object + (*object)[slot])
void CharacterData_FindBySprite(characters::CharacterEntry* entry, u32 sprite_id);   // 0x080042BC
RomPtr Character_GetSpriteRecord(s32 id);                             // 0x08009324
void PlayerEntity_RefreshSprite(entities::EntityHeader*);             // 0x080086A2

void BytecodeVM_SetCharacterTransformation(VmContext* vm, const u8**) {
    const u32 sprite_id = static_cast<u32>(Pop(vm));
    const u32 slot = static_cast<u32>(Pop(vm));
    auto* behaviour = [](u8 index) { return reinterpret_cast<RomPtr*>(static_cast<uintptr_t>(g_CharacterDisplayData[index].behaviour_object)); };

    entities::EntityHeader* entity = nullptr;
    if (g_PartyState.active_character_index == slot) {
        BehaviourObject_Call(behaviour(g_PartyState.characters[g_PartyState.active_character_index].display_data_index), 3);
        entity = g_PlayerObject;
    }
    CharacterData_FindBySprite(&g_PartyState.characters[slot & 0xFF], sprite_id);
    if (entity != nullptr) {
        entity->sprite_record = Character_GetSpriteRecord(static_cast<s32>(sprite_id));
        CallSlotOfEntity(entity, 0, 10);
        PlayerEntity_RefreshSprite(entity);
    }
    if (g_PartyState.active_character_index == slot) BehaviourObject_Call(behaviour(g_PartyState.characters[g_PartyState.active_character_index].display_data_index), 2);
}

}  // namespace dbzlog2::vm
