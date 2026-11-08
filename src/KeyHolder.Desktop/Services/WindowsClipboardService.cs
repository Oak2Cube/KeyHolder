using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace KeyHolder.Desktop.Services;

/// <summary>
/// WPF clipboard with Windows clipboard-history exclusion and an ownership check.
/// </summary>
internal sealed partial class WindowsClipboardService : IClipboardService, IDisposable
{
    private const int RetryCount = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly DispatcherTimer _timer = new();
    private uint _ownedSequence;
    private bool _owns;

    public WindowsClipboardService()
    {
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            ClearIfOwned();
        };
    }

    public void SetSecret(string text, TimeSpan clearAfter)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);

        // Windows 10/11 clipboard history, cloud clipboard, and monitors honor these formats.
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(BitConverter.GetBytes(1)));
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));

        Retry(() => Clipboard.SetDataObject(data, copy: false));

        _ownedSequence = GetClipboardSequenceNumber();
        _owns = true;

        _timer.Stop();
        _timer.Interval = clearAfter;
        _timer.Start();
    }

    public void ClearIfOwned()
    {
        _timer.Stop();

        if (!_owns)
        {
            return;
        }

        _owns = false;

        // If another app copied something since, the sequence number changed and we leave it alone.
        if (GetClipboardSequenceNumber() == _ownedSequence)
        {
            Retry(Clipboard.Clear);
        }
    }

    public void Dispose() => ClearIfOwned();

    [LibraryImport("user32.dll")]
    private static partial uint GetClipboardSequenceNumber();

    private static void Retry(Action action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (COMException) when (attempt < RetryCount)
            {
                // The clipboard may be locked by another process for a moment.
                Thread.Sleep(RetryDelay);
            }
        }
    }
}
