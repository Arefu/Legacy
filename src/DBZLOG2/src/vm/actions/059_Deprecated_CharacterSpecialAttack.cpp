// BytecodeVM_Deprecated_CharacterSpecialAttack -- action opcode 59, 0x0800A084 (0x14 bytes). HIGH.
// Never returns: drops two values and calls AssertionFailed("CharacterSpecialAttack() is deprecated", "K:\\Source\\ByteCodeInterpreter\\ByteCodeFunctionList.cpp", line 642). The original source path shows the game was built from a ByteCodeInterpreter module.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

[[noreturn]] void AssertionFailed(const char* message, const char* file, int line, int is_assertion);   // 0x0801F878

[[noreturn]] void BytecodeVM_Deprecated_CharacterSpecialAttack(VmContext* vm, const u8**) {
    vm->stack_count -= 2;
    AssertionFailed("CharacterSpecialAttack() is deprecated", "K:\\Source\\ByteCodeInterpreter\\ByteCodeFunctionList.cpp", 642, 0);
}

}  // namespace dbzlog2::vm
