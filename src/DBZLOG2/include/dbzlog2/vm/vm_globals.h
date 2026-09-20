#pragma once
// Globals, records and callee prototypes shared by the bytecode action handlers (src/vm/actions/). Names are the IDA names (2026-09-20).
// Addresses are the GBA addresses; the host build defines the globals in its own translation unit.
#include "dbzlog2/characters.h"
#include "dbzlog2/entities.h"
#include "dbzlog2/vm/vm_stack.h"

namespace dbzlog2::vm {

// ---- VM globals ---------------------------------------------------------------------------------------------------------
extern s32 g_VMAccumulator;              // 0x03001FB0  (BytecodeVM_PushAccumulator)
extern s32 g_VMGlobalVar[3];             // 0x03001FB4 / B8 / BC  (PushGlobalVar0..2)
extern VmContext* g_VMContextPtr;        // 0x03001FAC  the caller-supplied context; its first words are the script arguments (PushScriptArg0..3)
inline s32 ScriptArg(int n) { return reinterpret_cast<s32*>(g_VMContextPtr)[n]; }

// ---- map / party / world state -----------------------------------------------------------------------------------
struct MapEntry;
struct MapState {                        // g_MapState, 0x03002080, 16 bytes
    u8 zone;                             // +0x00  (IDA field name "world" in older comments)
    u8 area;                             // +0x01
    u8 variant;                          // +0x02
    u8 pad;                              // +0x03
    MapEntry* entry;                     // +0x04
    u8 unk_8[4];                         // +0x08
    u32 flags;                           // +0x0C
};
static_assert(sizeof(MapState) == 16);
extern MapState g_MapState;

struct PartyState {                      // g_PartyState, 0x03000E90, 352 bytes (0x160)
    u8 active_character_index;           // +0x000
    u8 save_exists;                      // +0x001
    u8 zone, area, variation;            // +0x002..4
    u8 gap5[3];                          // +0x005
    u8 unk8[4];                          // +0x008
    characters::CharacterEntry characters[characters::kPartySize];   // +0x00C
    u8 story_flags[108];                 // +0x0B4  bit array (PartyState_TestStoryFlag / Set / Clear)
    u8 gap120[11];                       // +0x120
    u8 inventory[50];                    // +0x12B  item counts
};
extern PartyState g_PartyState;

extern u8 g_LevelUpStatDelta_END;        // 0x03001FC1
extern u8 g_LevelUpStatDelta_POW;        // 0x03001FC2 (following byte; MEDIUM)
extern u8 g_LevelUpStatDelta_STR;        // 0x03001FC3 (following byte; MEDIUM)
extern const char* const g_StringTable[];// 0x080E2840; character names start at index 48
extern u8 g_MapRestoreFlag;              // 0x03001FC4
extern void* g_EntityList;               // 0x03001120
extern void* g_MapRenderer;              // 0x03001090
extern entities::EntityHeader* g_ActiveEntity;   // 0x030020C4
extern void* g_ActiveEntityContext;      // 0x03000FF4  (points at 0x030020C0 by default)

struct ItemDef {                         // g_ItemsInGame (0x086ADE24), IDA: Item_Definition, 56 bytes
    u32 flags;                           // +0x00
    s32 sprite_ids;                      // +0x04
    s32 width, height, oam_flags;        // +0x08..
    RomPtr sprite0;                      // +0x14  Graphic_Item*
    s32 width2, height2, oam_flags2;     // +0x18..
    RomPtr sprite1, sprite2, sprite3;    // +0x24..
    RomPtr on_item_use;                  // +0x30
    RomPtr on_item_pickup;               // +0x34
};
static_assert(sizeof(ItemDef) == 56);

// ---- callees (IDA names; bodies are transcribed separately) ---------------------------------------------------------------
bool PartyState_TestStoryFlag(PartyState*, s32 flag);                      // 0x08003E42
void PartyState_SetStoryFlag(PartyState*, s32 flag);                       // 0x08003E5E
void PartyState_ClearStoryFlag(PartyState*, s32 flag);                     // 0x08003E74
bool PartyState_TestMemberFlag(PartyState*, s32 a, s32 b);                 // 0x080041A6  (argument roles MEDIUM)
u32 CharStats_GetLevel(characters::CharacterEntry*);                       // 0x08004214
u32 Random_Range(u32 max);                                                 // 0x0802141C
bool Random_Chance(u32 threshold);                                         // 0x0802142E
s32 EntityList_CountCollisionHandlers(void* list, s32 type, s32 id);       // 0x08006E64
void* EntityList_FindByTypeAndIndex(void* list, s32 type, s32 index);      // 0x08006E0A
void GBA_DisableDisplayLayer(void* renderer, s32 layer);                   // 0x08006274
void GBA_EnableDisplayLayer(void* renderer, s32 layer);                    // 0x08006298
void MapRenderer_SetBgLayerPriority(void* renderer, s32 layer, s32 prio);  // 0x080062C0
int GameLoop_ExecuteAndWait(void* coroutine);                              // 0x0801F2F4
void* ArenaAlloc(int bytes);                                               // 0x0801F08C
void MapState_SaveForRestore(MapState*);                                   // 0x0800C7AE
void MapState_Restore(MapState*);                                          // 0x0800C7C6 (IDA: sub_800C7C6)
void Map_LoadCutscene(MapState*, s32 zone, s32 area, s32 variation, const s32* spawn_xy, s32 flag);   // 0x0800C664
void* FadeOut_Create(void* memory, s32 frames);                            // 0x0802664C
void* CameraPan_Create(void* memory, const s32* target_xy, s32 frames);    // 0x0800DF1C (IDA: sub_800DF1C)
entities::EntityHeader* Entity_GetByCharIndex(s32 index);                  // 0x08009370
void Entity_GetBounds(s32 out_rect[4], entities::EntityHeader*);           // 0x0800793A
void CallFunctionPointer(s32 arg, RomPtr function);                        // BytecodeVM_CallFuncPtr, 0x08021D2A: function(arg, function)

// ---- small command objects the handlers build --------------------------------------------------------------------------
// Most "wait" actions allocate a 12-byte object {vtable, +4, +8} from the arena, fill it as below and run it with GameLoop_ExecuteAndWait.
// A null allocation is passed through (GameLoop_ExecuteAndWait then gets nullptr, as in the ROM).
struct Command12 {
    RomPtr vtable;   // +0x00
    s32 field4;      // +0x04  0
    s32 field8;     // +0x08  the action's parameter (frames)
};
inline Command12* NewCommand12(RomPtr vtable, s32 field4, s32 field8) {
    Command12* c = static_cast<Command12*>(ArenaAlloc(12));
    if (c != nullptr) { c->vtable = vtable; c->field4 = field4; c->field8 = field8; }
    return c;
}

extern u8 g_PlayerWasDespawned;                    // 0x03001FD8
extern entities::EntityHeader* g_PlayerObject;     // 0x03001FA8
extern entities::EntityHeader* g_CurrentMapPlayerEntity;   // 0x030021F8
extern void* g_CommandQueue;                       // 0x030020F8
extern void* g_SFXPool;                            // 0x03002C80
struct SfxSample;
extern const SfxSample g_SFXDefTable[];            // 0x080E15B0
void* SFX_Queue(void* pool, const SfxSample* sfx, u8 volume, s8 pan);       // 0x0801FC9C
entities::EntityHeader* Map_Load(MapState*, s32 zone, s32 area, const s32* spawn_xy, s32 variation, s32 flag);   // 0x0800C584
void* Entity_EnqueueCommand(void* queue, void* command, entities::EntityHeader* target);   // 0x0800E04C
void* EntityList_Add(void* list, void* entity);    // 0x080068B8
void* MapScript_CreateCharacter(const void* record);   // 0x0800D6DE
void Palette_Save(void* buffer);                   // 0x0800DA68
void CallSlotOfEntity(entities::EntityHeader* entity, s32 argument, s32 vtable_slot);   // Ptr_InvokeHandler(entity, argument, vtable + vtable[slot])

// Command vtables (ROM addresses; their slot functions are not all decoded)
constexpr RomPtr kVTableScreenFade = 0x0802542C;        // FadeFromWhite / map transitions
constexpr RomPtr kVTableWarpTransition = 0x08023990;    // dword_8023990 (used by WarpToMapWithEntities)
constexpr RomPtr kVTableTimedSceneWaitA = 0x0802545C;
constexpr RomPtr kVTableTimedSceneWaitB = 0x08024778;
constexpr RomPtr kVTableFadeToWhite = 0x08024FC4;

extern const ItemDef g_ItemsInGame[];                                      // 0x086ADE24
extern RomPtr g_ScreenFadeVTable;                                          // 0x0802542C: the transition object's vtable

}  // namespace dbzlog2::vm
