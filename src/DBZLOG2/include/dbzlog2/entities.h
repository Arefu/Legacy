#pragma once
// The few entity fields the recovered character code touches. The player entity is the pointer at IWRAM 0x03001FA8 (0 when there is none).
#include "dbzlog2/types.h"

namespace dbzlog2::entities {

constexpr u32 kEntityVTableSlotSetAnimation = 10;   // vtable + 0x28: SetAnimation(entity, animationId) (Ptr_InvokeHandler(entity, id, that))

struct EntityHeader {
    RomPtr vtable;        // +0x00  relative-offset table (see vtable.h)
    u8 gap[0x44];         // +0x04
    RomPtr sprite_record; // +0x48  the sprite record this entity draws (set from Character_GetSpriteRecord)
};
static_assert(offsetof(EntityHeader, sprite_record) == 0x48);

// Animation ids the character abilities request through SetAnimation (MEDIUM: taken from the Transformation / ability on_use functions).
enum PlayerAnimation : u8 {
    kAnimIdle = 0,
    kAnimRevertTransformation = 0x24,
    kAnimTransformation = 0x25,
};

}  // namespace dbzlog2::entities
