namespace Legacy
{
    internal static class SView_Tools
    {
        internal static long ZigZagDecode(int v)
        {
            return (v & 1) != 0 ? -(v >> 1) : (v >> 1);
        }

        internal static string CleanName(string name)
        {
            const string prefix = "BytecodeVM_";

            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return name[prefix.Length..];

            return name;
        }

        internal static bool TryParseHex(string input, out byte[]? bytes, bool isScript = true)
        {
            bytes = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var tokens = input.Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var result = new List<byte>();

            foreach (var rawToken in tokens)
            {
                string token = rawToken.Trim();

                if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    token = token[2..];

                if (token.Length == 0)
                    return false;

                for (int i = 0; i < token.Length; i++)
                {
                    char c = token[i];

                    bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

                    if (!isHex)
                        return false;
                }

                if (!byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                    return false;

                result.Add(b);

                if (b == 0x11 && isScript == true)
                    break;
            }

            if (result.Count == 0)
                return false;

            bytes = result.ToArray();
            return true;
        }
    }
}