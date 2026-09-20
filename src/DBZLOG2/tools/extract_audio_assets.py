#!/usr/bin/env python3
"""Export every audio data block of the US ROM to .bin files plus a manifest.

    python extract_audio_assets.py <rom.gba> [out_dir]      (default out_dir = ../assets)

Layout of the output:

    assets/audio/manifest.json          every block: name, rom address, size, kind, and what points at it
    assets/audio/tables/*.bin           the fixed tables (SFX table, instrument bank, track table, note/slide/vibrato tables, presets)
    assets/audio/sfx/sfx_NN.bin         signed 8-bit PCM of each sound effect
    assets/audio/instruments/inst_NNN.bin
    assets/audio/music/trackNN/{order.bin,pattern_table.bin,patternPP.bin}

The manifest is what the devkit's ROM builder will consume: blocks are placed by name, and the pointer fields listed in
"pointers" are relocated when a block moves. All layouts are HIGH certainty (see DBZKit/Audio-Notes.md); the same tables are
read by DBZKit's DrGero/Audio.cs.
"""
import json
import os
import struct
import sys

SFX_TABLE, SFX_COUNT = 0x0E15B0, 64
TRACK_TABLE, TRACK_COUNT = 0x1D4B2C, 44
NOTE_TABLE, NOTE_COUNT = 0x7FC93C, 104
SLIDE_TABLES = {"fine_slide_up": 0x7FCB9C, "slide_up": 0x7FCC1C, "fine_slide_down": 0x7FCE1C, "slide_down": 0x7FCE9C}
SINE_TABLE = 0x7FD09C
EFFECT_TABLE, EFFECT_COUNT = 0x7FCB1C, 32


def is_rom_ptr(v, rom):
    return 0x08000000 <= v < 0x0A000000 and (v & 0xFFFFFF) < len(rom)


def off(v):
    return v & 0xFFFFFF


class Exporter:
    def __init__(self, rom, out):
        self.rom, self.out, self.blocks = rom, out, []

    def block(self, rel, addr, size, kind, **extra):
        path = os.path.join(self.out, "audio", rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as f:
            f.write(self.rom[addr:addr + size])
        self.blocks.append({"file": "audio/" + rel.replace("\\", "/"), "rom_address": 0x08000000 | addr, "size": size, "kind": kind, **extra})

    def u32(self, a):
        return struct.unpack_from("<I", self.rom, a)[0]

    def u16(self, a):
        return struct.unpack_from("<H", self.rom, a)[0]


def parse_pattern(rom, addr, presets):
    """Returns the size in bytes of one pattern (u8 rows, then per row events ended by a 0 byte)."""
    o = addr
    rows = rom[o]
    o += 1
    last = [0] * 16
    for _ in range(rows):
        while True:
            b = rom[o]
            o += 1
            if b == 0:
                break
            ch, hi = b & 15, b >> 4
            if hi == 0:
                flags = last[ch] = rom[o]
                o += 1
            elif hi == 1:
                flags = last[ch]
            else:
                flags = presets[hi - 2]
            o += (1 if flags & 1 else 0) + (1 if flags & 2 else 0) + (1 if flags & 4 else 0) + (2 if flags & 8 else 0)
    return o - addr


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    rom = open(sys.argv[1], "rb").read()
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets")
    out = os.path.abspath(out)
    e = Exporter(rom, out)

    # ---- fixed tables ---------------------------------------------------------------------------------------------
    e.block("tables/sfx_table.bin", SFX_TABLE, SFX_COUNT * 12, "table", symbol="g_SFXDefTable", entry="SfxSample", count=SFX_COUNT,
            pointers=[{"at": 12 * i, "to": "sfx/sfx_%02d.bin" % i} for i in range(SFX_COUNT)])
    e.block("tables/music_track_table.bin", TRACK_TABLE, TRACK_COUNT * 20, "table", symbol="g_MusicTrackTable", entry="MusicTrackInfo", count=TRACK_COUNT)
    e.block("tables/note_frequency_table.bin", NOTE_TABLE, NOTE_COUNT * 4, "table", symbol="g_NoteFrequencyTable", entry="u32", count=NOTE_COUNT)
    for name, a in SLIDE_TABLES.items():
        e.block("tables/%s_table.bin" % name, a, 128, "table", symbol="g_" + "".join(p.capitalize() for p in name.split("_")) + "Table", entry="u16", count=64)
    e.block("tables/vibrato_sine_table.bin", SINE_TABLE, 64, "table", symbol="g_VibratoSineTable", entry="s8", count=64)
    e.block("tables/effect_table.bin", EFFECT_TABLE, EFFECT_COUNT * 4, "table", symbol="Audio_EffectTable", entry="code pointer", count=EFFECT_COUNT,
            note="pointers into the engine code; rebuilt from src/audio/effect_table.cpp")

    # ---- SFX samples ----------------------------------------------------------------------------------------------
    for i in range(SFX_COUNT):
        a = SFX_TABLE + 12 * i
        data, loop, length, rate = struct.unpack_from("<IIHH", rom, a)
        e.block("sfx/sfx_%02d.bin" % i, off(data), length, "pcm_s8", rate=rate, loop=bool(loop), index=i)

    # ---- tracks (every song shares one bank + one preset table) -----------------------------------------------------
    tracks = [struct.unpack_from("<IIIIBBH", rom, TRACK_TABLE + 20 * i) for i in range(TRACK_COUNT)]
    bank = off(tracks[0][2])
    presets_addr = off(tracks[0][3])
    presets = list(rom[presets_addr:presets_addr + 14])
    n_inst = 0
    while True:
        p, mode, ls, ln, rate = struct.unpack_from("<IHHHH", rom, bank + 12 * n_inst)
        if not is_rom_ptr(p, rom) or not 1000 < rate < 60000:
            break
        n_inst += 1
    e.block("tables/instrument_bank.bin", bank, n_inst * 12, "table", symbol="g_InstrumentBank", entry="SampleInstrument", count=n_inst,
            pointers=[{"at": 12 * i, "to": "instruments/inst_%03d.bin" % i} for i in range(n_inst)])
    e.block("tables/event_presets.bin", presets_addr, 14, "table", symbol="g_EventPresets", entry="u8", count=14)
    for i in range(n_inst):
        p, mode, ls, ln, rate = struct.unpack_from("<IHHHH", rom, bank + 12 * i)
        e.block("instruments/inst_%03d.bin" % i, off(p), ln, "pcm_s8", rate=rate, mode=mode, loop_start=ls, index=i)

    pattern_bytes = 0
    for t, (cp, vm, bk, mc, tempo, speed, _) in enumerate(tracks):
        d = "music/track%02d/" % t
        order, o = [], off(vm)
        while rom[o] != 0xFF:
            order.append(rom[o])
            o += 1
        e.block(d + "order.bin", off(vm), len(order) + 1, "order_list", track=t, symbol="Music_Track%02d_OrderList" % t)
        n_pat = max(order) + 1
        e.block(d + "pattern_table.bin", off(cp), n_pat * 4, "pointer_table", track=t, symbol="Music_Track%02d_PatternTable" % t,
                pointers=[{"at": 4 * k, "to": d + "pattern%02d.bin" % k} for k in sorted(set(order))])
        for k in sorted(set(order)):
            pa = off(struct.unpack_from("<I", rom, off(cp) + 4 * k)[0])
            size = parse_pattern(rom, pa, presets)
            pattern_bytes += size
            e.block(d + "pattern%02d.bin" % k, pa, size, "pattern", track=t, pattern=k)

    manifest = {
        "rom": os.path.basename(sys.argv[1]),
        "note": "US ROM. Blocks are byte-exact copies; 'pointers' lists 4-byte ROM pointers inside a block and the block file they target.",
        "blocks": sorted(e.blocks, key=lambda b: b["rom_address"]),
    }
    with open(os.path.join(out, "audio", "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=1)
    total = sum(b["size"] for b in e.blocks)
    print("wrote %d blocks (%d bytes) under %s" % (len(e.blocks), total, os.path.join(out, "audio")))
    return 0


if __name__ == "__main__":
    sys.exit(main())
