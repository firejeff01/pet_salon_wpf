using System.Security.Cryptography;
using PetSalon.Core.Abstractions;

namespace PetSalon.Infrastructure.Identity;

/// <summary>Nanoid-style URL-safe ID; uses cryptographically secure RNG.</summary>
public sealed class NanoIdGenerator : IIdGenerator
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int Length = 12;
    private const int Mask = 63;

    public string New(string prefix)
    {
        Span<char> chars = stackalloc char[Length];
        Span<byte> bytes = stackalloc byte[Length * 2];

        var written = 0;
        while (written < Length)
        {
            RandomNumberGenerator.Fill(bytes);
            foreach (var b in bytes)
            {
                var idx = b & Mask;
                if (idx < Alphabet.Length)
                {
                    chars[written++] = Alphabet[idx];
                    if (written == Length) break;
                }
            }
        }

        return $"{prefix}_{new string(chars)}";
    }
}
