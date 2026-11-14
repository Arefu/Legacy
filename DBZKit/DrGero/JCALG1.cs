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

            rom.NoteResource(address);
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

            rom.NoteResource(address);
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

            rom.NoteResource(rom.Position);
            int bufferSize = expectedOutputSize > 0 ? expectedOutputSize : 4 * 1024 * 1024;
            return DecompressInternal(rom, bufferSize);
        }

        /// <summary>
        /// Compresses to a JCALG1 bitstream the game's decoder (IWRAM 0x3000000, mirrored by <see cref="DecompressInternal"/>) reads back
        /// exactly. Greedy LZ over hash chains, using every op the decoder has: 8-bit literals, the one-byte "zero" / "copy 1..14 back"
        /// ops, short matches (distance 1..127, length 2..5), long matches, and "repeat the last distance". Wrap it in a ResourceHeader
        /// {mode 1, size, stream}. The stream starts with a "literal size change" op because the engine never initialises the literal width
        /// itself (see FrameImport.CompressLiteral).
        /// </summary>
        public static byte[] Compress(byte[] data)
        {
            var w = new BitWriter();
            w.Write(0b001, 3); w.Write(0, 4); w.Write(0, 1); w.Write(1, 1); // literal size change -> 8-bit literals, no offset

            const int HashBits = 16, MaxChain = 64, MaxDistance = 0xFFFF;
            int n = data.Length, last = 1;
            var head = new int[1 << HashBits];
            Array.Fill(head, -1);
            var prev = new int[Math.Max(1, n)];
            int Hash(int p) => (int)(((uint)(data[p] | data[p + 1] << 8 | data[p + 2] << 16) * 2654435761u) >> (32 - HashBits));
            void Insert(int p) { if (p + 2 < n) { int h = Hash(p); prev[p] = head[h]; head[h] = p; } }
            int Length(int from, int at, int max = int.MaxValue) { int l = 0; while (l < max && at + l < n && data[from + l] == data[at + l]) l++; return l; }
            static int IntBits(int v) => 2 * (31 - System.Numerics.BitOperations.LeadingZeroCount((uint)v));
            static int Adjust(int d) => d >= 0x10000 ? 3 : d >= 0x37FF ? 2 : d >= 0x27F ? 1 : d <= 127 ? 4 : 0;

            int pos = 0;
            while (pos < n)
            {
                // Candidates: (savings in bits against plain literals, length, kind, distance).
                int bestSave = 0, bestLen = 0, bestKind = 0, bestDist = 0;
                void Consider(int length, int cost, int kind, int distance)
                {
                    int save = length * 9 - cost;
                    if (save > bestSave) { bestSave = save; bestLen = length; bestKind = kind; bestDist = distance; }
                }

                if (last <= pos)
                {
                    int l = Length(pos - last, pos);
                    if (l >= 2) Consider(l, 4 + IntBits(l), 1, last);                                   // repeat last distance
                }
                for (int d = 1; d <= Math.Min(pos, 127); d++)                                            // short matches
                {
                    int l = Length(pos - d, pos, 5);
                    if (l >= 2) Consider(l, 12, 2, d);
                }
                if (pos + 2 < n)
                    for (int c = head[Hash(pos)], chain = 0; c >= 0 && chain < MaxChain; c = prev[c], chain++)  // long matches
                    {
                        int d = pos - c;
                        if (d > MaxDistance) break;
                        int l = Length(c, pos), stored = l - Adjust(d);
                        if (stored >= 2) Consider(l, 2 + IntBits((d >> 8) + 3) + 8 + IntBits(stored), 3, d);
                    }

                if (bestLen > 0)
                {
                    switch (bestKind)
                    {
                        case 1: w.Write(0b01, 2); w.Integer(2); w.Integer(bestLen); break;
                        case 2: w.Write(0b000, 3); w.Write(bestDist, 7); w.Write(bestLen - 2, 2); last = bestDist; break;
                        default:
                            w.Write(0b01, 2); w.Integer((bestDist >> 8) + 3); w.Write(bestDist & 0xFF, 8); w.Integer(bestLen - Adjust(bestDist));
                            last = bestDist;
                            break;
                    }
                    for (int i = 0; i < bestLen; i++) Insert(pos + i);
                    pos += bestLen;
                    continue;
                }

                // One byte: zero, a copy from 1..14 back (7 bits each), or a literal (9 bits).
                int back = 0;
                for (int d = 1; d <= Math.Min(pos, 14) && back == 0; d++) if (data[pos - d] == data[pos]) back = d;
                if (data[pos] == 0) { w.Write(0b001, 3); w.Write(1, 4); }
                else if (back > 0) { w.Write(0b001, 3); w.Write(back + 1, 4); }
                else { w.Write(1, 1); w.Write(data[pos], 8); }
                Insert(pos);
                pos++;
            }

            w.Write(0b000, 3); w.Write(0, 7); w.Write(0, 2); // end of stream
            return w.ToArray();
        }

        /// <summary>MSB-first bits packed into little-endian 32-bit words, as the decoder reads them.</summary>
        private sealed class BitWriter
        {
            private readonly List<byte> _out = [];
            private uint _word;
            private int _bits;

            public void Write(int value, int count)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    _word = (_word << 1) | (uint)((value >> i) & 1);
                    if (++_bits == 32) Flush();
                }
            }

            /// <summary>JCALG1's variable-length integer (v &gt;= 2): the bits after the leading 1, each followed by "more?".</summary>
            public void Integer(int v)
            {
                int top = 31 - System.Numerics.BitOperations.LeadingZeroCount((uint)v);
                for (int i = top - 1; i >= 0; i--) { Write((v >> i) & 1, 1); Write(i > 0 ? 1 : 0, 1); }
            }

            private void Flush()
            {
                _out.AddRange(BitConverter.GetBytes(_word));
                _word = 0;
                _bits = 0;
            }

            public byte[] ToArray()
            {
                if (_bits > 0) { _word <<= 32 - _bits; Flush(); }
                return [.. _out];
            }
        }

        /// <summary>
        /// Checks whether a <c>ResourceHeader</c> {u32 mode, u32 size, payload} at <paramref name="offset"/> is a real JCALG1 block:
        /// mode 1 or 2, a sane size, a stream that ends with its own end code, produces exactly <c>size</c> bytes and never copies
        /// from before the output start. Never throws; used to scan a ROM for blocks without trusting the bytes.
        /// </summary>
        /// <param name="end">File offset just past the compressed stream (exclusive).</param>
        public static bool TryValidate(byte[] rom, int offset, out int size, out int end, int maxSize = 0x40000)
        {
            size = 0; end = 0;
            if (offset < 0 || (offset & 3) != 0 || offset + 8 > rom.Length) return false;
            int mode = BitConverter.ToInt32(rom, offset);
            size = BitConverter.ToInt32(rom, offset + 4);
            if (mode is not (1 or 2) || size <= 0 || size > maxSize) return false;

            int pos = offset + 8, produced = 0, bits = 0;
            uint buffer = 0;
            int Bit()
            {
                if (bits == 0)
                {
                    if (pos + 4 > rom.Length) throw new InvalidDataException();
                    buffer = BitConverter.ToUInt32(rom, pos); pos += 4; bits = 32;
                }
                int b = (int)(buffer >> 31); buffer <<= 1; bits--;
                return b;
            }
            int Bits(int n) { int v = 0; for (int i = 0; i < n; i++) v = (v << 1) | Bit(); return v; }
            int Int() { int v = 1; do v = (v << 1) + Bit(); while (Bit() != 0); return v; }
            int sz = size;
            void Copy(int distance, int length)
            {
                if (distance <= 0 || distance > produced || produced + length > sz) throw new InvalidDataException();
                produced += length;
            }

            try
            {
                int last = 1, indexBase = 8, literalBits = 8;
                while (true)
                {
                    if (produced > size) return false;
                    if (Bit() == 1) { Bits(literalBits); produced++; }
                    else if (Bit() == 1)
                    {
                        int high = Int();
                        if (high == 2) Copy(last, Int());
                        else
                        {
                            last = ((high - 3) << indexBase) + Bits(indexBase);
                            int length = Int();
                            if (last >= 0x10000) length += 3;
                            else if (last >= 0x37FF) length += 2;
                            else if (last >= 0x27F) length += 1;
                            else if (last <= 127) length += 4;
                            Copy(last, length);
                        }
                    }
                    else if (Bit() == 1)
                    {
                        int v = Bits(4) - 1;
                        if (v == 0) produced++;
                        else if (v > 0) Copy(v, 1);
                        else if (Bit() == 1)
                        {
                            do { if (produced + 256 > size) return false; Bits(8 * 256); produced += 256; } while (Bit() == 1);
                        }
                        else
                        {
                            literalBits = 7 + Bit();
                            if (literalBits != 8) Bits(8);
                        }
                    }
                    else
                    {
                        int index = Bits(7), length = 2 + Bits(2);
                        if (index == 0)
                        {
                            if (length == 2) break;
                            indexBase = Bits(length + 1);
                        }
                        else { last = index; Copy(index, length); }
                    }
                }
            }
            catch (InvalidDataException) { return false; }

            end = pos;
            return produced == size;
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