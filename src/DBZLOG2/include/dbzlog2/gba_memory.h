#pragma once
// GBA memory map and the fixed RAM addresses the game uses. HIGH: every address below is read from IDA.
#include "dbzlog2/types.h"

namespace dbzlog2::mem {

// ---- regions -----------------------------------------------------------------------------------------------------
constexpr u32 kEwram = 0x02000000, kEwramSize = 0x40000;        // 256 KB external work RAM
constexpr u32 kIwram = 0x03000000, kIwramSize = 0x8000;         // 32 KB internal work RAM (code copied here: JCALG1 at 0x03000000, mixer kernels)
constexpr u32 kPalette = 0x05000000, kVram = 0x06000000, kOam = 0x07000000, kRom = 0x08000000;
constexpr u32 kSramFlash = 0x0E000000;                          // save chip: 1 Mbit Flash, commands via 0x0E005555 / 0x0E002AAA

// ---- EWRAM (0x02......) -----------------------------------------------------------------------------------------------
constexpr u32 kAudioRingA = 0x02000000, kAudioRingB = 0x02000600;   // two 1024-byte DMA-fed FIFO rings (see audio/AudioSystem_Init)
constexpr u32 kArenaBase = 0x02000800;                              // EWRAM heap starts here (Arena_Init); ~0x3F800 bytes

// ---- IWRAM globals (0x03......) -----------------------------------------------------------------------------------------
constexpr u32 kJcalg1Decoder = 0x03000000;    // the JCALG1 decompressor is copied here at boot (ARM code, 752 bytes)
constexpr u32 kIrqDispatcher = 0x03000824;    // BIOS IRQ vector target (Interrupt_Init)
constexpr u32 kPartyState = 0x03000E90;       // g_PartyState, 0x164 bytes
constexpr u32 kCurrentCoroutine = 0x03000E1C; // g_CurrentCoroutine
constexpr u32 kFrameCounter = 0x03000E20;     // g_FrameCounter, incremented by the VBlank handler
constexpr u32 kCoroutineDepth = 0x03000E24;   // g_CoroutineDepth
constexpr u32 kIntrVectorTable = 0x03000E3C;  // g_IntrVectorTable: per-interrupt handler pointers; +0x38 = pending flags for IntrWait
constexpr u32 kEntityList = 0x03001120;       // g_EntityList
constexpr u32 kPlayerObject = 0x03001FA8;     // g_PlayerObject: the player entity, 0 if none
constexpr u32 kVmContextPtr = 0x03001FAC;     // g_VMContextPtr
constexpr u32 kSaveState = 0x03001FC0;        // g_SaveState
constexpr u32 kInputSystem = 0x0300229C;      // g_InputSystem
constexpr u32 kInputCurrent = 0x030022AE;     // g_InputState (u16, active-high)
constexpr u32 kButtonState = 0x030022B0;      // g_ButtonState (u16, newly pressed this frame)
constexpr u32 kArenaFreeListHead = 0x030022B4;// g_ArenaFreeListHead (HIDWORD(g_ButtonState))
constexpr u32 kRootCoroutine = 0x030022B8;    // g_RootCoroutine
constexpr u32 kMusicPlayer = 0x030026C8;      // g_MusicChannel: 0x18-byte header + 15 channels
constexpr u32 kAudioMixer = 0x030026BC;       // g_AudioMixer
constexpr u32 kRandomState0 = 0x03003890, kRandomState1 = 0x03003894;   // Random_Next state
constexpr u32 kBiosIrqHandler = 0x03007FFC;   // BIOS reads the IRQ handler pointer from here

}  // namespace dbzlog2::mem
