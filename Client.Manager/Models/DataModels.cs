using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.Manager.Models;

public class NotificationItem
{
    public string Icon { get; set; } = "";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info"; // info | warning | success | error
}

public partial class FileItem : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _type = "file";
    [ObservableProperty] private string _icon = "";
    [ObservableProperty] private string _size = string.Empty;
    [ObservableProperty] private string _owner = string.Empty;
    [ObservableProperty] private string _modified = string.Empty;
    [ObservableProperty] private string _access = "Полный доступ";
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isConfidential;
    [ObservableProperty] private string _extension = string.Empty;
    [ObservableProperty] private bool _isExpanded;

    public List<FileItem> Children { get; set; } = new();
}

public class FolderNode
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "";
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public ObservableCollection<FolderNode> Children { get; set; } = new();
    public int Level { get; set; }
}

public class AccessRight
{
    public string Name { get; set; } = string.Empty;
    public string Avatar { get; set; } = "";
    public string Role { get; set; } = "Просмотр";
    public bool IsGroup { get; set; }
}

public partial class ChatMessage : ObservableObject
{
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private string _time = string.Empty;
    [ObservableProperty] private bool _isMe;
    [ObservableProperty] private bool _isSystem;
    [ObservableProperty] private bool _isRead = true;
}

public partial class ChatThread : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _lastMessage = string.Empty;
    [ObservableProperty] private string _time = string.Empty;
    [ObservableProperty] private int _unreadCount;
    [ObservableProperty] private string _icon = "";
    [ObservableProperty] private bool _isDocument;
    [ObservableProperty] private bool _isPinned;

    public ObservableCollection<ChatMessage> Messages { get; set; } = new();
}

public partial class ExternalLink : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _target = string.Empty;
    [ObservableProperty] private string _expires = string.Empty;
    [ObservableProperty] private string _status = "Активна";
    [ObservableProperty] private string _permissions = "Только просмотр";
    [ObservableProperty] private int _downloads;
    [ObservableProperty] private int _maxDownloads;
    [ObservableProperty] private string _createdBy = string.Empty;
    [ObservableProperty] private string _created = string.Empty;
    [ObservableProperty] private bool _hasWatermark;
    [ObservableProperty] private string _ipRestriction = string.Empty;
    [ObservableProperty] private string _timeWindow = string.Empty;
}

public partial class AuditEntry : ObservableObject
{
    [ObservableProperty] private string _time = string.Empty;
    [ObservableProperty] private string _user = string.Empty;
    [ObservableProperty] private string _action = string.Empty;
    [ObservableProperty] private string _target = string.Empty;
    [ObservableProperty] private string _ip = string.Empty;
    [ObservableProperty] private string _result = "Успешно";
    [ObservableProperty] private string _device = string.Empty;
    [ObservableProperty] private string _browser = string.Empty;
}

public partial class PolicyGroup : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _type = string.Empty;
    [ObservableProperty] private int _members;
    [ObservableProperty] private string _template = string.Empty;
    [ObservableProperty] private bool _inherited;

    public List<GroupMember> MemberList { get; set; } = new();
    [ObservableProperty] private string _description = string.Empty;
}

public class GroupMember
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Участник";
    public string Avatar { get; set; } = "";
    public bool IsAdmin { get; set; }
}

public class PolicyTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "";
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#1E3A5F";
    public string TextColor { get; set; } = "#93C5FD";
    public List<string> Rules { get; set; } = new();
}