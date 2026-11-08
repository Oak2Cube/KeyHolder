namespace KeyHolder.Desktop.Services;

/// <summary>
/// Puts secrets on the clipboard for a limited time.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies the text, asks Windows to keep it out of clipboard history and cloud sync,
    /// and clears it after <paramref name="clearAfter"/> unless something else was copied meanwhile.
    /// </summary>
    void SetSecret(string text, TimeSpan clearAfter);

    /// <summary>
    /// Clears the clipboard only if it still holds the value this app copied.
    /// </summary>
    void ClearIfOwned();
}
