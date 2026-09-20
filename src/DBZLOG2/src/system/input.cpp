// Input_Init 0x0801EF56 and Input_Poll 0x0801EEFC. HIGH. Reads the key register once per frame.
// The g_InputSystem block (0x0300229C): +0 history head, +1 history-armed flag, +2 history[8] (u16 newly-pressed masks),
// +0x12 current (active-high), +0x14 newly pressed, +0x16 released.
#include "dbzlog2/gba_io.h"
#include "dbzlog2/gba_memory.h"
#include "dbzlog2/types.h"

namespace dbzlog2::system {

struct InputSystem {
    u8 history_head;         // +0x00
    u8 history_armed;        // +0x01  set by the frontend once a key has been pressed; the soft-reset chord only works after that
    u16 history[8];          // +0x02  newly-pressed masks of the last 8 pressed frames
    u16 current;             // +0x12  keys down (active-high, i.e. ~KEYINPUT)
    u16 pressed;             // +0x14  keys that went down since the last poll
    u16 released;            // +0x16  keys that went up since the last poll
};

extern u32 g_LastInputFrame;   // 0x03002298: frame of the last key press
extern u32 g_FrameCounter;     // 0x03000E20
void SoftReset();              // 0x0802135C

void Input_Init(InputSystem* in) {
    in->history_head = 0;
    in->current = 0;
    in->pressed = 0;
    in->released = 0;
    in->history_armed = 0;
}

void Input_Poll(InputSystem* in) {
    const u16 now = static_cast<u16>(~io::KEYINPUT);
    const u16 changed = in->current ^ now;
    const u16 pressed = now & changed;
    const u16 released = in->current & changed;
    in->pressed = pressed;
    in->released = released;
    in->current = now;
    if (pressed != 0) {
        in->history[in->history_head++] = pressed;
        if (in->history_head >= 8) in->history_head = 0;
        g_LastInputFrame = g_FrameCounter;
    }
    // A + B + Select + Start all held, with at least one of them newly pressed this frame: soft reset.
    if ((~in->current & 0xF) == 0 && in->history_armed != 0 && (in->released << 28) != 0) SoftReset();
}

}  // namespace dbzlog2::system
