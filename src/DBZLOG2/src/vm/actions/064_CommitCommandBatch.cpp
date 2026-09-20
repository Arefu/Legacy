// BytecodeVM_CommitCommandBatch -- action opcode 64, 0x0800A5B2 (0xC bytes). MEDIUM.
// Calls sub_0800E038(&g_CommandQueue): releases the batch started by BeginCommandBatch.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void CommandQueue_CommitBatch(void* queue);   // 0x0800E038 (IDA: sub_800E038)

void BytecodeVM_CommitCommandBatch(VmContext*, const u8**) {
    CommandQueue_CommitBatch(g_CommandQueue);
}

}  // namespace dbzlog2::vm
