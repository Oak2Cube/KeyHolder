using System.Globalization;

namespace KeyHolder.Domain.Entries;

/// <summary>
/// One field-level validation problem.
/// </summary>
public sealed record ValidationError(string Field, string Message);

/// <summary>
/// Thrown when an entry that fails validation reaches code that requires a valid entry.
/// </summary>
public sealed class EntryValidationException : Exception
{
    public EntryValidationException()
        : this([new ValidationError("Entry", "The entry is invalid.")])
    {
    }

    public EntryValidationException(string message)
        : base(message)
    {
        Errors = [new ValidationError("Entry", message)];
    }

    public EntryValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [new ValidationError("Entry", message)];
    }

    public EntryValidationException(IReadOnlyList<ValidationError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<ValidationError> errors) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Entry validation failed: {string.Join("; ", errors.Select(e => $"{e.Field}: {e.Message}"))}");
}
