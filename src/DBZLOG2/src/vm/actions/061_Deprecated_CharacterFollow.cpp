// BytecodeVM_Deprecated_CharacterFollow -- action opcode 61, 0x0800A438 (0x14 bytes). HIGH.
// Never returns: drops 2 value(s) and calls AssertionFailed("CharacterFollow() is deprecated", ..., line 739).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

[[noreturn]] void AssertionFailed(const char* message, const char* file, int line, int is_assertion);   // 0x0801F878

[[noreturn]] void BytecodeVM_Deprecated_CharacterFollow(VmContext* vm, const u8**) {
    vm->stack_count -= 2;
    AssertionFailed("CharacterFollow() is deprecated", "K:\\Source\\ByteCodeInterpreter\\ByteCodeFunctionList.cpp", 739, 0);
}

}  // namespace dbzlog2::vm
