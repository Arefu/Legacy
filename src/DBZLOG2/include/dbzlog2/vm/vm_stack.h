#pragma once
// The bytecode VM's context and operand stack. Layout read from the decompiles of BytecodeVM_ExecuteScript and the main-table handlers (HIGH).
#include "dbzlog2/types.h"

namespace dbzlog2::vm {

constexpr int kVmStackDepth = 16;      // words 1..16; there is no overflow check in the handlers

// VMContext (IDA: VMContext). Every BytecodeVM_* handler receives a pointer to it as its first argument.
struct VmContext {
    s32 stack_count;                   // +0x00  number of pushed values (BytecodeVM_ExecuteScript zeroes it)
    s32 stack[kVmStackDepth];          // +0x04  operand stack; a push is stack[stack_count++] = v, a pop is stack[--stack_count]
    s32 alt_count;                     // +0x44  loop-counter stack depth (BytecodeVM_PushToAltStack / LoopOrJump); ExecuteScript zeroes it
    s32 alt_stack[1];                  // +0x48  values (the real array is longer)
};
static_assert(offsetof(VmContext, alt_count) == 0x44);

// The state ExecuteScript keeps on its own stack and passes to every main-table handler by pointer.
struct VmExecState {
    const u8* pc;                      // +0x00  next byte to execute
    VmContext* local_ctx;              // +0x04  the caller-supplied context (or ExecuteScript's own scratch one)
};

inline s32 Pop(VmContext* vm) { return vm->stack[--vm->stack_count]; }
inline void Push(VmContext* vm, s32 value) { vm->stack[vm->stack_count++] = value; }

using MainHandler = void (*)(VmContext*, const u8** pc);   // BytecodeVM_MainDispatchTable entries
using ActionHandler = void (*)(VmContext*, const u8** pc); // BytecodeVM_OpcodeTable entries ("Step" actions)

}  // namespace dbzlog2::vm
