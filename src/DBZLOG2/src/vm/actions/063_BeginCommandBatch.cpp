// BytecodeVM_BeginCommandBatch -- action opcode 63, 0x0800A5A6 (0xC bytes). MEDIUM.
// Calls sub_0800E032(&g_CommandQueue): starts a batch so following entity commands are held (committed together by CommitCommandBatch, opcode 64). IDA still names the callee sub_800E032.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void CommandQueue_BeginBatch(void* queue);    // 0x0800E032 (IDA: sub_800E032)

void BytecodeVM_BeginCommandBatch(VmContext*, const u8**) {
    CommandQueue_BeginBatch(g_CommandQueue);
}

}  // namespace dbzlog2::vm
