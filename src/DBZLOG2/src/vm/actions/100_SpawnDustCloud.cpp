// BytecodeVM_SpawnDustCloud -- action opcode 100, 0x0800A4DA (0x4C bytes). LOW.
// Pops (character, x, y) and enqueues a command (vtable 0x08025230) with payload (x, y). In-game test: spawns a small dust-cloud explosion at x/y; the character index is still required (it selects the command queue). IDA name: sub_800A4DA.
// Vtable 0x08025230 decoded from the ROM's Thumb code (slot n = vtable + vtable[n]; slot roles are inferred, not confirmed):
//   [0] 0x08027688  cleanup: calls 0x08026A10(cmd, 0), then 0x0801F110(cmd) if the flag arg is set (free).
//   [1] 0x08011A52  EXECUTE: calls 0x0800FA1C(NULL, &cmd[2] /*x,y*/, 0x27), which allocates a 0x44-byte object (0x0801F08C) = an effect of
//                   type 0x27 at (x, y) -- the dust cloud -- then hands it to 0x080068B8 with the global at [0x086D7750].
//                   The command body never touches the owning entity; the entity only selects the queue it is enqueued on.
//   [2] 0x08026598  no-op (bx lr).   [3] 0x08026498  returns 1 (probably "done", so the op does not block).
//   [4] 0x0802611C  returns 0.       [7] 0x08026584  returns 1.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_SpawnDustCloud(VmContext* vm, const u8**) {
    const s32 y = Pop(vm);
    const s32 x = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    // The character must be on the map: Entity_GetByCharIndex only finds live entities (tested in-game with arbitrary indices).
    u32* command = static_cast<u32*>(ArenaAlloc(16));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025230;
        command[2] = x;
        command[3] = y;
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
