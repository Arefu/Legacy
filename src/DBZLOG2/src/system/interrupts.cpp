// Interrupt_Init 0x08020D26, IntrWait 0x08020D40, Hardware_Shutdown 0x08020B8C, SoftReset 0x0802135C. HIGH (Interrupt_Init, Hardware_Shutdown),
// MEDIUM (IntrWait: a custom IntrWait with a frame-time estimate; SVC 2 is the BIOS Halt).
#include "dbzlog2/gba_io.h"
#include "dbzlog2/gba_memory.h"
#include "dbzlog2/types.h"

namespace dbzlog2::system {

extern u32 g_BiosIrqHandler;      // 0x03007FFC
extern u32 g_IntrVectorTable[];   // 0x03000E3C; [14] (+0x38) = pending interrupt flags set by the handlers
void Halt();                      // BIOS SWI 2

void Interrupt_Init() {
    g_BiosIrqHandler = mem::kIrqDispatcher;
    io::IE = 0x2000;              // Game Pak interrupt only (the flash driver); VBlank is added by GameLoop_Init
    io::IME = 1;
    io::DISPSTAT = io::kDispstatVBlankIrq | io::kDispstatHBlankIrq;   // 0x18
}

// Waits until one of the interrupts in `mask` has fired (flag bits are set in the vector table's pending word by the handlers).
// With mask & 1 (VBlank) it also stores an estimate of the elapsed frame fraction in word 15 (100 = a whole frame).
int IntrWait(u32* vector_table, u32 mask) {
    u32* pending = &vector_table[14];
    if (mask & 1) {
        if (*pending & 1) {
            vector_table[15] = 100;                      // already late: a full frame
        } else {
            const u32 line = io::VCOUNT;                 // 0..227; 160..227 is VBlank
            vector_table[15] = (28900u * (line - 160 + (line < 0xA0 ? 0xE3 : 0))) >> 16;
        }
    }
    *pending = 0;
    do { Halt(); } while ((*pending & mask) == 0);
    *pending &= ~mask;
    return static_cast<int>(*pending);
}

void Hardware_Shutdown() {
    IntrWait(g_IntrVectorTable, 0x400);                  // wait for the Timer1/DMA sound interrupt bit
    io::TM0CNT_H = 0;                                    // stop the sound timer
    io::DMA1CNT_H = 0;                                   // stop both direct-sound DMAs
    io::DMA2CNT_H = 0;
    io::SOUNDCNT_X = 0;                                  // master sound off
}

void SoftReset() {
    Hardware_Shutdown();
    io::IME = 0;
    io::DISPCNT = 0x80;                                  // forced blank
    io::BG0HOFS = io::BG0VOFS = io::BG1HOFS = io::BG1VOFS = 0;
    io::BG2HOFS = io::BG2VOFS = io::BG3HOFS = io::BG3VOFS = 0;
    // then BIOS SoftReset (SWI 0); the decompile shows it as a bogus linux syscall
}

}  // namespace dbzlog2::system
