/// <summary>
/// Pure C# port of wisk's JCALG1 decompressor.
/// Works on raw headerless streams (no size header expected).
/// Direct port of: https://gist.github.com/wisk/e7deaacba9dbea812dc9b9753f7d8ebe
/// JCALG1 is copyright (C) Jeremy Collake
/// </summary>
public static class Jcalg1Decompress
{
    public class DecompressResult
    {
        /// <summary>Decompressed bytes.</summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Exact file offset where the compressed stream ended.
        /// Use this as the start of the next asset.
        /// </summary>
        public int EndOffset { get; set; }
    }

    private class CompressionSourceData
    {
        private readonly byte[] _data;
        private int _pos;
        private uint _bitBuffer;
        private int _bitsRemaining;

        public int Position => _pos;

        public CompressionSourceData(byte[] data, int offset)
        {
            _data = data;
            _pos = offset;
            _bitBuffer = 0;
            _bitsRemaining = 0;
        }

        private uint ReadUInt32()
        {
            uint val = BitConverter.ToUInt32(_data, _pos);
            _pos += 4;
            return val;
        }

        public int GetBit()
        {
            if (_bitsRemaining == 0)
            {
                _bitsRemaining = 32;
                _bitBuffer = ReadUInt32();
            }

            int bit = (int)(_bitBuffer >> 31);
            _bitBuffer <<= 1;
            _bitsRemaining--;
            return bit;
        }

        public int GetBits(int count)
        {
            if (count == 0) return 0;

            if (_bitsRemaining >= count)
            {
                int val = (int)(_bitBuffer >> (32 - count));
                _bitBuffer <<= count;
                _bitsRemaining -= count;
                return val;
            }
            else
            {
                int remainder = count - _bitsRemaining;
                int val = (int)(_bitBuffer >> (32 - _bitsRemaining)) << remainder;
                _bitBuffer = ReadUInt32();
                val |= (int)(_bitBuffer >> (32 - remainder));
                _bitsRemaining = 32 - remainder;
                _bitBuffer <<= remainder;
                return val;
            }
        }

        public int GetInteger()
        {
            int value = 1;
            do
            {
                value = (value << 1) + GetBit();
            } while (GetBit() != 0);
            return value;
        }
    }

    /// <summary>
    /// Decompresses a raw JCALG1 bitstream (no size header).
    /// </summary>
    /// <param name="data">Buffer containing the compressed data.</param>
    /// <param name="offset">Offset into <paramref name="data"/> where the stream begins.</param>
    /// <param name="expectedOutputSize">
    /// Optional hint for output buffer size. If 0, a 4MB buffer is used and trimmed.
    /// </param>
    /// <returns>
    /// <see cref="DecompressResult"/> containing the decompressed bytes and the exact
    /// file offset where the compressed stream ended, ready to read the next asset.
    /// </returns>
    public static DecompressResult Decompress(byte[] data, int offset = 0, int expectedOutputSize = 0)
    {
        var source = new CompressionSourceData(data, offset);
        byte[] dest = new byte[expectedOutputSize > 0 ? expectedOutputSize : 4 * 1024 * 1024];
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
                else
                {
                    if (source.GetBit() == 1)
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
                            if (matchLength == 2) break;

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
        }

        byte[] result = new byte[destPos];
        Buffer.BlockCopy(dest, 0, result, 0, destPos);

        return new DecompressResult
        {
            Data = result,
            EndOffset = source.Position
        };
    }

    private static void TransferMatch(byte[] dest, ref int destPos, int matchOffset, int matchLength)
    {
        int src = destPos - matchOffset;
        for (int i = 0; i < matchLength; i++)
            dest[destPos++] = dest[src++];
    }
}