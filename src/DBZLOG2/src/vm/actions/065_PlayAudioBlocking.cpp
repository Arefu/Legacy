// BytecodeVM_PlayAudioBlocking -- action opcode 65, 0x0800A5BE (0x4E bytes). MEDIUM.
// Pops TWO values (a, b). Plays sound effect 43 at volume 32 (fixed), then builds a generic command with sub_0800DED0(a, b) (vtable 0x08024950) and blocks on it; afterwards the effect's voice is deactivated (pending bit 0 cleared). Real usage: PlayAudioBlocking(50, 180) in Z1A1's item dialog. The arguments are NOT the sound id; meaning (duration / priority?) unconfirmed.
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void* TimedCommand_Create(void* memory, s32 a, s32 b);   // 0x0800DED0 (IDA: sub_800DED0)
struct AudioVoiceView { u32 words[4]; };   // pending byte is at +0x10

void BytecodeVM_PlayAudioBlocking(VmContext* vm, const u8**) {
    const s32 b = Pop(vm);
    const s32 a = Pop(vm);
    u8* voice = static_cast<u8*>(SFX_Queue(g_SFXPool, &g_SFXDefTable[43], 32, 0));
    GameLoop_ExecuteAndWait(TimedCommand_Create(nullptr, b, a));   // TODO(unknown): argument order of the constructor (IDA: (nullptr, v2, v3) with v2 = top of stack)
    if (voice != nullptr) voice[16] = static_cast<u8>(2 * (voice[16] >> 1));   // clear the 'active' bit
}

}  // namespace dbzlog2::vm
