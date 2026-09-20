// BytecodeVM_StepTypePost_Pop -- main opcode 0x15, 0x08009130 (0x30 bytes). HIGH.
// Operand byte n: runs action n (BytecodeVM_OpcodeTable[n]) to compute a value / address, then applies pop the result, then handler(value + 1) and calls type handler n (BytecodeVM_TypeHandlerTable[n]) with the result. Used for assignments and ++ / -- on variables.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_StepTypePost_Pop(VmContext* vm, const u8** pc) {
    const u8 n = *(*pc)++;
    g_BytecodeVM_OpcodeTable[n](vm, pc);
    const s32 value = Pop(vm);
    reinterpret_cast<void (*)(s32)>(g_BytecodeVM_TypeHandlerTable[n])(value + 1);   // TODO(unknown): the handler's full prototype (it also sees vm / pc)
}

}  // namespace dbzlog2::vm
