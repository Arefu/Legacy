// BytecodeVM_MainDispatchTable -- 0x083B5C00, 30 entries (0x00-0x1D); slot 0x11 is NULL (the end-of-script marker). HIGH (read from the ROM).
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

const MainHandler g_BytecodeVM_MainDispatchTable[kMainOpCount] = {
    BytecodeVM_PushByte, BytecodeVM_PushVarint, BytecodeVM_Step, BytecodeVM_TypeHandler,                  // 0x00-0x03
    BytecodeVM_StackAnd, BytecodeVM_StackNot, BytecodeVM_StackNegate, BytecodeVM_StackAdd,                // 0x04-0x07
    BytecodeVM_StackSub, BytecodeVM_StackMul, BytecodeVM_StackDiv, BytecodeVM_StackCmpEq,                 // 0x08-0x0B
    BytecodeVM_StackCmpNe, BytecodeVM_StackCmpGt, BytecodeVM_StackCmpGe, BytecodeVM_StackCmpLt,           // 0x0C-0x0F
    BytecodeVM_StackCmpLe, nullptr, BytecodeVM_Jump, BytecodeVM_JumpIfFalse,                              // 0x10-0x13
    BytecodeVM_StackPop, BytecodeVM_StepTypePost_Pop, BytecodeVM_StepTypePost_IncPeek, BytecodeVM_StepTypePost_Peek,   // 0x14-0x17
    BytecodeVM_StepTypePost_PopDec, BytecodeVM_StepTypePost_DecPeek, BytecodeVM_StepTypePost_PeekDec, BytecodeVM_PushToAltStack,   // 0x18-0x1B
    BytecodeVM_LoopOrJump, BytecodeVM_PushMultiVarint,                                                    // 0x1C-0x1D
};

}  // namespace dbzlog2::vm
