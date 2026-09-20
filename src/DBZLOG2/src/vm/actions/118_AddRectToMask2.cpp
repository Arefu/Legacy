// BytecodeVM_AddRectToMask2 -- action opcode 118, 0x0800A89E (0x4E bytes). MEDIUM.
// Pops (x1, y1, x2, y2) and calls CollisionList_InsertRemove(rect, g_MapMask2Bitmap, 0). Pair of RemoveRectFromMask2 (117).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

struct Rect { s32 x1, y1, x2, y2; };
void CollisionList_InsertRemove(const Rect* rect, void* bitmap, s32 flag);   // 0x080063E0
extern void* g_MapMask2Bitmap;

void BytecodeVM_AddRectToMask2(VmContext* vm, const u8**) {
    Rect rect;
    rect.y2 = Pop(vm);
    rect.x2 = Pop(vm);
    rect.y1 = Pop(vm);
    rect.x1 = Pop(vm);
    CollisionList_InsertRemove(&rect, g_MapMask2Bitmap, 0);
}

}  // namespace dbzlog2::vm
