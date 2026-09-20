// BytecodeVM_ClearCollisionRect -- action opcode 72, 0x0800A7F8 (0x58 bytes). HIGH.
// Same as SetCollisionRect but with flag 0 (the opposite operation) on both maps.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

struct Rect { s32 x1, y1, x2, y2; };
void CollisionList_InsertRemove(const Rect* rect, void* bitmap, s32 flag);   // 0x080063E0
extern void* g_WorldCollisionMap;   // 0x030010FC
extern void* g_EntityBlockList;     // 0x03001104

void BytecodeVM_ClearCollisionRect(VmContext* vm, const u8**) {
    Rect rect;
    rect.y2 = Pop(vm);
    rect.x2 = Pop(vm);
    rect.y1 = Pop(vm);
    rect.x1 = Pop(vm);
    CollisionList_InsertRemove(&rect, g_WorldCollisionMap, 0);
    CollisionList_InsertRemove(&rect, g_EntityBlockList, 0);
}

}  // namespace dbzlog2::vm
