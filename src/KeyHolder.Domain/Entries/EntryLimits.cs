namespace KeyHolder.Domain.Entries;

/// <summary>
/// Size limits that keep entries small and bounded.
/// </summary>
public static class EntryLimits
{
    public const int MaxTitleLength = 200;
    public const int MaxUsernameLength = 200;
    public const int MaxEmailLength = 320;
    public const int MaxUrlLength = 2048;
    public const int MaxTagLength = 50;
    public const int MaxTagsPerList = 20;
    public const int MinImportance = 0;
    public const int MaxImportance = 100;
    public const int MaxPasswordLength = 1024;
    public const int MaxNotesLength = 10_000;
}
