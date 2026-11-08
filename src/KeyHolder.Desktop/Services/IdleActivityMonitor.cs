using System.Windows.Input;
using System.Windows.Threading;
using KeyHolder.Domain.Locking;

namespace KeyHolder.Desktop.Services;

/// <summary>
/// Feeds user input into <see cref="VaultLockPolicy"/> and checks the idle timeout once per second.
/// </summary>
internal sealed class IdleActivityMonitor : IDisposable
{
    private readonly VaultLockPolicy _policy;
    private readonly DispatcherTimer _timer;
    private bool _started;

    public IdleActivityMonitor(VaultLockPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        _policy = policy;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _policy.CheckIdle();
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        InputManager.Current.PreProcessInput += OnPreProcessInput;
        _timer.Start();
    }

    public void Dispose()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _timer.Stop();
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is MouseEventArgs or KeyboardEventArgs)
        {
            _policy.RecordActivity();
        }
    }
}
