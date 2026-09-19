using DrGero.IO;

namespace DrGero.Rendering
{
    /// <summary>
    /// A map's static tileset table (Map_VariationEntry +0x48): the list of which atlas tile
    /// each tileset SLOT shows. Layer chunk entries store a SLOT id, not an atlas tile, which is
    /// why a tile from another map can't just be pasted in -- the slot means something different
    /// there. Using another map's tile means finding (or adding) a slot in THIS map's table that
    /// points at the same atlas tile.
    ///
    /// Format (confirmed via IDA, MapRenderer_UploadStaticTileset @0x80050C8 and this project's
    /// DrawTileset): a resource holding one u16 per slot; the atlas tile index is a running sum:
    ///   atlas[0] = d0;   atlas[i] = atlas[i-1] + 1 + d[i]     (16-bit wraparound, like the game's
    ///   __int16 arithmetic) -- so any tile order is expressible, going backwards wraps.
    /// The atlas tile index splits as bank = index >> 8 (which compressed 256-tile bank) and
    /// tile = index &amp; 0xFF. The game reads at most 896 entries (its stack buffer is 1792 bytes)
    /// and VRAM slots overlap animated-sequence ranges, so a new slot must avoid those.
    /// </summary>
    public static class TilesetTable
    {
        private const int TableFieldOffset = 0x48;
        public const int MaxSlots = 896;

        /// <summary>Atlas tile index per slot, decoded from the map's table.</summary>
        public static int[] ReadAtlasIndices(ROM rom, int mapOffset)
        {
            byte[] table = ReadTable(rom, mapOffset);
            var result = new int[table.Length / 2];
            int current = 0;
            for (int i = 0; i < result.Length; i++)
            {
                int delta = table[i * 2] | (table[(i * 2) + 1] << 8);
                current = (current + delta) & 0xFFFF;
                result[i] = current;
                current = (current + 1) & 0xFFFF;
            }
            return result;
        }

        private static byte[] ReadTable(ROM rom, int mapOffset)
        {
            rom.PushPosition(mapOffset + TableFieldOffset);
            int pointer = rom.ReadPointer();
            rom.PopPosition();
            return JCALG1.Decompress(rom, pointer);
        }

        /// <summary>
        /// Returns the slot in this map's tileset that shows <paramref name="atlasIndex"/>, adding
        /// one if it isn't there yet (the table is rewritten as a stored resource in free space and
        /// the map's +0x48 pointer repointed). Returns -1 if the table is full or the new slot
        /// would land inside one of the map's animated-tile ranges.
        /// </summary>
        public static int EnsureAtlasTile(ROM rom, int mapOffset, int atlasIndex)
        {
            atlasIndex &= 0xFFFF;

            byte[] table = ReadTable(rom, mapOffset);
            int count = table.Length / 2;

            int previous = -1; // atlas index of the last slot (the running sum starts at 0)
            int current = 0;
            for (int i = 0; i < count; i++)
            {
                int delta = table[i * 2] | (table[(i * 2) + 1] << 8);
                current = (current + delta) & 0xFFFF;
                if (current == atlasIndex) return i; // already there
                previous = current;
                current = (current + 1) & 0xFFFF;
            }

            if (count >= MaxSlots) return -1;
            int newSlot = count;
            if (SlotIsInAnimatedRange(rom, mapOffset, newSlot)) return -1;

            int newDelta = (atlasIndex - previous - 1) & 0xFFFF;
            byte[] blob = new byte[8 + table.Length + 2];
            BitConverter.GetBytes(0).CopyTo(blob, 0);                  // format 0 = stored
            BitConverter.GetBytes(table.Length + 2).CopyTo(blob, 4);   // decompressed size
            table.CopyTo(blob, 8);
            blob[8 + table.Length] = (byte)(newDelta & 0xFF);
            blob[8 + table.Length + 1] = (byte)(newDelta >> 8);

            int offset = rom.AllocateFreeSpace(blob.Length);
            rom.WriteBytesAt(offset, blob);
            rom.PatchInt32(mapOffset + TableFieldOffset, 0x08000000 | offset);
            return newSlot;
        }

        // Animated tile sequences (map +0xC count, +0x10 pointer array) are uploaded at their own
        // vramOffset..vramOffset+frames-1 tile ids -- see MapRenderer.DrawTileset.
        private static bool SlotIsInAnimatedRange(ROM rom, int mapOffset, int slot)
        {
            rom.PushPosition(mapOffset + 0xC);
            int sequenceCount = rom.ReadInt();
            int sequencePointer = rom.ReadPointer();
            rom.PopPosition();
            if (sequenceCount <= 0 || sequencePointer == 0) return false;

            for (int i = 0; i < sequenceCount; i++)
            {
                rom.PushPosition(sequencePointer + (i * 4));
                int structPointer = rom.ReadPointer();
                rom.PopPosition();

                rom.PushPosition(structPointer);
                rom.ReadByte(); // strips
                int frames = rom.ReadByte();
                int vramOffset = rom.ReadShort();
                rom.PopPosition();

                if (slot >= vramOffset && slot <= vramOffset + frames - 1) return true;
            }
            return false;
        }
    }
}
