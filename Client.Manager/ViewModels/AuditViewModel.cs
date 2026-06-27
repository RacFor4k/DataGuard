using Common.Client.UI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.Manager.ViewModels;

/// <summary>
/// Плейсхолдер ViewModel вкладки аудита.
/// </summary>
public partial class AuditViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _notificationCount = 12;

    public bool HasNotifications => NotificationCount > 0;

    public string NotificationBadgeText => NotificationCount > 99 ? "99+" : NotificationCount.ToString();

    partial void OnNotificationCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(NotificationBadgeText));
    }
}