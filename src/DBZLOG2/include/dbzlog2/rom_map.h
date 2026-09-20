#pragma once
// Addresses of tables and globals identified in the US ROM (see DBZKit/*.md). Every entry names the IDA symbol it comes from.
// HIGH = verified against the ROM data, MEDIUM = read from the decompile only.
#include "dbzlog2/types.h"

namespace dbzlog2::rom {

// ---- maps / scripting (Engine-Notes.md) -----------------------------------------------------------------------------
constexpr RomPtr kMapEntries = 0x08409368;             // g_MapEntries, 327 x 0x38 bytes (HIGH)
constexpr u32 kMapEntrySize = 0x38;
constexpr u32 kMapEntryCount = 327;                    // Map_GetCount is the constant 327
constexpr RomPtr kBytecodeMainDispatchTable = 0x083B5C00;   // BytecodeVM_MainDispatchTable, 30 entries (HIGH)
constexpr RomPtr kBytecodeOpcodeTable = 0x083B5D28;         // BytecodeVM_OpcodeTable, ~146 "Step" actions (HIGH)
constexpr RomPtr kBytecodeTypeHandlerTable = 0x083B5F70;    // BytecodeVM_TypeHandlerTable (HIGH)

// ---- characters / sprites -------------------------------------------------------------------------------------------
constexpr RomPtr kCharacterSpriteIndex = 0x083B4E74;   // g_CharacterSpriteIndex: u32[] -> sprite records (HIGH)
constexpr RomPtr kObjPalette = 0x081DA6C8;             // g_ObjPalette (HIGH)

// ---- audio (Audio-Notes.md) ---------------------------------------------------------------------------------------
constexpr RomPtr kSfxTable = 0x080E15B0;               // g_SFXDefTable, 64 x SfxSample (12 bytes) (HIGH)
constexpr u32 kSfxCount = 64;
constexpr RomPtr kMusicTrackTable = 0x081D4B2C;        // g_MusicTrackTable, 44 x MusicTrackInfo (20 bytes) (HIGH)
constexpr u32 kMusicTrackCount = 44;
constexpr RomPtr kInstrumentBank = 0x0814A8A4;         // g_InstrumentBank, 101 x SampleInstrument (12 bytes) (HIGH)
constexpr u32 kInstrumentCount = 101;
constexpr RomPtr kEventPresets = 0x0814AD60;           // g_EventPresets, u8[14] (HIGH)
constexpr RomPtr kNoteFrequencyTable = 0x087FC93C;     // g_NoteFrequencyTable, u32[104] = 512*2^(n/12) (HIGH)
constexpr u32 kNoteCount = 104;
constexpr RomPtr kFineSlideUpTable = 0x087FCB9C;       // g_FineSlideUpTable, u16[64] (HIGH)
constexpr RomPtr kSlideUpTable = 0x087FCC1C;           // g_SlideUpTable, u16[64] (HIGH)
constexpr RomPtr kFineSlideDownTable = 0x087FCE1C;     // g_FineSlideDownTable, u16[64] (HIGH)
constexpr RomPtr kSlideDownTable = 0x087FCE9C;         // g_SlideDownTable, u16[64] (HIGH)
constexpr RomPtr kVibratoSineTable = 0x087FD09C;       // g_VibratoSineTable, s8[64] (HIGH)
constexpr RomPtr kAudioEffectTable = 0x087FCB1C;       // Audio_EffectTable, function pointers indexed by effect id (HIGH)

}  // namespace dbzlog2::rom
