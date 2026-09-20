// BytecodeVM_DespawnCharacterAndRecordPosition -- action opcode 104, 0x0800AAC6 (0x54 bytes). MEDIUM.
// Pops (character, record slot): marks the player despawned if it is the map player, reads the entity's position and facing, removes it, and records position + facing in the map state (sub_0800C75C) so a later map load can put the character back.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void MapState_RecordCharacter(MapState*, s32 slot, const s32* position, u8 direction);   // 0x0800C75C (IDA: sub_800C75C)
void Entity_GetPosition(s32 out_xy[2], entities::EntityHeader* entity);   // 0x0800791A
void EntityList_RemoveAndDestroy(void* list, entities::EntityHeader* entity);   // 0x080068CA

void BytecodeVM_DespawnCharacterAndRecordPosition(VmContext* vm, const u8**) {
    const s32 record_slot = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    if (entity == g_CurrentMapPlayerEntity) g_PlayerWasDespawned = 1;
    s32 position[2];
    Entity_GetPosition(position, entity);
    const u8 direction = *(reinterpret_cast<const u8*>(entity) + 0x10);   // Entity::direction (IDA type Entity, +0x10)
    EntityList_RemoveAndDestroy(g_EntityList, entity);
    MapState_RecordCharacter(&g_MapState, record_slot, position, direction);
}

}  // namespace dbzlog2::vm
