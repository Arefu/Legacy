// Sequencer_ParseRow -- 0x08020820 (0x8A bytes). HIGH. Advances to the next row (and pattern) and stores that row's events on the channels.
// Pattern data: u8 rows, then per row a list of events ended by a 0 byte. Event byte = (preset << 4) | channel(1..15):
//   preset 0: the next byte is the flag byte (remembered per channel); 1: reuse the remembered flags; 2..15: g_EventPresets[preset - 2].
// Flag bits 0x01 / 0x02 / 0x04 each pull one byte (note, instrument, volume), 0x08 pulls two (effect id, param).
#include "dbzlog2/audio/audio_state.h"

namespace dbzlog2::audio {

void Sequencer_ParseRow(MusicPlayer* player) {
    SequencerCursor& cursor = g_SequencerCursor;
    const MusicTrackInfo* track = cursor.track;

    cursor.row_counter += 1;
    if (cursor.row_counter >= cursor.pattern_length) {
        cursor.pattern_index += 1;
        const u8* order = RomData(track->order_list);
        if (order[cursor.pattern_index] == 0xFF) cursor.pattern_index = 0;          // end of the order list: loop
        const u8* pattern_table = RomData(track->pattern_table);
        const RomPtr pattern_ptr = reinterpret_cast<const RomPtr*>(pattern_table)[order[cursor.pattern_index]];
        const u8* pattern = RomData(pattern_ptr);
        cursor.row_counter = 0;
        cursor.read_ptr = pattern_ptr + 1;
        cursor.pattern_length = pattern[0];
    }

    for (int i = 0; i < kMusicChannelCount; ++i) player->channels[i].event_flags = 0;   // channels without an event this row do nothing

    const u8* presets = RomData(track->event_presets);
    for (;;) {
        const u8* rp = RomData(cursor.read_ptr);
        const u8 event = *rp++;
        if (event == 0) { cursor.read_ptr += 1; break; }                            // end of the row
        MusicChannel* ch = &player->channels[(event & 0xF) - 1];
        const u8 preset = event >> 4;
        if (preset == 0) {
            const u8 flags = *rp++;
            ch->last_event_flags = flags;
            ch->event_flags = flags;
        } else if (preset == 1) {
            ch->event_flags = ch->last_event_flags;
        } else {
            ch->event_flags = presets[preset - 2];
        }
        const u8 flags = ch->event_flags;
        if (flags & kEventNote) ch->note = *rp++;
        if (flags & kEventInstrument) ch->instrument_index = *rp++;
        if (flags & kEventVolume) ch->event_volume = *rp++;
        if (flags & kEventEffect) { ch->effect_id = rp[0]; ch->effect_param = rp[1]; rp += 2; }
        cursor.read_ptr = cursor.read_ptr + static_cast<RomPtr>(rp - RomData(cursor.read_ptr));
    }
}

}  // namespace dbzlog2::audio
