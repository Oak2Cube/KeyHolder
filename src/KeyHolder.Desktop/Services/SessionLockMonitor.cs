using System.Windows;
using KeyHolder.Domain.Locking;
using Microsoft.Win32;

namespace KeyHolder.Desktop.Services;

/// <summary>
/// Locks the vault when Windows locks the session (Win+L) or the user disconnects.
/// </summary>
internal sealed class SessionLockMonitor : IDisposable
{
    private readonly VaultLockPolicy _policy;
    private bool _started;

    public SessionLockMonitor(VaultLockPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public void Dispose()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    // SystemEvents are raised on a background thread. The policy and view models belong to the UI thread.
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is not (SessionSwitchReason.SessionLock
            or SessionSwitchReason.ConsoleDisconnect
            or SessionSwitchReason.RemoteDisconnect))
        {
            return;
        }

        Application.Current?.Dispatcher.BeginInvoke(() => _policy.Lock(LockReason.SessionLock));
    }
}
