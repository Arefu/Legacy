// BytecodeVM_StepTypePost_Peek -- main opcode 0x17, 0x08009190 (0x2C bytes). HIGH.
// Operand byte n: runs action n (BytecodeVM_OpcodeTable[n]) to compute a value / address, then applies peek the top, then handler(top + 1) and calls type handler n (BytecodeVM_TypeHandlerTable[n]) with the result. Used for assignments and ++ / -- on variables.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_StepTypePost_Peek(VmContext* vm, const u8** pc) {
    const u8 n = *(*pc)++;
    g_BytecodeVM_OpcodeTable[n](vm, pc);
    const s32 value = vm->stack[vm->stack_count - 1];
    reinterpret_cast<void (*)(s32)>(g_BytecodeVM_TypeHandlerTable[n])(value + 1);   // TODO(unknown): the handler's full prototype (it also sees vm / pc)
}

}  // namespace dbzlog2::vm
