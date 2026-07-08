using System.Text;

namespace DrGero.IO
{
    public class ROM
    {
        private readonly byte[] mem;
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

        private void CheckLength(int n)
        {
            if (n > Remaining)
                throw new InvalidOperationException();
        }

    }
}
