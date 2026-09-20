#pragma once
// The 30 top-level script opcodes (BytecodeVM_MainDispatchTable, 0x083B5C00). One file each under src/vm/main_ops/.
#include "dbzlog2/vm/vm_stack.h"

namespace dbzlog2::vm {

enum MainOp : u8 {
    kOpPushByte = 0x00,             // operand: 1 byte, unsigned
    kOpPushVarint = 0x01,           // operand: LEB128-style varint, zigzag-signed
    kOpStep = 0x02,                 // operand: 1 byte -> BytecodeVM_OpcodeTable (the "action" opcodes)
    kOpTypeHandler = 0x03,          // operand: 1 byte -> BytecodeVM_TypeHandlerTable (pops a value, IDA: sub_8008FA4)
    kOpStackAnd = 0x04,
    kOpStackNot = 0x05,
    kOpStackNegate = 0x06,
    kOpStackAdd = 0x07,
    kOpStackSub = 0x08,
    kOpStackMul = 0x09,
    kOpStackDiv = 0x0A,
    kOpStackCmpEq = 0x0B,
    kOpStackCmpNe = 0x0C,
    kOpStackCmpGt = 0x0D,
    kOpStackCmpGe = 0x0E,
    kOpStackCmpLt = 0x0F,
    kOpStackCmpLe = 0x10,
    kOpEnd = 0x11,                  // table slot is NULL: ExecuteScript returns when it reads this
    kOpJump = 0x12,                 // operand: 1 signed byte, target = address after the operand + operand
    kOpJumpIfFalse = 0x13,          // pops; operand as Jump
    kOpStackPop = 0x14,
    kOpStepTypePostPop = 0x15,      // operand: 1 byte (action index; also the type-handler index)
    kOpStepTypePostIncPeek = 0x16,
    kOpStepTypePostPeek = 0x17,
    kOpStepTypePostPopDec = 0x18,
    kOpStepTypePostDecPeek = 0x19,
    kOpStepTypePostPeekDec = 0x1A,
    kOpPushToAltStack = 0x1B,
    kOpLoopOrJump = 0x1C,           // operand: 1 signed byte; decrement-and-branch-backward
    kOpPushMultiVarint = 0x1D,      // operand: count byte, then that many varints
};
constexpr int kMainOpCount = 30;

void BytecodeVM_PushByte(VmContext*, const u8** pc);
void BytecodeVM_PushVarint(VmContext*, const u8** pc);
void BytecodeVM_Step(VmContext*, const u8** pc);
void BytecodeVM_TypeHandler(VmContext*, const u8** pc);   // IDA: sub_8008FA4 (still to be renamed)
void BytecodeVM_StackAnd(VmContext*, const u8** pc);
void BytecodeVM_StackNot(VmContext*, const u8** pc);
void BytecodeVM_StackNegate(VmContext*, const u8** pc);
void BytecodeVM_StackAdd(VmContext*, const u8** pc);
void BytecodeVM_StackSub(VmContext*, const u8** pc);
void BytecodeVM_StackMul(VmContext*, const u8** pc);
void BytecodeVM_StackDiv(VmContext*, const u8** pc);
void BytecodeVM_StackCmpEq(VmContext*, const u8** pc);
void BytecodeVM_StackCmpNe(VmContext*, const u8** pc);
void BytecodeVM_StackCmpGt(VmContext*, const u8** pc);
void BytecodeVM_StackCmpGe(VmContext*, const u8** pc);
void BytecodeVM_StackCmpLt(VmContext*, const u8** pc);
void BytecodeVM_StackCmpLe(VmContext*, const u8** pc);
void BytecodeVM_Jump(VmContext*, const u8** pc);
void BytecodeVM_JumpIfFalse(VmContext*, const u8** pc);
void BytecodeVM_StackPop(VmContext*, const u8** pc);
void BytecodeVM_StepTypePost_Pop(VmContext*, const u8** pc);
void BytecodeVM_StepTypePost_IncPeek(VmContext*, const u8** pc);
void BytecodeVM_StepTypePost_Peek(VmContext*, const u8** pc);
void BytecodeVM_StepTypePost_PopDec(VmContext*, const u8** pc);
void BytecodeVM_StepTypePost_DecPeek(VmContext*, const u8** pc);
void BytecodeVM_StepTypePost_PeekDec(VmContext*, const u8** pc);
void BytecodeVM_PushToAltStack(VmContext*, const u8** pc);
void BytecodeVM_LoopOrJump(VmContext*, const u8** pc);
void BytecodeVM_PushMultiVarint(VmContext*, const u8** pc);

// The interpreter loop: BytecodeVM_ExecuteScript, 0x080092E4.
void BytecodeVM_ExecuteScript(VmContext* ctx, const u8* pc, VmContext* local_ctx);

extern const MainHandler g_BytecodeVM_MainDispatchTable[kMainOpCount];      // 0x083B5C00, slot 0x11 is nullptr
extern const ActionHandler g_BytecodeVM_OpcodeTable[];                      // 0x083B5D28
extern const ActionHandler g_BytecodeVM_TypeHandlerTable[];                 // 0x083B5F70 (handlers take the VALUE popped / peeked as r0)
extern VmContext* g_VMContextPtr;                                           // 0x03001FAC

}  // namespace dbzlog2::vm
