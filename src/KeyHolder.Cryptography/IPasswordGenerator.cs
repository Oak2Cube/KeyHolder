namespace KeyHolder.Cryptography;

/// <summary>
/// Creates random passwords. Only the cryptography layer chooses the random source.
/// </summary>
public interface IPasswordGenerator
{
    /// <exception cref="ArgumentException">The options are invalid.</exception>
    string Generate(PasswordGeneratorOptions options);
}
