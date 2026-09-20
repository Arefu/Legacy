// BytecodeVM_Deprecated_ShowImage -- action opcode 66, 0x0800A60C (0x14 bytes). HIGH.
// Never returns: drops 1 value(s) and calls AssertionFailed("ShowImage() is deprecated", ..., line 810).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

[[noreturn]] void AssertionFailed(const char* message, const char* file, int line, int is_assertion);   // 0x0801F878

[[noreturn]] void BytecodeVM_Deprecated_ShowImage(VmContext* vm, const u8**) {
    vm->stack_count -= 1;
    AssertionFailed("ShowImage() is deprecated", "K:\\Source\\ByteCodeInterpreter\\ByteCodeFunctionList.cpp", 810, 0);
}

}  // namespace dbzlog2::vm
