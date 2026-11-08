using System.ComponentModel;
using System.Windows.Input;
using KeyHolder.Desktop.Mvvm;
using KeyHolder.Desktop.Services;
using KeyHolder.Domain.Locking;

namespace KeyHolder.Desktop.ViewModels;

/// <summary>
/// The window shell: locked or unlocked state, the fake-data banner, and the lock commands.
/// In Milestone 1 Unlock needs no password because the vault holds fake data only.
/// Real password unlock arrives with the encrypted vault in Milestone 2.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    public const string FakeDataBanner =
        "FAKE DATA ONLY - Milestone 1 stores demo data unencrypted. Do not enter real passwords.";

    private readonly VaultLockPolicy _lockPolicy;
    private readonly IClipboardService _clipboard;
    private string _lockStatus = "Vault locked";

    public MainWindowViewModel(VaultLockPolicy lockPolicy, VaultViewModel vault, IClipboardService clipboard)
    {
        ArgumentNullException.ThrowIfNull(lockPolicy);
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(clipboard);

        _lockPolicy = lockPolicy;
        _clipboard = clipboard;
        Vault = vault;

        UnlockCommand = new RelayCommand(() => _lockPolicy.Unlock(), () => IsLocked);
        LockCommand = new RelayCommand(() => _lockPolicy.Lock(LockReason.User), () => !IsLocked);

        _lockPolicy.Unlocked += OnUnlocked;
        _lockPolicy.Locked += OnLocked;
        Vault.PropertyChanged += OnVaultPropertyChanged;
    }

    public VaultViewModel Vault { get; }

    public ICommand UnlockCommand { get; }

    public ICommand LockCommand { get; }

    public string Banner { get; } = FakeDataBanner;

    public bool IsLocked => _lockPolicy.IsLocked;

    public bool IsUnlocked => !_lockPolicy.IsLocked;

    /// <summary>
    /// Text for the status bar.
    /// </summary>
    public string StatusText => IsLocked ? _lockStatus : Vault.StatusMessage;

    private async void OnUnlocked(object? sender, EventArgs e)
    {
        NotifyLockStateChanged();

        try
        {
            await Vault.LoadAsync();
            Vault.SetStatus("Vault unlocked.");
        }
        catch (Exception exception)
        {
            Vault.SetStatus($"Could not load the vault: {exception.Message}");
        }
    }

    private void OnLocked(object? sender, LockedEventArgs e)
    {
        // Order matters: forget everything the screen showed before the locked screen appears.
        Vault.Clear();
        _clipboard.ClearIfOwned();

        _lockStatus = e.Reason switch
        {
            LockReason.Idle => "Vault locked after being idle.",
            LockReason.SessionLock => "Vault locked because Windows was locked.",
            _ => "Vault locked.",
        };

        NotifyLockStateChanged();
    }

    private void OnVaultPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VaultViewModel.StatusMessage))
        {
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void NotifyLockStateChanged()
    {
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(StatusText));
        CommandManager.InvalidateRequerySuggested();
    }
}
