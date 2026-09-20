#pragma once
// Party / character tables and the ability record. Layouts from Roster-and-Abilities.md (HIGH: decompile and ROM data agree).
#include "dbzlog2/types.h"

namespace dbzlog2::characters {

// One party slot, also what is saved (28 bytes). CharacterDefaultStats_Table (0x081D9548) holds the 6 new-game values.
struct CharacterEntry {
    u16 current_hp;          // +0x00
    u16 max_hp;              // +0x02
    s16 current_ep;          // +0x04  signed: the ability ready checks compare it as ldrsh
    u16 max_ep;              // +0x06
    u8 display_data_index;   // +0x08  row in Character_DisplayData: which character / form this slot IS right now
    u8 ability_index;        // +0x09  which of the 4 abilities is selected
    u16 strength;            // +0x0A  256 = 1.0, capped at 25600
    u16 power;               // +0x0C
    u16 endurance;           // +0x0E
    u32 exp;                 // +0x10
    u32 flags;               // +0x14  bit0 = in party
    u8 ability_list[4];      // +0x18  indices into g_AbilityTable
};
static_assert(sizeof(CharacterEntry) == 28);

// One display row = one character FORM (base and Super Saiyan are separate rows), Character_DisplayData (0x081D967C), 22 rows in the ROM.
struct CharacterDisplayData {
    u8 flags;                // +0x00  bit0 = a transformed form, bit1 = cannot transform (MEDIUM)
    u8 sprite_id;            // +0x01  key into g_CharacterSpriteIndex (CharacterData_FindBySprite matches the FIRST row with this id)
    u8 portrait_index;       // +0x02  Portrait_Table (0x083EC9D4)
    u8 unk3;                 // +0x03  per-character family id (LOW)
    u8 transformed_index;    // +0x04  row link
    u8 detransformed_index;  // +0x05  row link (what the stock Transformation reverts to)
    u8 min_frames;           // +0x06  transform timing (MEDIUM)
    u8 max_frames;           // +0x07
    u8 transformed_index2;   // +0x08
    u8 unk9;                 // +0x09
    u16 unk_a;               // +0x0A
    u32 unk_c;               // +0x0C  a spare word
    RomPtr behaviour_object; // +0x10  RAM pointer 0x030021E0..0x030021F0: the flight / ground behaviour object (has slots 2 and 3 used by transformations)
};
static_assert(sizeof(CharacterDisplayData) == 20);

// g_AbilityTable (0x081D9900), 14 entries. Player_UseAbility runs abilityList[abilityIndex]: is_ready(entry) then on_use(entry, player_entity).
struct AbilityDef {
    RomPtr ready_icon;       // +0x00  0x200-byte HUD icon resource
    RomPtr cooling_icon;     // +0x04
    RomPtr is_ready;         // +0x08  bool (*)(CharacterEntry*)                      Thumb
    RomPtr on_use;           // +0x0C  void (*)(CharacterEntry*, PlayerEntity*)       Thumb
};
static_assert(sizeof(AbilityDef) == 16);

constexpr int kPartySize = 6;
constexpr u8 kDisplayFlagTransformed = 0x01;
constexpr u8 kDisplayFlagCannotTransform = 0x02;

// PartyState (IWRAM 0x03000E90, 0x164 bytes; copied to / from the 3 save slots).
struct PartyStateHeader {
    u8 active_character_index;   // +0x00
    u8 unknown[0x0B];            // +0x01
    CharacterEntry characters[kPartySize];   // +0x0C
    // +0xB4 story flags, +0xE7 visited-area bits, +0x12B inventory (see Roster-and-Abilities.md)
};

}  // namespace dbzlog2::characters
