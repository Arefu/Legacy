#pragma once
// Fixed-width aliases and small helpers shared by every DBZLOG2 source file.
#include <cstddef>
#include <cstdint>

namespace dbzlog2 {

using u8 = std::uint8_t;
using s8 = std::int8_t;
using u16 = std::uint16_t;
using s16 = std::int16_t;
using u32 = std::uint32_t;
using s32 = std::int32_t;
using u64 = std::uint64_t;
using s64 = std::int64_t;

// A ROM address as it appears in pointers (0x08xxxxxx). Kept as an integer so structs that mirror the ROM layout stay 4 bytes wide on
// any host.
using RomPtr = u32;

constexpr u32 kRomBase = 0x08000000;
constexpr u32 kEwramBase = 0x02000000;
constexpr u32 kIwramBase = 0x03000000;

constexpr u32 RomFileOffset(RomPtr addr) { return addr & 0x00FFFFFFu; }
constexpr RomPtr RomAddress(u32 fileOffset) { return kRomBase | fileOffset; }

}  // namespace dbzlog2
