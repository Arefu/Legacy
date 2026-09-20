// BytecodeVM_EntityCmd_8025514_Toward -- action opcode 96, 0x0800A158 (0x5C bytes). LOW.
// Pops (character, target character, n) and enqueues a 20-byte command (vtable 0x08025514) carrying 32, the target entity and n * 1966. Candidate: move / turn toward another entity over a duration. IDA still calls it sub_800A158.
// Layout read from the ROM's Thumb code. The command's behaviour lives in its vtable slots (relative offsets, see vtable.h) and is not decoded here.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void BytecodeVM_EntityCmd_8025514_Toward(VmContext* vm, const u8**) {
    const s32 n = Pop(vm);
    const s32 target_index = Pop(vm);
    entities::EntityHeader* entity = Entity_GetByCharIndex(Pop(vm));
    entities::EntityHeader* target = Entity_GetByCharIndex(target_index);
    u32* command = static_cast<u32*>(ArenaAlloc(20));
    if (command != nullptr) {
        command[1] = 0;
        command[0] = 0x08025514;
        command[2] = 32;
        command[3] = reinterpret_cast<uintptr_t>(target);
        command[4] = 1966 * n;                       // 1966 = 0x7AE: a frame / angle scale that recurs in three commands
    }
    Entity_EnqueueCommand(g_CommandQueue, command, entity);
}

}  // namespace dbzlog2::vm
