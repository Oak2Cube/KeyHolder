namespace KeyHolder.Domain.Entries;

/// <summary>
/// The secret part of an entry. In Milestone 2 this becomes the <c>secret_envelope</c> payload.
/// </summary>
/// <param name="Password">Optional password.</param>
/// <param name="Notes">Optional private notes.</param>
public sealed record EntrySecret(string? Password = null, string? Notes = null)
{
    public static EntrySecret Empty { get; } = new();

    /// <summary>
    /// Prevents accidental logging of secret values through <c>ToString</c>.
    /// </summary>
    public override string ToString() => "EntrySecret { ***** }";
}
