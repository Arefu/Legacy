// BytecodeVM_PushVarint -- main opcode 0x01, 0x08008F5A (0x34 bytes). HIGH.
// Varint: 7 bits per byte, big-endian groups, high bit = continue; the result is zigzag-decoded (bit 0 = sign).
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_PushVarint(VmContext* vm, const u8** pc) {
    s32 raw = 0;
    s8 byte;
    do {
        byte = static_cast<s8>(*(*pc)++);
        raw = (raw << 7) + (byte & 0x7F);
    } while (byte < 0);
    const s32 value = (raw & 1) ? -(raw >> 1) : (raw >> 1);
    Push(vm, value);
}

}  // namespace dbzlog2::vm
