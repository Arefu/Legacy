// GameLoop_Init 0x0801F248, GameLoop_Tick 0x0801F294, GameLoop_VBlankHandler 0x0801F230, Coroutine_SwitchTo 0x0801F2CE,
// GameLoop_ExecuteAndWait 0x0801F2F4. HIGH. The whole game is a stack of "coroutine" objects with a 5-slot relative vtable:
//   +0x00 (unused here)  +0x04 enter  +0x08 leave  +0x0C update (before VBlank)  +0x10 vblank  +0x14 late update (nonzero = finished).
#include "dbzlog2/gba_io.h"
#include "dbzlog2/gba_memory.h"
#include "dbzlog2/types.h"

namespace dbzlog2::system {

struct Coroutine { s32 vtable[6]; };

extern Coroutine* g_CurrentCoroutine;     // 0x03000E1C
extern Coroutine g_RootCoroutine;         // 0x030022B8
extern u32 g_CoroutineDepth;              // 0x03000E24 (a small stack of saved coroutine pointers follows g_CurrentCoroutine)
extern Coroutine* g_CoroutineStack[];     // (&g_CurrentCoroutine)[3 + depth]
extern u32 g_FrameCounter;
extern u32 g_IntrVectorTable[];
extern void* g_InputSystem;
extern void* g_AudioMixer;

int CallSlot(Coroutine* c, int slot);     // Ptr_InvokeHandler on c->vtable[slot]: function = vtable address + word
void Input_Init(void*);
void Input_Poll(void*);
void Arena_Init();
void Interrupt_Init();
void AudioSystem_Init(void*);
void AudioMixer_Init(void*);
void Audio_MixerUpdate(void*);
int IntrWait(u32*, u32);
void* ArenaAlloc(int size);

void* GameLoop_Init(void* memory) {
    if (memory == nullptr) {
        memory = ArenaAlloc(1);
        if (memory == nullptr) return nullptr;
    }
    Input_Init(g_InputSystem);
    Arena_Init();                                   // IDA still named this OAM_Init before 2026-09-20
    Interrupt_Init();
    g_IntrVectorTable[0] = reinterpret_cast<uintptr_t>(nullptr);   // TODO: GameLoop_VBlankHandler address (Thumb) goes here
    io::IE |= io::kIrqVBlank;
    AudioSystem_Init(g_AudioMixer);
    AudioMixer_Init(&g_AudioMixer);                 // 0x0300389C in the ROM (sound-bank streaming from flash)
    Input_Poll(g_InputSystem);
    return memory;
}

void GameLoop_VBlankHandler() {
    ++g_FrameCounter;
    CallSlot(g_CurrentCoroutine, 4);                // vtable slot +0x10
}

int GameLoop_Tick() {
    int result;
    do {
        CallSlot(g_CurrentCoroutine, 3);            // +0x0C update
        Audio_MixerUpdate(g_AudioMixer);
        IntrWait(g_IntrVectorTable, 1);             // wait for VBlank
        Input_Poll(g_InputSystem);
        result = CallSlot(g_CurrentCoroutine, 5);   // +0x14 late update; nonzero ends the coroutine
    } while (result == 0);
    return result;
}

void Coroutine_SwitchTo(Coroutine* next) {
    Coroutine* old = g_CurrentCoroutine;
    g_CurrentCoroutine = &g_RootCoroutine;
    CallSlot(old, 2);                               // +0x08 leave
    CallSlot(next, 1);                              // +0x04 enter
    g_CurrentCoroutine = next;
}

int GameLoop_ExecuteAndWait(Coroutine* child) {
    g_CoroutineStack[g_CoroutineDepth++] = g_CurrentCoroutine;   // remember the caller
    g_CurrentCoroutine = &g_RootCoroutine;
    CallSlot(child, 1);                             // enter
    g_CurrentCoroutine = child;
    const int result = GameLoop_Tick();
    if (g_CoroutineDepth != 0) {
        Coroutine* finished = g_CurrentCoroutine;
        g_CurrentCoroutine = &g_RootCoroutine;
        CallSlot(finished, 2);                      // leave
        g_CurrentCoroutine = g_CoroutineStack[--g_CoroutineDepth];
    }
    return result;
}

}  // namespace dbzlog2::system
