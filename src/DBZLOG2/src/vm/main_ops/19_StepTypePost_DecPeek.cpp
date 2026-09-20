// BytecodeVM_StepTypePost_DecPeek -- main opcode 0x19, 0x080091EC (0x30 bytes). HIGH.
// Operand byte n: runs action n (BytecodeVM_OpcodeTable[n]) to compute a value / address, then applies decrement the top in place, then handler(new top) and calls type handler n (BytecodeVM_TypeHandlerTable[n]) with the result. Used for assignments and ++ / -- on variables.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_StepTypePost_DecPeek(VmContext* vm, const u8** pc) {
    const u8 n = *(*pc)++;
    g_BytecodeVM_OpcodeTable[n](vm, pc);
    s32* top = &vm->stack[vm->stack_count - 1];
    *top -= 1;
    reinterpret_cast<void (*)(s32)>(g_BytecodeVM_TypeHandlerTable[n])(*top);   // TODO(unknown): the handler's full prototype (it also sees vm / pc)
}

}  // namespace dbzlog2::vm
