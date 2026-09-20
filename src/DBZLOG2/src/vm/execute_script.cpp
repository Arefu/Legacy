// BytecodeVM_ExecuteScript -- 0x080092E4 (0x38 bytes). HIGH. The interpreter loop: reads an opcode byte, looks it up in
// BytecodeVM_MainDispatchTable and calls it with (context, &pc). Opcode 0x11's table slot is NULL: reaching it ends the script.
#include "dbzlog2/vm/main_ops.h"

namespace dbzlog2::vm {

void BytecodeVM_ExecuteScript(VmContext* ctx, const u8* pc, VmContext* local_ctx) {
    VmContext scratch;                               // used when the caller passes no context
    VmExecState state{pc, local_ctx};
    if (local_ctx == nullptr) state.local_ctx = &scratch;
    g_VMContextPtr = state.local_ctx;
    ctx->stack_count = 0;
    ctx->alt_count = 0;                              // IDA: unk_44
    for (;;) {
        const u8 opcode = *state.pc++;
        const MainHandler handler = g_BytecodeVM_MainDispatchTable[opcode];
        if (handler == nullptr) break;               // opcode 0x11 = end
        handler(ctx, &state.pc);
    }
}
