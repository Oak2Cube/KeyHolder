using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace KeyHolder.Desktop.Views;

/// <summary>
/// Grows a window when its panels need more room, up to the work area of the monitor it is on.
/// The window never shrinks by itself. Once the limit is reached, the panels show their own scrollbars.
/// </summary>
public static partial class AutoGrowWindow
{
    private const int MonitorDefaultToNearest = 2;
    private const int EnterSizeMove = 0x0231;
    private const int ExitSizeMove = 0x0232;

    private static readonly ConditionalWeakTable<Window, ResizeState> States = new();

    private sealed class ResizeState
    {
        public bool IsUserResizing { get; set; }
    }

    /// <summary>
    /// Calculates the new window bounds. The window grows by the deficit but never beyond the work area,
    /// and it is moved back inside the work area when the growth would cross an edge.
    /// A zero (or negative) deficit leaves the bounds unchanged.
    /// </summary>
    public static Rect Compute(Rect window, Size deficit, Rect workArea)
    {
        var growWidth = Math.Max(0, deficit.Width);
        var growHeight = Math.Max(0, deficit.Height);
        if (growWidth == 0 && growHeight == 0)
        {
            return window;
        }

        // A window that is already bigger than the work area is not shrunk.
        var width = Math.Max(window.Width, Math.Min(window.Width + growWidth, workArea.Width));
        var height = Math.Max(window.Height, Math.Min(window.Height + growHeight, workArea.Height));

        var x = ClampStart(window.X, width, workArea.Left, workArea.Right);
        var y = ClampStart(window.Y, height, workArea.Top, workArea.Bottom);
        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// Starts watching the window for manual resizing (and moving), so auto-grow never fights the user's mouse.
    /// Call once, after the window handle exists (for example in <c>SourceInitialized</c>).
    /// </summary>
    public static void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var source = HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle());
        var state = States.GetOrCreateValue(window);
        source?.AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
        {
            if (message == EnterSizeMove)
            {
                state.IsUserResizing = true;
            }
            else if (message == ExitSizeMove)
            {
                state.IsUserResizing = false;
            }

            return 0;
        });
    }

    /// <summary>
    /// Grows the window by the given amounts (device independent pixels) if it is in the normal state.
    /// </summary>
    public static void Fit(Window window, double deficitWidth, double deficitHeight)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.WindowState != WindowState.Normal || (deficitWidth <= 0 && deficitHeight <= 0))
        {
            return;
        }

        // While the user drags a window edge, the window must follow the mouse, not the content.
        if (States.TryGetValue(window, out var state) && state.IsUserResizing)
        {
            return;
        }

        var workArea = GetWorkArea(window);
        var current = new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
        var next = Compute(
            current,
            new Size(Math.Ceiling(Math.Max(0, deficitWidth)), Math.Ceiling(Math.Max(0, deficitHeight))),
            workArea);

        if (next == current)
        {
            return;
        }

        window.Left = next.X;
        window.Top = next.Y;
        window.Width = next.Width;
        window.Height = next.Height;
    }

    private static double ClampStart(double start, double length, double min, double max)
    {
        if (start + length > max)
        {
            start = max - length;
        }

        return Math.Max(start, min);
    }

    private static Rect GetWorkArea(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var scale = VisualTreeHelper.GetDpi(window);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (handle != 0 && GetMonitorInfo(MonitorFromWindow(handle, MonitorDefaultToNearest), ref info))
        {
            return new Rect(
                info.Work.Left / scale.DpiScaleX,
                info.Work.Top / scale.DpiScaleY,
                (info.Work.Right - info.Work.Left) / scale.DpiScaleX,
                (info.Work.Bottom - info.Work.Top) / scale.DpiScaleY);
        }

        return SystemParameters.WorkArea;
    }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint window, int flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
