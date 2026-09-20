// BytecodeVM_PushMultiVarint -- main opcode 0x1D, 0x0800929A (0x40 bytes). HIGH.
// Operand: a count byte, then that many zigzag varints, each pushed in order.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_PushMultiVarint(VmContext* vm, const u8** pc) {
    u8 remaining = *(*pc)++;
    do {
        s32 raw = 0;
        s8 byte;
        do {
            byte = static_cast<s8>(*(*pc)++);
            raw = (raw << 7) + (byte & 0x7F);
        } while (byte < 0);
        Push(vm, (raw & 1) ? -(raw >> 1) : (raw >> 1));
        --remaining;
    } while (remaining != 0);
}

}  // namespace dbzlog2::vm
