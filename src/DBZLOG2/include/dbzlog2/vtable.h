#pragma once
// The game's vtables are arrays of RELATIVE offsets: the function for slot n is `vtable_address + vtable[n]` (Thumb, so bit 0 is set in the
// stored value on the GBA). Ptr_InvokeHandler (0x08021D2C) is just `bx r2`: call the function in r2 with r0 / r1 as arguments.
// A buildable ROM either keeps these tables (the builder computes the offsets from symbol addresses) or replaces the indirection with direct
// calls; see docs/roadmap.md.
#include "dbzlog2/types.h"

namespace dbzlog2 {

// Address (in the game's address space) of the function in `slot` of the vtable that starts at `vtable`.
inline u32 VTableSlotAddress(u32 vtable, const s32* words, int slot) { return vtable + static_cast<u32>(words[slot]); }

}  // namespace dbzlog2
