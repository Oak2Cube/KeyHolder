using System.Security.Cryptography;

namespace KeyHolder.Cryptography;

/// <summary>
/// Generates passwords with a cryptographically secure random number generator.
/// Every enabled character class appears at least once; the order is then shuffled.
/// </summary>
public sealed class PasswordGenerator : IPasswordGenerator
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.?";
    private const string Ambiguous = "Il1O0o";

    private readonly IRandomIndexSource _random;

    public PasswordGenerator()
        : this(new SecureRandomIndexSource())
    {
    }

    internal PasswordGenerator(IRandomIndexSource random)
    {
        _random = random;
    }

    public string Generate(PasswordGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();

        var classes = BuildClasses(options);
        var all = string.Concat(classes);

        var characters = new char[options.Length];

        for (var i = 0; i < classes.Count; i++)
        {
            characters[i] = Pick(classes[i]);
        }

        for (var i = classes.Count; i < characters.Length; i++)
        {
            characters[i] = Pick(all);
        }

        Shuffle(characters);

        return new string(characters);
    }

    private static List<string> BuildClasses(PasswordGeneratorOptions options)
    {
        var classes = new List<string>();

        AddClass(classes, options.IncludeLowercase, Lowercase, options.ExcludeAmbiguous);
        AddClass(classes, options.IncludeUppercase, Uppercase, options.ExcludeAmbiguous);
        AddClass(classes, options.IncludeDigits, Digits, options.ExcludeAmbiguous);
        AddClass(classes, options.IncludeSymbols, Symbols, excludeAmbiguous: false);

        return classes;
    }

    private static void AddClass(List<string> classes, bool include, string characters, bool excludeAmbiguous)
    {
        if (!include)
        {
            return;
        }

        classes.Add(excludeAmbiguous
            ? new string(characters.Where(c => !Ambiguous.Contains(c, StringComparison.Ordinal)).ToArray())
            : characters);
    }

    private char Pick(string alphabet) => alphabet[_random.NextIndex(alphabet.Length)];

    private void Shuffle(char[] characters)
    {
        for (var i = characters.Length - 1; i > 0; i--)
        {
            var j = _random.NextIndex(i + 1);
            (characters[i], characters[j]) = (characters[j], characters[i]);
        }
    }
}

/// <summary>
/// Picks uniform random indexes. A seam so tests can use a deterministic source.
/// </summary>
internal interface IRandomIndexSource
{
    /// <returns>A value from 0 (inclusive) to <paramref name="exclusiveMax"/> (exclusive).</returns>
    int NextIndex(int exclusiveMax);
}

internal sealed class SecureRandomIndexSource : IRandomIndexSource
{
    public int NextIndex(int exclusiveMax) => RandomNumberGenerator.GetInt32(exclusiveMax);
}
