namespace KeyHolder.Cryptography;

/// <summary>
/// Settings for <see cref="IPasswordGenerator"/>.
/// </summary>
public sealed record PasswordGeneratorOptions
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
    public const int DefaultLength = 20;

    public int Length { get; init; } = DefaultLength;

    public bool IncludeLowercase { get; init; } = true;

    public bool IncludeUppercase { get; init; } = true;

    public bool IncludeDigits { get; init; } = true;

    public bool IncludeSymbols { get; init; } = true;

    /// <summary>
    /// Leaves out characters that look alike: I, l, 1, O, 0, and o.
    /// </summary>
    public bool ExcludeAmbiguous { get; init; }

    /// <summary>
    /// Throws when the options cannot produce a password.
    /// </summary>
    /// <exception cref="ArgumentException">The length is out of range or no character class is enabled.</exception>
    public void EnsureValid()
    {
        if (Length is < MinLength or > MaxLength)
        {
            throw new ArgumentException(
                $"Length must be between {MinLength} and {MaxLength}.",
                nameof(Length));
        }

        if (!(IncludeLowercase || IncludeUppercase || IncludeDigits || IncludeSymbols))
        {
            throw new ArgumentException("At least one character class must be enabled.", "options");
        }
    }
}
