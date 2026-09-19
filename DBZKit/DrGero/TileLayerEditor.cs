using DrGero.IO;
using System.Drawing;

namespace DrGero.Rendering
{
    /// <summary>
    /// Edits the tile entries of ONE background layer of a map, in place in a ROM.
    ///
    /// Format (mirrors MapRenderer.DrawLayer/DrawChunk exactly): a layer struct has a 0x18-byte
    /// header (type 0x56A1, scroll offsets, numCols/numRows) followed by numCols*numRows
    /// pointers to chunks; a chunk is a resource (u32 format, u32 size, payload -- format 0 is
    /// STORED, confirmed via IDA: Resource_LoadOrDecompress copies `size` bytes from +8) that
    /// decodes to 32x32 tile entries of u16 each, row-major: bits 0-9 tile id (a slot in the
    /// map's tileset), bit 10 flip X, bit 11 flip Y.
    ///
    /// Writing: the first edit to a chunk writes the WHOLE chunk as a stored resource into free
    /// space and repoints that chunk's slot at it (no compressor needed, and the game reads
    /// format 0 natively); every later edit to the same chunk patches just the changed 2 bytes
    /// in that same allocation, so a long brush stroke costs one 2 KB allocation per touched
    /// chunk, not one per cell. Keep an instance alive across strokes for the same reason.
    /// </summary>
    public sealed class TileLayerEditor
    {
        public const int TileSize = 8;
        public const int ChunkTiles = 32;
        private const int ChunkPixels = TileSize * ChunkTiles; // 256
        private const int ChunkBytes = ChunkTiles * ChunkTiles * 2;
        private const int HeaderSize = 0x18;
        private const int TiledLayerType = 0x0056A1;

        private sealed class Chunk
        {
            public byte[] Data = new byte[ChunkBytes];
            public int RomOffset; // our stored resource (0 = not written to the ROM yet)
        }

        private readonly ROM _rom;
        private readonly Dictionary<(int Col, int Row), Chunk> _chunks = new();

        public int LayerAddress { get; }
        /// <summary>Scroll offsets after the same fixups DrawLayer applies -- layer pixel = world pixel + Off.</summary>
        public int OffX { get; }
        public int OffY { get; }
        public int NumCols { get; }
        public int NumRows { get; }

        private TileLayerEditor(ROM rom, int layerAddress, int offX, int offY, int numCols, int numRows)
        {
            _rom = rom;
            LayerAddress = layerAddress;
            OffX = offX;
            OffY = offY;
            NumCols = numCols;
            NumRows = numRows;
        }

        /// <summary>The ROM this editor writes to -- callers should drop the editor if their ROM object is replaced.</summary>
        public ROM Rom => _rom;

        /// <summary>Opens a layer for editing, or null if it's empty or in the unrecognised 0x4FCD format.</summary>
        public static TileLayerEditor? Open(ROM rom, int layerAddress)
        {
            if (layerAddress <= 0) return null;

            rom.PushPosition(layerAddress);
            try
            {
                if (rom.ReadPointer() != TiledLayerType) return null;

                rom.Skip(0x9);
                int offX = rom.ReadShort() / 4;
                rom.Skip(0x2);
                int offY = rom.ReadShort() / 4;
                rom.Skip(0x1);
                int numCols = rom.ReadByte();
                int numRows = rom.ReadByte();

                if (offX >= 0x3F00) { offX -= 0x3F00; offX *= -1; }
                if (offY >= 0x3F00) { offY -= 0x3F00; offY *= -1; }

                if (numCols <= 0 || numRows <= 0) return null;
                return new TileLayerEditor(rom, layerAddress, offX, offY, numCols, numRows);
            }
            finally
            {
                rom.PopPosition();
            }
        }

        private bool TryLocate(int worldX, int worldY, out int chunkCol, out int chunkRow, out int tileX, out int tileY)
        {
            int lx = worldX + OffX, ly = worldY + OffY;
            chunkCol = chunkRow = tileX = tileY = 0;
            if (lx < 0 || ly < 0) return false;

            chunkCol = lx / ChunkPixels;
            chunkRow = ly / ChunkPixels;
            if (chunkCol >= NumCols || chunkRow >= NumRows) return false;

            tileX = (lx % ChunkPixels) / TileSize;
            tileY = (ly % ChunkPixels) / TileSize;
            return true;
        }

        /// <summary>Top-left (world/composite pixels) of the tile cell containing a world point.</summary>
        public Point CellOrigin(int worldX, int worldY) =>
            new((((worldX + OffX) / TileSize) * TileSize) - OffX, (((worldY + OffY) / TileSize) * TileSize) - OffY);

        private Chunk? GetChunk(int col, int row)
        {
            if (_chunks.TryGetValue((col, row), out var existing)) return existing;

            int slot = LayerAddress + HeaderSize + (4 * ((row * NumCols) + col));
            _rom.PushPosition(slot);
            int pointer = _rom.ReadPointer();
            _rom.PopPosition();

            var chunk = new Chunk();
            if (pointer != 0)
            {
                byte[] data;
                try { data = JCALG1.Decompress(_rom, pointer); }
                catch { return null; }
                if (data.Length != ChunkBytes) return null; // unexpected size -- refuse to edit rather than guess
                chunk.Data = data;
            }
            // pointer == 0: an empty chunk the map never drew; starts as all tile-0 (transparent)

            _chunks[(col, row)] = chunk;
            return chunk;
        }

        /// <summary>
        /// The whole layer as one flat tilemap: (NumCols*32) x (NumRows*32) u16 tile entries,
        /// little-endian, row-major (entry = id | flipX&lt;&lt;10 | flipY&lt;&lt;11). Chunks that are missing or
        /// unreadable come out as zeros (blank). This is the layer in the form that's easy to edit
        /// outside the tool.
        /// </summary>
        public byte[] ExportTilemap()
        {
            int width = NumCols * ChunkTiles, height = NumRows * ChunkTiles;
            byte[] result = new byte[width * height * 2];

            for (int r = 0; r < NumRows; r++)
            {
                for (int c = 0; c < NumCols; c++)
                {
                    var chunk = GetChunk(c, r);
                    if (chunk == null) continue;

                    for (int ty = 0; ty < ChunkTiles; ty++)
                    {
                        int src = ty * ChunkTiles * 2;
                        int dst = ((((r * ChunkTiles) + ty) * width) + (c * ChunkTiles)) * 2;
                        Buffer.BlockCopy(chunk.Data, src, result, dst, ChunkTiles * 2);
                    }
                }
            }
            return result;
        }

        /// <summary>The raw tile entry at a world pixel, or null if that's outside the layer / unreadable.</summary>
        public ushort? GetTile(int worldX, int worldY)
        {
            if (!TryLocate(worldX, worldY, out int c, out int r, out int tx, out int ty)) return null;
            var chunk = GetChunk(c, r);
            if (chunk == null) return null;
            int i = ((ty * ChunkTiles) + tx) * 2;
            return (ushort)(chunk.Data[i] | (chunk.Data[i + 1] << 8));
        }

        /// <summary>Sets the tile entry (id | flipX&lt;&lt;10 | flipY&lt;&lt;11) at the cell containing a world pixel. False = outside the layer / can't edit.</summary>
        public bool SetTile(int worldX, int worldY, ushort entry)
        {
            if (!TryLocate(worldX, worldY, out int c, out int r, out int tx, out int ty)) return false;
            var chunk = GetChunk(c, r);
            if (chunk == null) return false;

            int i = ((ty * ChunkTiles) + tx) * 2;
            byte lo = (byte)(entry & 0xFF), hi = (byte)(entry >> 8);
            if (chunk.Data[i] == lo && chunk.Data[i + 1] == hi) return true; // nothing to change

            chunk.Data[i] = lo;
            chunk.Data[i + 1] = hi;

            if (chunk.RomOffset == 0)
            {
                byte[] blob = new byte[8 + ChunkBytes];
                BitConverter.GetBytes(0).CopyTo(blob, 0);          // format 0 = stored
                BitConverter.GetBytes(ChunkBytes).CopyTo(blob, 4); // size
                chunk.Data.CopyTo(blob, 8);

                chunk.RomOffset = _rom.AllocateFreeSpace(blob.Length);
                _rom.WriteBytesAt(chunk.RomOffset, blob);
                _rom.PatchInt32(LayerAddress + HeaderSize + (4 * ((r * NumCols) + c)), 0x08000000 | chunk.RomOffset);
            }
            else
            {
                _rom.WriteBytesAt(chunk.RomOffset + 8 + i, new[] { lo, hi });
            }
            return true;
        }
    }
}
