// BytecodeVM_RemoveRectFromMask2 -- action opcode 117, 0x0800A850 (0x4E bytes). MEDIUM.
// Pops (x1, y1, x2, y2) and calls CollisionList_InsertRemove(rect, g_MapMask2Bitmap, 1). g_MapMask2Bitmap (0x03001100) = renderer + 0x70, the buffer loaded from Map_VariationEntry + 0x38: a second 8 KB per-map bitmap in the collision-map format. What reads it is not traced.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

struct Rect { s32 x1, y1, x2, y2; };
void CollisionList_InsertRemove(const Rect* rect, void* bitmap, s32 flag);   // 0x080063E0
extern void* g_MapMask2Bitmap;

void BytecodeVM_RemoveRectFromMask2(VmContext* vm, const u8**) {
    Rect rect;
    rect.y2 = Pop(vm);
    rect.x2 = Pop(vm);
    rect.y1 = Pop(vm);
    rect.x1 = Pop(vm);
    CollisionList_InsertRemove(&rect, g_MapMask2Bitmap, 1);
}

}  // namespace dbzlog2::vm
