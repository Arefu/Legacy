using DrGero.IO;

namespace DrGero.Rendering
{
    /// <summary>
    /// JCALG1 decompressor, reading directly from a <see cref="ROM"/> instead of
    /// a raw byte[]/offset pair. The core bitstream engine is ported from
    /// Jcalg1Decompress (https://gist.github.com/wisk/e7deaacba9dbea812dc9b9753f7d8ebe),
    /// which fixes a latent bug present in the original ROM-based port: literalBits
    /// must default to 8, not 0, or the very first literal byte read (before any
    /// "literal size change" instruction appears in the stream) decodes incorrectly.
    /// JCALG1 is copyright (C) Jeremy Collake.
    /// </summary>
    public static class JCALG1
    {
        /// <summary>
        /// Result of a decompression, including where in the ROM the compressed
        /// stream ended — useful for reading back-to-back assets sequentially.
        /// </summary>
        public readonly struct DecompressResult(byte[] data, int endOffset)
        {
            public byte[] Data { get; } = data;
            public int EndOffset { get; } = endOffset;
        }

        /// <summary>
        /// Decompresses a block with a known header: a 4-byte format flag
        /// (0 = stored/uncompressed, nonzero = JCALG1-compressed) followed by a
        /// 4-byte decompressed size. Restores the ROM's position on return.
        /// </summary>
        public static byte[] Decompress(ROM rom, int address)
        {
            ArgumentNullException.ThrowIfNull(rom);
            ArgumentOutOfRangeException.ThrowIfNegative(address);

            rom.PushPosition(address);

            int format = rom.ReadInt();
            int decompressedSize = rom.ReadInt();

            byte[] result;
            if (format == 0)
            {
                // stored, not compressed
                result = new byte[decompressedSize];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = (byte)rom.ReadByte();
                }
            }
            else
            {
                result = DecompressInternal(rom, decompressedSize).Data;
            }

            rom.PopPosition();
            return result;
        }

        /// <summary>
        /// Decompresses a raw JCALG1 bitstream with no size header — the source
        /// only specifies a 4-byte value to skip before the stream begins, and
        /// decompression runs until the stream's own terminator is hit.
        /// </summary>
        public static byte[] DecompressUnknownHeader(ROM rom, int address)
        {
            ArgumentNullException.ThrowIfNull(rom);
            ArgumentOutOfRangeException.ThrowIfNegative(address);

            rom.PushPosition(address);
            rom.Skip(0x4);

            const int maxBufferSize = 1024 * 64;
            var result = DecompressInternal(rom, maxBufferSize);

            rom.PopPosition();
            return result.Data;
        }

        /// <summary>
        /// Decompresses starting at the ROM's current position with no header at
        /// all, and reports the ROM offset where the compressed stream ended —
        /// for reading multiple back-to-back compressed assets sequentially.
        /// </summary>
        public static DecompressResult DecompressSequential(ROM rom, int expectedOutputSize = 0)
        {
            ArgumentNullException.ThrowIfNull(rom);

            int bufferSize = expectedOutputSize > 0 ? expectedOutputSize : 4 * 1024 * 1024;
            return DecompressInternal(rom, bufferSize);
        }

        private static DecompressResult DecompressInternal(ROM rom, int bufferSize)
        {
            var source = new CompressionSource(rom);
            byte[] dest = new byte[bufferSize];
            int destPos = 0;

            int lastIndex = 1;
            int indexBase = 8;
            int literalBits = 8;
            int literalOffset = 0;

            while (true)
            {
                if (source.GetBit() == 1)
                {
                    dest[destPos++] = (byte)(source.GetBits(literalBits) + literalOffset);
                }
                else
                {
                    if (source.GetBit() == 1)
                    {
                        int highIndex = source.GetInteger();

                        if (highIndex == 2)
                        {
                            int phraseLength = source.GetInteger();
                            TransferMatch(dest, ref destPos, lastIndex, phraseLength);
                        }
                        else
                        {
                            lastIndex = ((highIndex - 3) << indexBase) + source.GetBits(indexBase);
                            int phraseLength = source.GetInteger();

                            if (lastIndex >= 0x10000) phraseLength += 3;
                            else if (lastIndex >= 0x37FF) phraseLength += 2;
                            else if (lastIndex >= 0x27F) phraseLength += 1;
                            else if (lastIndex <= 127) phraseLength += 4;

                            TransferMatch(dest, ref destPos, lastIndex, phraseLength);
                        }
                    }
                    else if (source.GetBit() == 1)
                    {
                        int oneBytePhraseValue = source.GetBits(4) - 1;

                        if (oneBytePhraseValue == 0)
                        {
                            dest[destPos++] = 0;
                        }
                        else if (oneBytePhraseValue > 0)
                        {
                            dest[destPos] = dest[destPos - oneBytePhraseValue];
                            destPos++;
                        }
                        else
                        {
                            if (source.GetBit() == 1)
                            {
                                do
                                {
                                    for (int i = 0; i < 256; i++)
                                        dest[destPos++] = (byte)source.GetBits(8);
                                } while (source.GetBit() == 1);
                            }
                            else
                            {
                                literalBits = 7 + source.GetBit();
                                literalOffset = 0;
                                if (literalBits != 8)
                                    literalOffset = source.GetBits(8);
                            }
                        }
                    }
                    else
                    {
                        int newIndex = source.GetBits(7);
                        int matchLength = 2 + source.GetBits(2);

                        if (newIndex == 0)
                        {
                            if (matchLength == 2) break; // end of stream

                            indexBase = source.GetBits(matchLength + 1);
                        }
                        else
                        {
                            lastIndex = newIndex;
                            TransferMatch(dest, ref destPos, lastIndex, matchLength);
                        }
                    }
                }
            }

            byte[] result = new byte[destPos];
            Buffer.BlockCopy(dest, 0, result, 0, destPos);

            return new DecompressResult(result, rom.Position);
        }

        private static void TransferMatch(byte[] destination, ref int destPos, int matchOffset, int matchLength)
        {
            int src = destPos - matchOffset;
            for (int i = 0; i < matchLength; i++)
            {
                destination[destPos++] = destination[src++];
            }
        }

        /// <summary>
        /// Bit-reading engine sourced from a <see cref="ROM"/> instead of a byte[].
        /// Reads 4 bytes at a time via <see cref="ROM.ReadInt"/> (little-endian,
        /// matching the original BitConverter.ToUInt32 behavior exactly).
        /// </summary>
        private class CompressionSource(ROM _rom)
        {
            private readonly ROM rom = _rom;
            private uint bitBuffer;
            private int bitsRemaining;

            public int GetBit()
            {
                if (bitsRemaining == 0)
                {
                    bitsRemaining = 32;
                    AdvanceBuffer();
                }

                uint result = bitBuffer >> 31;
                bitBuffer <<= 1;
                bitsRemaining--;
                return (int)result;
            }

            public int GetBits(int count)
            {
                if (count == 0) return 0;

                if (bitsRemaining >= count)
                {
                    uint result = bitBuffer >> (32 - count);
                    bitBuffer <<= count;
                    bitsRemaining -= count;
                    return (int)result;
                }
                else
                {
                    int remainder = count - bitsRemaining;

                    uint result = bitBuffer >> (32 - bitsRemaining) << remainder;
                    AdvanceBuffer();

                    result |= bitBuffer >> (32 - remainder);
                    bitsRemaining = 32 - remainder;
                    bitBuffer <<= remainder;

                    return (int)result;
                }
            }

            public int GetInteger()
            {
                int result = 1;
                do
                {
                    result = (result << 1) + GetBit();
                }
                while (GetBit() != 0);
                return result;
            }

            private void AdvanceBuffer()
            {
                if (rom.Remaining >= 4)
                {
                    bitBuffer = (uint)rom.ReadInt();
                }
                else
                {
                    bitBuffer = 0;
                }
            }
        }
    }
}