using KeyHolder.Cryptography;

namespace KeyHolder.UnitTests;

public sealed class PasswordGeneratorTests
{
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.?";

    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(128)]
    public void GeneratesRequestedLength(int length)
    {
        var password = new PasswordGenerator().Generate(new PasswordGeneratorOptions { Length = length });

        Assert.Equal(length, password.Length);
    }

    [Fact]
    public void EveryEnabledClassAppearsAtLeastOnce()
    {
        var generator = new PasswordGenerator();
        var options = new PasswordGeneratorOptions { Length = PasswordGeneratorOptions.MinLength };

        for (var i = 0; i < 200; i++)
        {
            var password = generator.Generate(options);

            Assert.Contains(password, char.IsAsciiLetterLower);
            Assert.Contains(password, char.IsAsciiLetterUpper);
            Assert.Contains(password, char.IsAsciiDigit);
            Assert.Contains(password, c => Symbols.Contains(c, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void DisabledClassesNeverAppear()
    {
        var generator = new PasswordGenerator();
        var options = new PasswordGeneratorOptions
        {
            Length = 64,
            IncludeSymbols = false,
            IncludeDigits = false,
            IncludeUppercase = false,
        };

        var password = generator.Generate(options);

        Assert.All(password, c => Assert.True(char.IsAsciiLetterLower(c)));
    }

    [Fact]
    public void AmbiguousCharactersAreExcludedWhenRequested()
    {
        var generator = new PasswordGenerator();
        var options = new PasswordGeneratorOptions { Length = 128, ExcludeAmbiguous = true };

        for (var i = 0; i < 50; i++)
        {
            var password = generator.Generate(options);

            Assert.DoesNotContain(password, c => "Il1O0o".Contains(c, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void SeededSourceGivesDeterministicResult()
    {
        var options = new PasswordGeneratorOptions { Length = 16 };

        var first = new PasswordGenerator(new SequenceIndexSource()).Generate(options);
        var second = new PasswordGenerator(new SequenceIndexSource()).Generate(options);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SecurePasswordsDiffer()
    {
        var generator = new PasswordGenerator();
        var options = new PasswordGeneratorOptions();

        var passwords = Enumerable.Range(0, 20).Select(_ => generator.Generate(options)).ToHashSet();

        Assert.Equal(20, passwords.Count);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(129)]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidLengthIsRejected(int length)
    {
        var generator = new PasswordGenerator();

        Assert.Throws<ArgumentException>(
            () => generator.Generate(new PasswordGeneratorOptions { Length = length }));
    }

    [Fact]
    public void NoClassEnabledIsRejected()
    {
        var options = new PasswordGeneratorOptions
        {
            IncludeLowercase = false,
            IncludeUppercase = false,
            IncludeDigits = false,
            IncludeSymbols = false,
        };

        Assert.Throws<ArgumentException>(() => new PasswordGenerator().Generate(options));
    }

    [Fact]
    public void NullOptionsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new PasswordGenerator().Generate(null!));
    }

    private sealed class SequenceIndexSource : IRandomIndexSource
    {
        private int _next;

        public int NextIndex(int exclusiveMax) => _next++ % exclusiveMax;
    }
}
