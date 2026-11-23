using System.Globalization;

namespace KeyHolder.Domain.Entries;

/// <summary>
/// Normalizes and validates entry data. Normalization always runs before validation,
/// so callers never have to trim or de-duplicate by hand.
/// </summary>
public static class EntryValidator
{
    /// <summary>
    /// Trims text, converts blank optional text to <see langword="null"/>, and cleans both tag lists.
    /// </summary>
    public static EntryMetadata Normalize(EntryMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new EntryMetadata(
            (metadata.Title ?? string.Empty).Trim(),
            NormalizeOptional(metadata.Username),
            NormalizeOptional(metadata.Email),
            NormalizeOptional(metadata.Url),
            NormalizeTags(metadata.DTags),
            NormalizeTags(metadata.STags),
            metadata.Importance);
    }

    /// <summary>
    /// Converts blank password and notes to <see langword="null"/>. Values are not trimmed,
    /// because leading or trailing spaces may be part of a password.
    /// </summary>
    public static EntrySecret Normalize(EntrySecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return new EntrySecret(
            string.IsNullOrEmpty(secret.Password) ? null : secret.Password,
            string.IsNullOrWhiteSpace(secret.Notes) ? null : secret.Notes);
    }

    /// <summary>
    /// Returns the validation errors for the metadata (empty when valid).
    /// The metadata is normalized first.
    /// </summary>
    public static IReadOnlyList<ValidationError> Validate(EntryMetadata metadata)
    {
        var normalized = Normalize(metadata);
        var errors = new List<ValidationError>();

        if (normalized.Title.Length == 0)
        {
            errors.Add(new ValidationError(EntryFields.Title, "Title is required."));
        }

        CheckLength(errors, EntryFields.Title, normalized.Title, EntryLimits.MaxTitleLength);
        CheckLength(errors, EntryFields.Username, normalized.Username, EntryLimits.MaxUsernameLength);
        CheckLength(errors, EntryFields.Email, normalized.Email, EntryLimits.MaxEmailLength);
        CheckLength(errors, EntryFields.Url, normalized.Url, EntryLimits.MaxUrlLength);
        CheckTags(errors, EntryFields.DTags, normalized.DTags);
        CheckTags(errors, EntryFields.STags, normalized.STags);

        if (normalized.Importance is { } importance
            && (importance < EntryLimits.MinImportance || importance > EntryLimits.MaxImportance))
        {
            errors.Add(new ValidationError(
                EntryFields.Importance,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Importance must be between {EntryLimits.MinImportance} and {EntryLimits.MaxImportance}.")));
        }

        return errors;
    }

    /// <summary>
    /// Returns the validation errors for the secret part (empty when valid).
    /// </summary>
    public static IReadOnlyList<ValidationError> Validate(EntrySecret secret)
    {
        var normalized = Normalize(secret);
        var errors = new List<ValidationError>();

        CheckLength(errors, EntryFields.Password, normalized.Password, EntryLimits.MaxPasswordLength);
        CheckLength(errors, EntryFields.Notes, normalized.Notes, EntryLimits.MaxNotesLength);

        return errors;
    }

    /// <summary>
    /// Returns the normalized metadata, or throws when it is invalid.
    /// </summary>
    /// <exception cref="EntryValidationException">The metadata is invalid.</exception>
    public static EntryMetadata EnsureValid(EntryMetadata metadata)
    {
        var errors = Validate(metadata);
        return errors.Count == 0 ? Normalize(metadata) : throw new EntryValidationException(errors);
    }

    /// <summary>
    /// Returns the normalized secret, or throws when it is invalid.
    /// </summary>
    /// <exception cref="EntryValidationException">The secret is invalid.</exception>
    public static EntrySecret EnsureValid(EntrySecret secret)
    {
        var errors = Validate(secret);
        return errors.Count == 0 ? Normalize(secret) : throw new EntryValidationException(errors);
    }

    /// <summary>
    /// Trims tags, drops blank tags, and removes case-insensitive duplicates while keeping the first spelling.
    /// </summary>
    public static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var tag in tags)
        {
            var normalized = NormalizeTag(tag);
            if (normalized.Length > 0 && seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    /// <summary>
    /// Trims a single tag and collapses runs of white space inside it into one space.
    /// </summary>
    public static string NormalizeTag(string? tag) =>
        tag is null
            ? string.Empty
            : string.Join(' ', tag.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Checks one new tag against the tags already in the same list.
    /// </summary>
    /// <returns>A short message for the user, or <see langword="null"/> when the tag can be added.</returns>
    public static string? ValidateNewTag(string? name, IReadOnlyCollection<string> existingTags)
    {
        ArgumentNullException.ThrowIfNull(existingTags);

        var normalized = NormalizeTag(name);
        if (normalized.Length == 0)
        {
            return "Enter a tag name.";
        }

        var nameError = TagNameError(normalized);
        if (nameError is not null)
        {
            return nameError;
        }

        if (existingTags.Any(tag => string.Equals(NormalizeTag(tag), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return "This tag is already on the entry.";
        }

        return existingTags.Count >= EntryLimits.MaxTagsPerList
            ? string.Create(CultureInfo.InvariantCulture, $"At most {EntryLimits.MaxTagsPerList} tags are allowed.")
            : null;
    }

    private static string? TagNameError(string tag)
    {
        if (tag.Length > EntryLimits.MaxTagLength)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Each tag must be at most {EntryLimits.MaxTagLength} characters.");
        }

        if (tag.Any(char.IsControl))
        {
            return "A tag cannot contain control characters.";
        }

        return tag.Contains(',', StringComparison.Ordinal) || tag.Contains(';', StringComparison.Ordinal)
            ? "A tag cannot contain commas or semicolons."
            : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static void CheckLength(List<ValidationError> errors, string field, string? value, int maxLength)
    {
        if (value is not null && value.Length > maxLength)
        {
            errors.Add(new ValidationError(
                field,
                string.Create(CultureInfo.InvariantCulture, $"{field} must be at most {maxLength} characters.")));
        }
    }

    private static void CheckTags(List<ValidationError> errors, string field, IReadOnlyList<string> tags)
    {
        if (tags.Count > EntryLimits.MaxTagsPerList)
        {
            errors.Add(new ValidationError(
                field,
                string.Create(CultureInfo.InvariantCulture, $"At most {EntryLimits.MaxTagsPerList} tags are allowed.")));
        }

        foreach (var message in tags.Select(TagNameError).Where(message => message is not null).Distinct())
        {
            errors.Add(new ValidationError(field, message!));
        }
    }
}
