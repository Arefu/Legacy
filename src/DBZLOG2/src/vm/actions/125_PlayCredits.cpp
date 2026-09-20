// BytecodeVM_PlayCredits -- action opcode 125, 0x0800AF44 (0x2E bytes). CONFIRMED (in-game: it goes to the credits).
// Allocates an 84-byte scene object (base constructor sub_08001F04(obj, 1), vtable 0x08023E88 + 12 * 4), runs it to completion and then zero-fills 0x18000 bytes of VRAM:
// the credits are a full-screen scene that owns all of VRAM, so the map has to be reloaded afterwards. Shipped use: Zone 4 Area 5 Trigger[9] Dialog #29,
// FadeOut(300); PlayCredits(); SetStoryFlag(196).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void SceneObject_Init(void* object, s32 mode);   // 0x08001F04 (IDA: sub_8001F04)
void DmaFill(void* dst, u32 value, u32 bytes);   // 0x0801F46A (IDA: sub_801F46A)

void BytecodeVM_PlayCredits(VmContext*, const u8**) {
    u32* scene = static_cast<u32*>(ArenaAlloc(84));
    if (scene != nullptr) {
        SceneObject_Init(scene, 1);
        scene[0] = 0x08023E88 + 12 * 4;
    }
    GameLoop_ExecuteAndWait(scene);
    DmaFill(reinterpret_cast<void*>(0x06000000), 0, 0x18000);
}

}  // namespace dbzlog2::vm
