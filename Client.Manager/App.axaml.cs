using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Styling;
using Client.Auth.ViewModels;
using Client.Auth.Views;
using Client.Manager.Services;
using Client.Manager.ViewModels;
using Client.Manager.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Client.Manager;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private AuthViewModel? _authViewModel;
    private AuthWindow? _authWindow;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
        AvaloniaXamlLoader.Load(this);

        // Load icon resource dictionary
        var iconsXaml = AvaloniaXamlLoader.Load(
            new Uri("avares://Client.Manager/Assets/Icons.axaml"));
        if (iconsXaml is ResourceDictionary icons)
            Resources.MergedDictionaries.Add(icons);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Показываем окно авторизации первым
            _authViewModel = Services.GetRequiredService<AuthViewModel>();
            _authViewModel.AuthSucceeded += OnAuthSucceeded;

            _authWindow = new AuthWindow { DataContext = _authViewModel };
            desktop.MainWindow = _authWindow;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OnAuthSucceeded(string userName, string companyName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_authWindow != null)
            {
                _authWindow.Close();
                _authWindow = null;
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var viewModel = Services.GetRequiredService<MainWindowViewModel>();
                viewModel.CurrentUserName = userName;
                viewModel.CurrentCompanyName = companyName;
                viewModel.StorageUsed = "0 ГБ / 10 ГБ";
                viewModel.StoragePercent = 0;

                _mainWindow = new MainWindow { DataContext = viewModel };
                desktop.MainWindow = _mainWindow;
                _mainWindow.Show();
                _mainWindow.Activate();

                viewModel.AddNotification("", "Вход выполнен", $"Добро пожаловать, {userName}!", "success");
            }
        });
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Auth
        services.AddSingleton<AuthViewModel>();

        // Manager services
        services.AddSingleton<GrpcClientService>();
        services.AddTransient<FilesViewModel>();
        services.AddTransient<MessengerViewModel>();
        services.AddTransient<ExternalAccessViewModel>();
        services.AddTransient<AuditViewModel>();
        services.AddTransient<PoliciesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}