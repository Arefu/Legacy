// BytecodeVM_TypeHandler -- main opcode 0x03, 0x08008FA4 (0x22 bytes). HIGH.
// IDA still calls this sub_8008FA4. Pops a value and hands it to BytecodeVM_TypeHandlerTable[operand] (get / set of a variable).
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_TypeHandler(VmContext* vm, const u8** pc) {
    const u8 type = *(*pc)++;
    const s32 value = Pop(vm);
    // The type handlers take the popped value in r0 (a1) and the context / pc in the following registers.
    reinterpret_cast<void (*)(s32, VmContext*, const u8**)>(g_BytecodeVM_TypeHandlerTable[type])(value, vm, pc);   // TODO(unknown): confirm handler prototype
}

}  // namespace dbzlog2::vm
