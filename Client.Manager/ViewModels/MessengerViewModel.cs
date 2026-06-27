using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Common.Client.UI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Client.Manager.ViewModels;

/// <summary>
/// Плейсхолдер ViewModel вкладки сообщений.
/// </summary>
public partial class MessengerViewModel : ViewModelBase
{
    private readonly List<ChatPlaceholder> _allChats;

    [ObservableProperty]
    private int _notificationCount = 4;

    [ObservableProperty]
    private string _selectedFolder = "Пользователи";

    [ObservableProperty]
    private ChatPlaceholder? _selectedChat;

    public ObservableCollection<ChatFolderPlaceholder> Folders { get; } = [];

    public ObservableCollection<ChatPlaceholder> Chats { get; } = [];

    public MessengerViewModel()
    {
        Folders.Add(new ChatFolderPlaceholder("Пользователи", true));
        Folders.Add(new ChatFolderPlaceholder("Группы", false));

        _allChats =
        [
            new("Пользователи", "Иван Петров", "Запрос доступа к файлу", "12:30", 1, "онлайн"),
            new("Пользователи", "Анна Смирнова", "Проверка внешней ссылки", "11:05", 0, "была 10 минут назад"),
            new("Группы", "Группа Финансы", "Новый документ на проверке", "10:48", 2, "6 участников"),
            new("Группы", "Группа Безопасность", "Обнаружено событие аудита", "09:12", 1, "4 участника"),
        ];

        RefreshFolders();
        RefreshChats();
        SelectChat(Chats.FirstOrDefault());
    }

    public bool HasNotifications => NotificationCount > 0;

    public string NotificationBadgeText => NotificationCount > 99 ? "99+" : NotificationCount.ToString();

    public bool IsUsersFolderSelected => SelectedFolder == "Пользователи";

    public bool IsGroupsFolderSelected => SelectedFolder == "Группы";

    public string SelectedChatName => SelectedChat?.Name ?? "Выберите чат";

    public string SelectedChatSubtitle => SelectedChat is null ? "Нет активного диалога" : $"{SelectedFolder} • {SelectedChat.Status}";

    public string SelectedChatPreview => SelectedChat?.Preview ?? "Откройте диалог из списка слева.";

    partial void OnNotificationCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(NotificationBadgeText));
    }

    partial void OnSelectedFolderChanged(string value)
    {
        OnPropertyChanged(nameof(IsUsersFolderSelected));
        OnPropertyChanged(nameof(IsGroupsFolderSelected));
        OnPropertyChanged(nameof(SelectedChatSubtitle));
    }

    partial void OnSelectedChatChanged(ChatPlaceholder? value)
    {
        OnPropertyChanged(nameof(SelectedChatName));
        OnPropertyChanged(nameof(SelectedChatSubtitle));
        OnPropertyChanged(nameof(SelectedChatPreview));
    }

    [RelayCommand]
    private void SelectUsersFolder() => SelectFolderByName("Пользователи");

    [RelayCommand]
    private void SelectGroupsFolder() => SelectFolderByName("Группы");

    [RelayCommand]
    private void SelectFolder(ChatFolderPlaceholder folder) => SelectFolderByName(folder.Name);

    [RelayCommand]
    private void SelectChat(ChatPlaceholder? chat)
    {
        if (chat is null)
        {
            return;
        }

        foreach (ChatPlaceholder item in _allChats)
        {
            item.IsSelected = item == chat;
        }

        chat.UnreadCount = 0;
        SelectedChat = chat;
        RefreshFolders();
    }

    private void SelectFolderByName(string folderName)
    {
        SelectedFolder = folderName;

        foreach (ChatFolderPlaceholder folder in Folders)
        {
            folder.IsSelected = folder.Name == folderName;
        }

        RefreshFolders();
        RefreshChats();
        SelectChat(Chats.FirstOrDefault());
    }

    private void RefreshChats()
    {
        Chats.Clear();

        foreach (ChatPlaceholder chat in _allChats.Where(chat => chat.FolderName == SelectedFolder))
        {
            Chats.Add(chat);
        }
    }

    private void RefreshFolders()
    {
        foreach (ChatFolderPlaceholder folder in Folders)
        {
            folder.UnreadCount = _allChats.Where(chat => chat.FolderName == folder.Name).Sum(chat => chat.UnreadCount);
        }

        NotificationCount = Folders.Sum(folder => folder.UnreadCount);
    }
}

public partial class ChatFolderPlaceholder : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _unreadCount;

    public ChatFolderPlaceholder(string name, bool isSelected)
    {
        Name = name;
        IsSelected = isSelected;
    }

    public string Name { get; }

    public bool HasVisibleBadge => !IsSelected && UnreadCount > 0;

    public string BadgeText => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(HasVisibleBadge));

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasVisibleBadge));
        OnPropertyChanged(nameof(BadgeText));
    }
}

public partial class ChatPlaceholder : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _unreadCount;

    public ChatPlaceholder(string folderName, string name, string preview, string time, int unreadCount, string status)
    {
        FolderName = folderName;
        Name = name;
        Preview = preview;
        Time = time;
        UnreadCount = unreadCount;
        Status = status;
    }

    public string FolderName { get; }

    public string Name { get; }

    public string Preview { get; }

    public string Time { get; }

    public string Status { get; }

    public bool HasUnread => UnreadCount > 0;

    public string BadgeText => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnread));
        OnPropertyChanged(nameof(BadgeText));
    }
}