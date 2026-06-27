using Common.Client.UI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Client.AuthLib.ViewModels;

namespace Client.Manager.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DashboardViewModel _dashboardViewModel = new();
    private readonly HomeViewModel _homeViewModel = new();
    private readonly ExternalAccessViewModel _externalAccessViewModel = new();
    private readonly AuditViewModel _auditViewModel = new();
    private readonly MessengerViewModel _messengerViewModel = new();
    private readonly GroupsViewModel _groupsViewModel = new();
    private readonly SettingsViewModel _settingsViewModel = new();
    private readonly AccountViewModel _accountViewModel = new();

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private bool _isSideBarEnabled = true;

    public MainWindowViewModel()
    {
        CurrentViewModel = _dashboardViewModel;
    }

    public bool IsDashboardSelected => CurrentViewModel == _dashboardViewModel;

    public bool IsHomeSelected => CurrentViewModel == _homeViewModel;

    public bool IsExternalAccessSelected => CurrentViewModel == _externalAccessViewModel;

    public bool IsAuditSelected => CurrentViewModel == _auditViewModel;

    public bool IsMessengerSelected => CurrentViewModel == _messengerViewModel;

    public bool IsGroupsSelected => CurrentViewModel == _groupsViewModel;

    public bool IsSettingsSelected => CurrentViewModel == _settingsViewModel;

    public bool IsAccountSelected => CurrentViewModel == _accountViewModel;

    public bool HasAuditNotifications => _auditViewModel.HasNotifications;

    public string AuditNotificationBadgeText => _auditViewModel.NotificationBadgeText;

    public bool HasMessengerNotifications => _messengerViewModel.HasNotifications;

    public string MessengerNotificationBadgeText => _messengerViewModel.NotificationBadgeText;

    partial void OnCurrentViewModelChanged(ViewModelBase? value)
    {
        // Блокировка боковой панели при открытии AuthViewModel
        // IsSideBarEnabled = value is AuthViewModel;
        OnPropertyChanged(nameof(IsDashboardSelected));
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(IsExternalAccessSelected));
        OnPropertyChanged(nameof(IsAuditSelected));
        OnPropertyChanged(nameof(IsMessengerSelected));
        OnPropertyChanged(nameof(IsGroupsSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(IsAccountSelected));
    }

    [RelayCommand]
    private void ShowDashboardView() => CurrentViewModel = _dashboardViewModel;

    [RelayCommand]
    private void ShowHomeView() => CurrentViewModel = _homeViewModel;

    [RelayCommand]
    private void ShowExternalAccessView() => CurrentViewModel = _externalAccessViewModel;

    [RelayCommand]
    private void ShowAuditView() => CurrentViewModel = _auditViewModel;

    [RelayCommand]
    private void ShowMessengerView() => CurrentViewModel = _messengerViewModel;

    [RelayCommand]
    private void ShowGroupsView() => CurrentViewModel = _groupsViewModel;

    [RelayCommand]
    private void ShowSettingsView() => CurrentViewModel = _settingsViewModel;

    [RelayCommand]
    private void ShowAccountView() => CurrentViewModel = _accountViewModel;

    [RelayCommand]
    private void ShowAuthView()
    {
        CurrentViewModel = new AuthViewModel();
    }
}
