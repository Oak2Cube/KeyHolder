using System.Windows;
using KeyHolder.Cryptography;
using KeyHolder.Desktop.Services;
using KeyHolder.Desktop.ViewModels;
using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Locking;
using KeyHolder.Infrastructure.FakeData;
using KeyHolder.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace KeyHolder.Desktop;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services, VaultPaths.Default());

            _serviceProvider = services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true,
                });

            // Prepare the database before any window can use it.
            _serviceProvider.GetRequiredService<SchemaMigrator>().Migrate();
            await _serviceProvider.GetRequiredService<FakeDataSeeder>().SeedIfNeededAsync();

            _serviceProvider.GetRequiredService<IdleActivityMonitor>().Start();
            _serviceProvider.GetRequiredService<SessionLockMonitor>().Start();

            _serviceProvider.GetRequiredService<MainWindow>().Show();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            MessageBox.Show(
                $"KeyHolder could not start:{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "KeyHolder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Disposing the provider also disposes the monitors and clears the app's clipboard value.
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    internal static void ConfigureServices(IServiceCollection services, VaultPaths paths)
    {
        // Domain and cryptography.
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(sp => new VaultLockPolicy(sp.GetRequiredService<IClock>()));
        services.AddSingleton<IPasswordGenerator, PasswordGenerator>();

        // Infrastructure: one SQLite repository serves all three storage interfaces.
        services.AddSingleton(paths);
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<SchemaMigrator>();
        services.AddSingleton<SqliteEntryRepository>();
        services.AddSingleton<IEntryRepository>(sp => sp.GetRequiredService<SqliteEntryRepository>());
        services.AddSingleton<IVaultStateStore>(sp => sp.GetRequiredService<SqliteEntryRepository>());
        services.AddSingleton<IFakeDataStore>(sp => sp.GetRequiredService<SqliteEntryRepository>());
        services.AddSingleton<FakeDataSeeder>();

        // Desktop services.
        services.AddSingleton<IClipboardService, WindowsClipboardService>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IdleActivityMonitor>();
        services.AddSingleton<SessionLockMonitor>();

        // View models and window.
        services.AddSingleton<SecretViewModel>();
        services.AddSingleton<VaultViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
