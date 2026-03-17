using System;
using System.Security.Cryptography;
using System.Text;

namespace Shared
{
    public static class Hasher
    {
        public static string GetHashFromString(object input, bool noSpecialChars = true)
        {
            using SHA256 shaAlgorythm = SHA256.Create();
            byte[] code = shaAlgorythm.ComputeHash(Encoding.ASCII.GetBytes(input.ToString()));

            if (noSpecialChars) return BitConverter.ToString(code).Replace("-", "");
            else return BitConverter.ToString(code);
        }
        
        public static uint GetHashFromIpv4(string ip)
        {
            uint result = 0;
            int shift = 24;
            int acc = 0;

            for (int i = 0; i < ip.Length; i++)
            {
                char c = ip[i];
                if (c == '.')
                {
                    result |= (uint)acc << shift;
                    shift -= 8;
                    acc = 0;
                }
                else
                {
                    acc = acc * 10 + (c - '0');
                }
            }

            result |= (uint)acc << shift;
            return result;
        }
    }
}
