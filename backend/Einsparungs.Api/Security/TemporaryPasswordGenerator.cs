using System.Security.Cryptography;

namespace Einsparungs.Api.Security;

public sealed class TemporaryPasswordGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@$%*-_+";
    private const string AllCharacters = Uppercase + Lowercase + Digits + Symbols;

    public string Generate(int length = 20)
    {
        if (length < 12)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Temporary passwords need at least 12 characters.");
        }

        var characters = new List<char>(length)
        {
            Pick(Uppercase),
            Pick(Lowercase),
            Pick(Digits),
            Pick(Symbols)
        };

        while (characters.Count < length)
        {
            characters.Add(Pick(AllCharacters));
        }

        for (var index = characters.Count - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }

        return new string(characters.ToArray());
    }

    private static char Pick(string characters)
    {
        return characters[RandomNumberGenerator.GetInt32(characters.Length)];
    }
}
