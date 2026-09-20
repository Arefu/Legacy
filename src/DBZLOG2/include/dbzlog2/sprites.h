#pragma once
// Character sprite resources (ROM). Layouts verified in the ROM and against DBZKit's SpriteDump / FrameImport (HIGH unless noted).
#include "dbzlog2/types.h"

namespace dbzlog2::sprites {

// Every compressed-or-raw blob starts with this (Resource_LoadOrDecompress, 0x0801F396): mode 0 = raw memcpy, 1 / 2 = JCALG1.
struct ResourceHeader {
    u32 mode;    // +0x00
    u32 size;    // +0x04  decompressed size in bytes
    // u8 data[]  +0x08
};
static_assert(sizeof(ResourceHeader) == 8);

// One animation frame (12 bytes). A record slot points at an ARRAY of these, one per frame; the frame shown is
// `*(record + 0x4C + 4 * slot) + 12 * frameIndex` (sub_0800775A, MEDIUM name).
struct FrameDescriptor {
    s8 x_offset;       // +0x00
    s8 y_offset;       // +0x01
    u8 width;          // +0x02  pixels; with height must be a GBA OAM shape (8x8 .. 64x64, 16x8 .. 32x64)
    u8 height;         // +0x03
    u16 oam_attr0;     // +0x04  0x2000 (256 colours) | shape << 14
    u16 oam_attr1;     // +0x06  size << 14 | 0x1000 when horizontally flipped
    RomPtr tiles;      // +0x08  ResourceHeader of 8x8 tiles, 64 bytes each, row-major tile order, palette = g_ObjPalette, index 0 transparent
};
static_assert(sizeof(FrameDescriptor) == 12);

constexpr u32 kSpriteRecordHeaderSize = 0x4C;   // the header holds hit-box style rectangles (int32 x, y, w, h groups); not decoded yet
constexpr u32 kFrameGroupSize = 16;             // 4 slots x 4 bytes: Down, Up, Left, Right (stock Right = Left flipped, sharing the tile blob)
constexpr u32 kBaseGroupCount = 21;             // a base record (0x1A0 bytes) holds groups 0..20; some sprites have 22-23

// Sprite record (variable length, NOT a fixed size): header (0x4C bytes) then `groups` x kFrameGroupSize bytes of frame-array pointers.
// A slot is 0 or a pointer to a FrameDescriptor[]. The record ends at the first non-zero word that is not such a pointer. Known groups:
//   0-4 walk / stand poses    5 Ki Blast, Big Bang, Burning, Masenko, Scatter Shot (ability animation reads record + 0x9C)
//   18 Kamehameha, Special Beam, Sword Blast (+0x16C)    19 Spirit Bomb (+0x17C)    20+ extra frames (idle / blink?  not confirmed)
constexpr u32 kSpriteGroupKiBlast = 5;
constexpr u32 kSpriteGroupBeam = 18;
constexpr u32 kSpriteGroupSpiritBomb = 19;

// g_CharacterSpriteIndex (file 0x3B4E74): u32 record pointers, ids 3..156 in use. It is reached through ONE literal (file 0x959C, value table + 7 words)
// in Character_GetSpriteRecord, so it can be relocated to make more ids (DBZKit's FrameImport.CloneSprite does).
constexpr u32 kSpriteIdFirstNpc = 7;   // ids 1..6 are the party slots, 0 = the player entity, >= 7 index the table

}  // namespace dbzlog2::sprites
