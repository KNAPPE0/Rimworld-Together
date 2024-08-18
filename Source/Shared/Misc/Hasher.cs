using System;
using System.Security.Cryptography;
using System.Text;

namespace Shared
{
    // This class contains tools to create hashes from various inputs
    public static class Hasher
    {
        // Generates a SHA-256 hash from a given string
        public static string GetHashFromString(string input, bool noSpecialChars = true)
        {
            using SHA256 shaAlgorithm = SHA256.Create();
            byte[] hashBytes = shaAlgorithm.ComputeHash(Encoding.ASCII.GetBytes(input));

            return noSpecialChars ? BitConverter.ToString(hashBytes).Replace("-", "") : BitConverter.ToString(hashBytes);
        }

        // Generates a SHA-256 hash from a given byte array
        public static string GetHashFromBytes(byte[] input, bool noSpecialChars = true)
        {
            using SHA256 shaAlgorithm = SHA256.Create();
            byte[] hashBytes = shaAlgorithm.ComputeHash(input);

            return noSpecialChars ? BitConverter.ToString(hashBytes).Replace("-", "") : BitConverter.ToString(hashBytes);
        }
    }
}