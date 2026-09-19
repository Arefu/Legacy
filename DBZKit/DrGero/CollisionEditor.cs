using DrGero.IO;

namespace DrGero.Rendering
{
    /// <summary>
    /// Edits a map's collision grid in place in a ROM.
    ///
    /// Format (mirrors MapRenderer.ReadCollisionMap; confirmed via IDA, Collision_CheckRect
    /// @0x8005D6C): Map_VariationEntry +0x34 points at a resource that decodes to 8192 bytes =
    /// 256x256 bits, one bit per 8x8 tile, 32 tiles per u32 word, 8 words per tile row:
    /// `word[8*tileY + (tileX>>5)] & (1 << (tileX & 31))`.
    ///
    /// A SET bit means ALLOWED (walkable); a CLEAR bit means BLOCKED (barred). Confirmed via IDA
    /// (Collision_CheckRect keeps scanning while bits are set and returns BLOCKED at the first
    /// clear one; outside the grid is BLOCKED too). An earlier version of these notes had it
    /// backwards. So "collision" painted on the map is where you CAN walk.
    ///
    /// Writing: the first edit writes the whole grid as a STORED resource (format 0 -- the game's
    /// Resource_LoadOrDecompress copies it as-is) into free space and repoints +0x34; later edits
    /// patch the single changed byte in that allocation. Keep the instance alive across strokes.
    /// </summary>
    public sealed class CollisionEditor
    {
        public const int GridSize = 256;
        private const int DataBytes = GridSize * GridSize / 8; // 8192
        private const int PointerFieldOffset = 0x34;

        private readonly ROM _rom;
        private readonly int _mapOffset;
        private readonly byte[] _data;
        private int _romOffset; // our stored copy (0 = not written yet)

        private CollisionEditor(ROM rom, int mapOffset, byte[] data)
        {
            _rom = rom;
            _mapOffset = mapOffset;
            _data = data;
        }

        /// <summary>The ROM this editor writes to -- drop the editor if the caller's ROM object is replaced.</summary>
        public ROM Rom => _rom;
        public int MapOffset => _mapOffset;

        /// <summary>Opens a map's collision grid; a map with none starts all-clear. Null if the data can't be read.</summary>
        public static CollisionEditor? Open(ROM rom, int mapOffset)
        {
            rom.PushPosition(mapOffset + PointerFieldOffset);
            int pointer = rom.ReadPointer();
            rom.PopPosition();

            byte[] data = new byte[DataBytes];
            if (pointer != 0)
            {
                byte[] decoded;
                try { decoded = JCALG1.Decompress(rom, pointer); }
                catch { return null; }
                Array.Copy(decoded, data, Math.Min(decoded.Length, DataBytes));
            }
            return new CollisionEditor(rom, mapOffset, data);
        }

        private static bool InRange(int tileX, int tileY) => tileX >= 0 && tileX < GridSize && tileY >= 0 && tileY < GridSize;

        /// <summary>A copy of the raw 8192-byte grid (bit set = allowed), exactly as the game reads it.</summary>
        public byte[] ExportRaw() => (byte[])_data.Clone();

        // Byte within the grid holding this tile's bit (the u32 words are little-endian).
        private static int ByteIndex(int tileX, int tileY) => (((8 * tileY) + (tileX >> 5)) * 4) + ((tileX & 31) >> 3);

        /// <summary>True if the tile's bit is set = the player is ALLOWED there (see the class notes).</summary>
        public bool IsAllowed(int tileX, int tileY) =>
            InRange(tileX, tileY) && (_data[ByteIndex(tileX, tileY)] & (1 << (tileX & 7))) != 0;

        /// <summary>Sets (allowed = walkable) or clears (barred) one 8x8 tile's bit. False = outside the 256x256 grid.</summary>
        public bool SetAllowed(int tileX, int tileY, bool allowed)
        {
            if (!InRange(tileX, tileY)) return false;

            int index = ByteIndex(tileX, tileY);
            byte mask = (byte)(1 << (tileX & 7));
            byte updated = allowed ? (byte)(_data[index] | mask) : (byte)(_data[index] & ~mask);
            if (updated == _data[index]) return true; // already that way

            _data[index] = updated;

            if (_romOffset == 0)
            {
                byte[] blob = new byte[8 + DataBytes];
                BitConverter.GetBytes(0).CopyTo(blob, 0);          // format 0 = stored
                BitConverter.GetBytes(DataBytes).CopyTo(blob, 4);  // size
                _data.CopyTo(blob, 8);

                _romOffset = _rom.AllocateFreeSpace(blob.Length);
                _rom.WriteBytesAt(_romOffset, blob);
                _rom.PatchInt32(_mapOffset + PointerFieldOffset, 0x08000000 | _romOffset);
            }
            else
            {
                _rom.WriteBytesAt(_romOffset + 8 + index, new[] { updated });
            }
            return true;
        }
    }
}
