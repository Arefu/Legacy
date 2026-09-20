// BytecodeVM_PushByte -- main opcode 0x00, 0x08008F44 (0x16 bytes). HIGH.
// Pushes the next byte (unsigned) as a value.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_PushByte(VmContext* vm, const u8** pc) {
    const u8 value = *(*pc)++;
    Push(vm, value);
}

}  // namespace dbzlog2::vm
