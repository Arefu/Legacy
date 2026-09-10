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
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(newPosition, Length);

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
        /// AVOID THIS for anything the game needs to read back at runtime: real
        /// data confirmed appended this way (44 bytes past the original 8MB file)
        /// still crashed the game on load, even though the bytes themselves were
        /// correct -- most likely because emulators/hardware map ROM based on the
        /// cartridge's original/detected size, and reads past that boundary return
        /// GBA "open bus" garbage rather than the actual appended bytes, not a
        /// file-reading problem at all. Prefer <see cref="AllocateFreeSpace"/>,
        /// which writes into real unused space inside the original file bounds.
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
        /// Finds and hands out chunks of real, unused space INSIDE the ROM's
        /// original file bounds, instead of growing the file. GBA ROMs are
        /// commonly padded to their declared size with a repeated fill byte
        /// (confirmed for this ROM: a 6856-byte run of 0xFF at the end) --
        /// on first call this locates that trailing run by scanning backward
        /// from the end of the buffer while bytes match the last byte's value,
        /// then hands out sequential chunks from it on each call. Throws if the
        /// padding run isn't big enough for everything requested in one ROM's
        /// lifetime -- there's no reclaiming/reuse across separate save
        /// operations on the same file today.
        /// </summary>
        public int AllocateFreeSpace(int size)
        {
            if (_freeSpaceCursor < 0)
            {
                _originalLength = mem.Length;
                byte fill = mem[mem.Length - 1];
                int i = mem.Length - 1;
                while (i > 0 && mem[i] == fill) i--;
                _freeSpaceCursor = i + 1;
            }

            if (_freeSpaceCursor + size > _originalLength)
            {
                throw new InvalidOperationException(
                    $"Not enough unused space left inside the ROM to place new data " +
                    $"(need {size} bytes, only {_originalLength - _freeSpaceCursor} left in the padding region).");
            }

            int start = _freeSpaceCursor;
            _freeSpaceCursor += size;
            return start;
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
