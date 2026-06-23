using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Client.UI.Models;

namespace Client.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private bool _showLogin = true;
    [ObservableProperty] private bool _showRegister = false;
    [ObservableProperty] private bool _showSetup = false;
    [ObservableProperty] private bool _isAuthenticated = false;

    [ObservableProperty] private string _currentUserName = "Гость";
    [ObservableProperty] private string _currentCompanyName = "";
    [ObservableProperty] private string _storageUsed = "0 ГБ / 10 ГБ";
    [ObservableProperty] private double _storagePercent = 0;
    [ObservableProperty] private bool _hasUnreadMessages = true;
    [ObservableProperty] private int _unreadCount = 3;

    [ObservableProperty] private bool _showFiles = true;
    [ObservableProperty] private bool _showMessenger = false;
    [ObservableProperty] private bool _showExternalAccess = false;
    [ObservableProperty] private bool _showPolicies = false;
    [ObservableProperty] private bool _showAudit = false;
    [ObservableProperty] private bool _showSettings = false;

    [ObservableProperty] private string _activeSection = "Files";

    public LoginViewModel LoginVM { get; }
    public RegisterViewModel RegisterVM { get; }
    public SetupCompanyViewModel SetupVM { get; }
    public FilesViewModel FilesVM { get; }
    public MessengerViewModel MessengerVM { get; }
    public ExternalAccessViewModel ExternalAccessVM { get; }
    public PoliciesViewModel PoliciesVM { get; }
    public AuditViewModel AuditVM { get; }
    public SettingsViewModel SettingsVM { get; }

    public ObservableCollection<NotificationItem> Notifications { get; } = new();

    public MainWindowViewModel()
    {
        LoginVM = new LoginViewModel();
        RegisterVM = new RegisterViewModel();
        SetupVM = new SetupCompanyViewModel();
        FilesVM = new FilesViewModel();
        MessengerVM = new MessengerViewModel();
        ExternalAccessVM = new ExternalAccessViewModel();
        PoliciesVM = new PoliciesViewModel();
        AuditVM = new AuditViewModel();
        SettingsVM = new SettingsViewModel();

        LoginVM.LoginSucceeded += OnLoginSucceeded;
        LoginVM.GoToRegister += () => { ShowLogin = false; ShowRegister = true; };
        LoginVM.GoToSetup += () => { ShowLogin = false; ShowSetup = true; };
        RegisterVM.RegisterSucceeded += () => { ShowRegister = false; ShowLogin = true; };
        RegisterVM.GoBack += () => { ShowRegister = false; ShowLogin = true; };
        SetupVM.SetupSucceeded += () => { ShowSetup = false; ShowLogin = true; };
        SetupVM.GoBack += () => { ShowSetup = false; ShowLogin = true; };
    }

    private void OnLoginSucceeded(string userName, string companyName)
    {
        CurrentUserName = userName;
        CurrentCompanyName = companyName;
        StorageUsed = "2.4 ГБ / 10 ГБ";
        StoragePercent = 24;
        ShowLogin = false;
        IsAuthenticated = true;
        NavigateTo("Files");

        Task.Delay(1500).ContinueWith(_ =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                AddNotification("🔔", "Запрос доступа", "Иванов И. запросил доступ к «Договор_Q1.docx»", "warning")));
        Task.Delay(3500).ContinueWith(_ =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                AddNotification("⏰", "Ссылка истекает", "Ссылка на Презентация.pptx истекает через 30 мин", "info")));
    }

    [RelayCommand]
    private void Navigate(string page) => NavigateTo(page);

    private void NavigateTo(string page)
    {
        ActiveSection = page;
        ShowFiles = page == "Files";
        ShowMessenger = page == "Messenger";
        ShowExternalAccess = page == "ExternalAccess";
        ShowPolicies = page == "Policies";
        ShowAudit = page == "Audit";
        ShowSettings = page == "Settings";
    }

    public void AddNotification(string icon, string title, string message, string type = "info")
    {
        var n = new NotificationItem { Icon = icon, Title = title, Message = message, Type = type };
        Notifications.Add(n);
        Task.Delay(6000).ContinueWith(_ =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (Notifications.Contains(n)) Notifications.Remove(n);
            }));
    }

    [RelayCommand]
    private void DismissNotification(NotificationItem n) => Notifications.Remove(n);

    [RelayCommand]
    private void Logout()
    {
        IsAuthenticated = false;
        ShowLogin = true;
        CurrentUserName = "Гость";
        CurrentCompanyName = "";
        Notifications.Clear();
    }

    [RelayCommand]
    private void Minimize()
    {
        var win = GetMainWindow();
        if (win != null) win.WindowState = Avalonia.Controls.WindowState.Minimized;
    }

    [RelayCommand]
    private void Maximize()
    {
        var win = GetMainWindow();
        if (win is null) return;
        win.WindowState = win.WindowState == Avalonia.Controls.WindowState.Maximized
            ? Avalonia.Controls.WindowState.Normal
            : Avalonia.Controls.WindowState.Maximized;
    }

    [RelayCommand]
    private void Close() => GetMainWindow()?.Close();

    private static Avalonia.Controls.Window? GetMainWindow() =>
        (Avalonia.Application.Current?.ApplicationLifetime as
         Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
