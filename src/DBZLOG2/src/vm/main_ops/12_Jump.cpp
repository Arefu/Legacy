// BytecodeVM_Jump -- main opcode 0x12, 0x080090FE (0xC bytes). HIGH.
// Operand is ONE SIGNED BYTE (not a varint): target = address after the operand + operand.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_Jump(VmContext*, const u8** pc) {
    *pc = *pc + static_cast<s8>(**pc) + 1;
}

}  // namespace dbzlog2::vm
