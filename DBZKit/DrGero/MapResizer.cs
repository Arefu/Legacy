using DrGero.IO;

namespace DrGero.Rendering
{
    /// <summary>
    /// Reads and changes a map's pixel size, and can grow the background layers so the new area
    /// has room to be painted.
    ///
    /// The size fields (Map_VariationEntry +0x8 width, +0xA height, u16 each) are NOT the pixel
    /// size: Map_InitRenderer stores width-1 / height-1 as the camera's max scroll and width+239 /
    /// height+159 as the far edge, i.e. they hold the SCROLL RANGE and the real map size is
    /// field + 239 by field + 159. Checked against real maps: a single-screen map stores 1 x 1
    /// (= 240 x 160), map 0 stores 145 x 609 (= 384 x 768, inside layers covering 512 x 896), and
    /// maps 8 and 11 land exactly on their layers' 512-pixel edge. This class always talks in
    /// real pixels.
    ///
    /// A layer is a header plus a grid of chunk pointers (each chunk 32x32 tiles = 256x256 px), so
    /// it only covers numCols*256 - offX by numRows*256 - offY. A map bigger than its layers shows
    /// void past the edge; growing a layer allocates a new header + pointer array with extra EMPTY
    /// chunks (pointer 0 -- drawn blank, like any chunk the map never used) and repoints the map.
    /// </summary>
    public static class MapResizer
    {
        private const int WidthOffset = 0x8;
        private const int HeightOffset = 0xA;
        private const int LayerHeaderSize = 0x18;
        private const int ChunkPixels = 256;

        // real pixel size = stored value + these (see the class notes)
        private const int WidthBias = 239;
        private const int HeightBias = 159;
        public const int ScreenWidth = 240;
        public const int ScreenHeight = 160;
        public const int MaxSize = 2048; // the collision grid is 256x256 tiles; Collision_CheckRect rejects beyond 2048

        /// <summary>The map's real pixel size (the stored scroll range + 239 / 159).</summary>
        public static (int Width, int Height) ReadSize(ROM rom, int mapOffset)
        {
            rom.PushPosition(mapOffset + WidthOffset);
            int scrollW = rom.ReadShort() & 0xFFFF;
            int scrollH = rom.ReadShort() & 0xFFFF;
            rom.PopPosition();
            return (scrollW + WidthBias, scrollH + HeightBias);
        }

        /// <summary>The pixel size the map's layers actually cover (the largest across BG0-3).</summary>
        public static (int Width, int Height) LayerCoverage(ROM rom, int mapOffset)
        {
            int width = 0, height = 0;
            for (int i = 0; i < 4; i++)
            {
                var layer = OpenLayer(rom, mapOffset, i, out _);
                if (layer == null) continue;
                width = Math.Max(width, (layer.NumCols * ChunkPixels) - layer.OffX);
                height = Math.Max(height, (layer.NumRows * ChunkPixels) - layer.OffY);
            }
            return (width, height);
        }

        private static TileLayerEditor? OpenLayer(ROM rom, int mapOffset, int index, out int layerAddress)
        {
            rom.PushPosition(mapOffset + 0x14 + (index * 4));
            layerAddress = rom.ReadPointer();
            rom.PopPosition();
            return TileLayerEditor.Open(rom, layerAddress);
        }

        /// <summary>
        /// Sets the map's real pixel size. Returns null on success or an error message. growLayers
        /// also extends each tiled layer that doesn't reach the new size (layersGrown says how many).
        /// </summary>
        public static string? Resize(ROM rom, int mapOffset, int width, int height, bool growLayers, out int layersGrown)
        {
            layersGrown = 0;
            if (width < ScreenWidth || height < ScreenHeight)
                return $"A map can't be smaller than the screen ({ScreenWidth} x {ScreenHeight}).";
            if (width > MaxSize || height > MaxSize)
                return $"A map can't be larger than {MaxSize} pixels either way (the collision grid stops there).";

            int scrollW = width - WidthBias, scrollH = height - HeightBias;
            rom.PatchByte(mapOffset + WidthOffset, (byte)(scrollW & 0xFF));
            rom.PatchByte(mapOffset + WidthOffset + 1, (byte)(scrollW >> 8));
            rom.PatchByte(mapOffset + HeightOffset, (byte)(scrollH & 0xFF));
            rom.PatchByte(mapOffset + HeightOffset + 1, (byte)(scrollH >> 8));

            if (!growLayers) return null;

            for (int i = 0; i < 4; i++)
            {
                var layer = OpenLayer(rom, mapOffset, i, out int layerAddress);
                if (layer == null) continue;

                int needCols = (width + layer.OffX + ChunkPixels - 1) / ChunkPixels;
                int needRows = (height + layer.OffY + ChunkPixels - 1) / ChunkPixels;
                int newCols = Math.Min(255, Math.Max(layer.NumCols, needCols));
                int newRows = Math.Min(255, Math.Max(layer.NumRows, needRows));
                if (newCols == layer.NumCols && newRows == layer.NumRows) continue;

                // New header (copied, with the two size bytes updated) + a pointer grid that keeps
                // every existing chunk in its (row, col) slot and leaves the new slots empty.
                byte[] header = rom.ReadBytesAt(layerAddress, LayerHeaderSize);
                header[0x14] = (byte)newCols;
                header[0x15] = (byte)newRows;

                byte[] blob = new byte[LayerHeaderSize + (newCols * newRows * 4)];
                header.CopyTo(blob, 0);
                for (int r = 0; r < layer.NumRows; r++)
                {
                    byte[] oldRow = rom.ReadBytesAt(layerAddress + LayerHeaderSize + (4 * r * layer.NumCols), 4 * layer.NumCols);
                    Buffer.BlockCopy(oldRow, 0, blob, LayerHeaderSize + (4 * r * newCols), oldRow.Length);
                }

                int offset = rom.AllocateFreeSpace(blob.Length);
                rom.WriteBytesAt(offset, blob);
                rom.PatchInt32(mapOffset + 0x14 + (i * 4), 0x08000000 | offset);
                layersGrown++;
            }
            return null;
        }
    }
}
