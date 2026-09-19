using System.Text;

namespace DrGero.IO
{
    public class ROM
    {
        private byte[] mem;
        private int _position;
        private Stack<int> positionStack = new();

        public int Position => _position;
        public int Length => mem.Length;
        public int Remaining => Length - _position;

        private ROM(byte[] _mem)
        {
            mem = _mem;
        }

        public static ROM FromFile(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            byte[] bytes = File.ReadAllBytes(filePath);
            return new ROM(bytes);
        }

        public static ROM FromBytes(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);

            return new ROM(bytes);
        }

        public void Seek(int newPosition)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(newPosition, 0);
            // newPosition == Length is a valid "just past the last byte" position (the same
            // state reading the final byte of the file naturally leaves _position in via
            // Read()'s mem[_position++]) -- it just can't be READ from, which CheckLength
            // already enforces on every Read*/Skip call. Rejecting it here too used to make
            // PushPosition/PopPosition crash any time a push/pop pair straddled a read that
            // ended exactly at EOF -- confirmed via a real crash (2026-09-19): a brand new
            // trigger appended at the very end of a growing ROM, whose 8-byte
            // {vTable,dataPtr} array entry was also the last 8 bytes in the file, left
            // _position == Length after reading it; the next PushPosition/PopPosition pair
            // around the trigger's own data (a legitimate, unrelated read) then threw trying
            // to restore that saved position. Only reject genuinely too-far positions.
            ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, Length);

            _position = newPosition;
        }

        public void Skip(int n)
        {
            CheckLength(n);
            _position += n;
        }

        public void PushPosition(int newPosition)
        {
            int current = _position;
            Seek(newPosition);
            positionStack.Push(current);
        }

        public void PopPosition()
        {
            if (positionStack.Count == 0)
                throw new InvalidOperationException();

            int pos = positionStack.Pop();
            Seek(pos);
        }

        public int ReadByte()
        {
            CheckLength(1);
            return Read();
        }

        public int ReadShort()
        {
            CheckLength(2);
            return Read() | (Read() << 8);
        }

        public int ReadShortBigEndian()
        {
            CheckLength(2);
            return (Read() << 8) | Read();
        }

        public int ReadInt()
        {
            CheckLength(4);
            return Read() | (Read() << 8) | (Read() << 16) | (Read() << 24);
        }

        public int ReadPointer()
        {
            int value = ReadInt();
            // GBA pointers: 0x08xxxxxx (ROM), 0x09xxxxxx (ROM mirror), 0x02xxxxxx (EWRAM), etc.
            // Strip the top byte to get the raw ROM offset.
            if ((value & 0xFF000000) != 0)
            {
                return value & 0x00FFFFFF;
            }
            return value;
        }
        public string ReadUnicodeString()
        {
            int startPos = _position;
            int length = 0;
            while (ReadShort() != 0)
            {
                length += 2;
            }
            return Encoding.Unicode.GetString(mem, startPos, length);
        }
        public string ReadNullTerminatedString()
        {
            var sb = new StringBuilder();
            int b;
            while ((b = ReadByte()) != 0)
                sb.Append((char)b);
            return sb.ToString();
        }
        private byte Read()
        {
            return mem[_position++];
        }

        /// <summary>Overwrites 2 bytes at an absolute ROM file offset (little-endian).</summary>
        public void PatchInt16(int address, short value)
        {
            CheckBounds(address, 2);
            mem[address] = (byte)(value & 0xFF);
            mem[address + 1] = (byte)((value >> 8) & 0xFF);
        }

        /// <summary>Overwrites 4 bytes at an absolute ROM file offset (little-endian).</summary>
        public void PatchInt32(int address, int value)
        {
            CheckBounds(address, 4);
            mem[address] = (byte)(value & 0xFF);
            mem[address + 1] = (byte)((value >> 8) & 0xFF);
            mem[address + 2] = (byte)((value >> 16) & 0xFF);
            mem[address + 3] = (byte)((value >> 24) & 0xFF);
        }

        /// <summary>Overwrites 1 byte at an absolute ROM file offset.</summary>
        public void PatchByte(int address, byte value)
        {
            CheckBounds(address, 1);
            mem[address] = value;
        }

        /// <summary>Reads a block of raw bytes at an absolute ROM file offset without moving Position.</summary>
        public byte[] ReadBytesAt(int address, int count)
        {
            CheckBounds(address, count);
            byte[] result = new byte[count];
            Buffer.BlockCopy(mem, address, result, 0, count);
            return result;
        }

        /// <summary>
        /// Grows the ROM buffer by appending <paramref name="data"/> at the end and
        /// returns the file offset it now lives at.
        ///
        /// A prior session saw this crash on load (44 bytes past the original 8MB
        /// file, correct bytes, still failed) and this method was avoided ever
        /// since. Per direct user confirmation, mGBA (our actual test target) is
        /// fine straddling/extending past the original file size -- that old
        /// failure was most likely something else at the time, or specific to
        /// whatever tool/hardware was used to check it back then, not a general
        /// GBA/mGBA rule. <see cref="AllocateFreeSpace"/> now falls back to this
        /// automatically once its small trailing-padding pool runs out, so this
        /// is called routinely, not just as a last resort.
        /// </summary>
        public int AppendBytes(byte[] data)
        {
            int start = mem.Length;
            byte[] grown = new byte[mem.Length + data.Length];
            Buffer.BlockCopy(mem, 0, grown, 0, mem.Length);
            Buffer.BlockCopy(data, 0, grown, start, data.Length);
            mem = grown;
            return start;
        }

        private int _freeSpaceCursor = -1;
        private int _originalLength = -1;

        /// <summary>
        /// Hands out chunks of unused space for new data, preferring real padding
        /// INSIDE the ROM's original file bounds over growing the file. GBA ROMs
        /// are commonly padded to their declared size with a repeated fill byte
        /// (confirmed for this ROM: a 6856-byte run of 0xFF at the end) -- on
        /// first call this locates that trailing run by scanning backward from
        /// the end of the buffer while bytes match the last byte's value, then
        /// hands out sequential chunks from it on each call.
        ///
        /// That padding pool is small and shared across everything one save
        /// touches (new objects, items, characters, and the pointer arrays that
        /// reference them all), so it's easy to exhaust in a single edit
        /// session. Once it's gone, this falls back to <see cref="AppendBytes"/>
        /// (growing the file) rather than throwing -- confirmed fine on mGBA,
        /// our test target. There's still no reclaiming/reuse of either pool
        /// across separate save operations on the same file.
        /// </summary>
        public int AllocateFreeSpace(int size)
        {
            // Every allocation starts on a 4-byte boundary (sizes are rounded up to a multiple of 4,
            // and the pool/file end is aligned before the first hand-out). FIXED 2026-09-19: this
            // used to hand out back-to-back exact-size chunks, so after any odd-sized allocation the
            // next one was misaligned -- and the GBA's 32-bit loads from a misaligned ROM address
            // return a ROTATED word. The game reads resource headers (format/size) and pointer
            // arrays that way, so a misaligned stored resource was decoded as garbage (a collision
            // grid that made the player unable to move, for one). Aligned space costs at most 3
            // bytes per allocation.
            size = (size + 3) & ~3;

            if (_freeSpaceCursor < 0)
            {
                _originalLength = mem.Length;
                byte fill = mem[mem.Length - 1];
                int i = mem.Length - 1;
                while (i > 0 && mem[i] == fill) i--;
                _freeSpaceCursor = (i + 1 + 3) & ~3;
            }

            if (_freeSpaceCursor + size <= _originalLength)
            {
                int start = _freeSpaceCursor;
                _freeSpaceCursor += size;
                return start;
            }

            // Padding pool exhausted -- grow the file instead of throwing. Each new
            // chunk lands past the previous one since AppendBytes always writes at
            // the current mem.Length, so callers still get back-to-back, non-
            // overlapping addresses exactly like the padding-pool path above.
            // Keep the file end aligned too, so the appended chunk starts on a 4-byte boundary.
            int misalignment = mem.Length & 3;
            if (misalignment != 0) AppendBytes(new byte[4 - misalignment]);
            return AppendBytes(new byte[size]);
        }

        /// <summary>Overwrites a block of bytes at an absolute ROM file offset.</summary>
        public void WriteBytesAt(int address, byte[] data)
        {
            CheckBounds(address, data.Length);
            Buffer.BlockCopy(data, 0, mem, address, data.Length);
        }

        /// <summary>Zeroes a block of bytes, e.g. to clear out storage made obsolete by AppendBytes.</summary>
        public void ZeroBytes(int address, int count)
        {
            CheckBounds(address, count);
            Array.Clear(mem, address, count);
        }

        /// <summary>Returns a copy of the full ROM buffer, e.g. for writing out an edited ROM.</summary>
        public byte[] ToArray() => (byte[])mem.Clone();

        private void CheckBounds(int address, int n)
        {
            if (address < 0 || address + n > Length)
                throw new ArgumentOutOfRangeException(nameof(address));
        }

        private void CheckLength(int n)
        {
            if (n > Remaining)
                throw new InvalidOperationException();
        }

    }
}
