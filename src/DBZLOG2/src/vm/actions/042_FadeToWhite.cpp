// BytecodeVM_FadeToWhite -- action opcode 42, 0x08009B5E (0x36 bytes). HIGH.
// Pops a frame count and fades to white: allocates 1036 bytes (a saved copy of the palette + state), saves the palette into it and runs it (vtable 0x08024FC4, +0x404 = 0, +0x408 = frames).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

struct FadeToWhiteObject {
    u8 saved_palette[0x404];   // Palette_Save fills the first part (IDA: result of Palette_Save(v3))
    s32 field404;              // +0x404 (u16 index 257 in IDA's arithmetic)
    s32 field408;              // +0x408 = frames
};

void BytecodeVM_FadeToWhite(VmContext* vm, const u8**) {
    const s32 frames = Pop(vm);
    // The object is 1036 bytes: [0] vtable, [4..0x403] saved palette buffer, [0x404] 0, [0x408] frames  (MEDIUM: layout from IDA's index arithmetic)
    u32* object = static_cast<u32*>(ArenaAlloc(1036));
    if (object != nullptr) {
        Palette_Save(object);
        object[0] = kVTableFadeToWhite;
        object[257] = 0;
        object[258] = frames;
    }
    GameLoop_ExecuteAndWait(object);
}

}  // namespace dbzlog2::vm
