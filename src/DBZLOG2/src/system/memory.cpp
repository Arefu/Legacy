// memset_impl 0x08021F18 / memset_aligned_core 0x08022C10 (MEDIUM: aligned core not transcribed), Arena_Init 0x0801EFA4, ArenaAlloc 0x0801F08C,
// Arena_UnlinkFreeBlock 0x0801EFBC. HIGH for the structure. The EWRAM heap is a doubly-linked list of blocks addressed by 16-bit WORD offsets
// from 0x02000000. Block header (8 bytes): u16 prev, u16 size_words, u16 next, u8 used, pad. Free blocks are kept in a list whose head is
// g_ArenaFreeListHead (0x030022B4). ArenaAlloc has NO out-of-memory guard (a request larger than any free block dereferences null).
#include "dbzlog2/gba_io.h"
#include "dbzlog2/gba_memory.h"
#include "dbzlog2/types.h"

namespace dbzlog2::system {

struct ArenaBlock {
    u16 prev;         // +0  word offset of the previous block (0 = none)
    u16 size_words;   // +2  payload size in 4-byte words
    u16 next;         // +4  word offset of the next block (0 = none)
    u8 used;          // +6
    u8 pad;           // +7
};
static_assert(sizeof(ArenaBlock) == 8);

extern ArenaBlock* g_ArenaFreeListHead;   // 0x030022B4

inline ArenaBlock* BlockAt(u16 word_offset) { return reinterpret_cast<ArenaBlock*>(mem::kEwram + 4u * word_offset); }

// Fills `count` bytes at dst with `value` (byte writes up to a 4-byte boundary, then 32-bit stores via memset_aligned_core).
void* memset_impl(u8* dst, u8 value, u32 count) {
    if (count < 4) { while (count--) *dst++ = value; return dst; }
    u32 head = (4 - (reinterpret_cast<uintptr_t>(dst) & 3)) & 3;
    for (u32 i = 0; i < head; ++i) *dst++ = value;
    count -= head;
    const u32 fill = value * 0x01010101u;
    u32* words = reinterpret_cast<u32*>(dst);
    for (u32 i = 0; i < count / 4; ++i) words[i] = fill;
    for (u32 i = count & ~3u; i < count; ++i) dst[i] = value;
    return dst;
}

void Arena_Init() {
    ArenaBlock* first = reinterpret_cast<ArenaBlock*>(mem::kArenaBase);
    g_ArenaFreeListHead = first;
    first->prev = 0;
    first->size_words = 0xFDFE;                      // one free block covering the rest of EWRAM
    first->next = 0;
    first->used = 0;
}

}  // namespace dbzlog2::system
