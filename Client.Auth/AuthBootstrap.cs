using Avalonia.Markup.Xaml;
using Client.Auth.ViewModels;
using Client.Auth.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Client.Auth;

/// <summary>
/// Client.Auth — легковесная библиотека авторизации.
/// Используется Client.Manager как окно входа.
/// Для автономного тестирования можно запустить Client.Auth.Host.
/// </summary>
public static class AuthBootstrap
{
    /// <summary>
    /// Создаёт и настраивает ServiceProvider с AuthViewModel.
    /// </summary>
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AuthViewModel>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Создаёт окно авторизации с внедрённым ViewModel.
    /// </summary>
    public static AuthWindow CreateAuthWindow(AuthViewModel viewModel)
    {
        return new AuthWindow { DataContext = viewModel };
    }

    /// <summary>
    /// Создаёт окно авторизации с новым ServiceProvider.
    /// </summary>
    public static (AuthWindow Window, AuthViewModel ViewModel, IServiceProvider Services) Create()
    {
        var services = ConfigureServices();
        var vm = services.GetRequiredService<AuthViewModel>();
        var window = CreateAuthWindow(vm);
        return (window, vm, services);
    }
}