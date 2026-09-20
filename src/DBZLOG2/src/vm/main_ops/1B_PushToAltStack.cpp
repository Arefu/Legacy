// BytecodeVM_PushToAltStack -- main opcode 0x1B, 0x08009250 (0x1C bytes). HIGH.
// Pops a value and pushes it on the alternate (loop-counter) stack at ctx + 0x44.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_PushToAltStack(VmContext* vm, const u8**) {
    const s32 value = Pop(vm);
    vm->alt_stack[vm->alt_count++] = value;
}

}  // namespace dbzlog2::vm
