// BytecodeVM_UploadSpritePalette -- action opcode 45, 0x08009BE4 (0x14 bytes). HIGH.
// DMA3-copies the shared OBJ palette (g_ObjPalette, 0x081DA6C8, 256 colours) into sprite palette RAM (0x05000200).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

extern const u16 g_ObjPalette[256];   // 0x081DA6C8
void Dma3Copy(const void* src, void* dst, u32 halfwords);

void BytecodeVM_UploadSpritePalette(VmContext*, const u8**) {
    Dma3Copy(g_ObjPalette, reinterpret_cast<void*>(0x05000200), 256);   // DMA3SAD / DAD, count 256 halfwords, enable
}

}  // namespace dbzlog2::vm
