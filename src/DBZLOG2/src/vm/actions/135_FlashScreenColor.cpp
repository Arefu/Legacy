// BytecodeVM_FlashScreenColor -- action opcode 135, 0x0800A2C2 (0x84 bytes). CONFIRMED (in-game: red, purple and black flashes).
// Pops (character, r, g, b, frames) and enqueues a 36-byte command (vtable 0x08025528): [8] = r, [0xC] = g, [0x10] = b, [0x14] = frames (= [0x18] the
// running counter), [0x1C] = frames / 2, [0x20] = 256 / (frames / 2) (the per-frame strength step). NOTE it pops FIVE values -- the old table said four.
// Its update slot (0x0801D348) counts the counter down, ramps a strength value up to 256 at the halfway point and back down, and hands (r, g, b,
// strength) to sub_0800BE14, which blends every palette entry (512 colours, BG and sprites) from a base table toward that colour and writes palette RAM
// (a whole-screen colour flash). Its "is done" slot (0x0801D368) is true when the counter reaches 0, so the entity's queue waits for the whole flash.
// The character only selects the queue and must exist on the map.
// Layout read from the ROM's Thumb code.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

s32 Div32(s32 divisor, s32 dividend);   // 0x08021F30: dividend / divisor

void BytecodeVM_FlashScreenColor(VmContext* vm, const u8**) {
    const s32 frames = Pop(vm);
    const s32 b = Pop(vm);
    const s32 g = Pop(vm);
    const s32 r = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    u32* command = static_cast<u32*>(ArenaAlloc(36));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025528;
        command[2] = r;
        command[3] = g;
        command[4] = b;
        command[5] = frames;
        command[7] = frames >> 1;
        command[6] = frames;
        command[8] = Div32(frames >> 1, 0x100);
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
