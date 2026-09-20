// BytecodeVM_Step -- main opcode 0x02, 0x08008F8E (0x16 bytes). HIGH.
// Runs the action handler BytecodeVM_OpcodeTable[operand] (see src/vm/actions/).
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_Step(VmContext* vm, const u8** pc) {
    const u8 action = *(*pc)++;
    g_BytecodeVM_OpcodeTable[action](vm, pc);
}

}  // namespace dbzlog2::vm
