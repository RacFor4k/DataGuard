using System.Collections.ObjectModel;

namespace Client.UI.Models;

public class NotificationItem
{
    public string Icon { get; set; } = "🔔";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info"; // info | warning | success | error
}

public class FileItem
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "file";
    public string Icon { get; set; } = "📄";
    public string Size { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Modified { get; set; } = string.Empty;
    public string Access { get; set; } = "Полный доступ";
    public bool IsSelected { get; set; }
    public bool IsConfidential { get; set; }
    public string Extension { get; set; } = string.Empty;
    public List<FileItem> Children { get; set; } = new();
    public bool IsExpanded { get; set; }
}

public class FolderNode
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁";
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public ObservableCollection<FolderNode> Children { get; set; } = new();
    public int Level { get; set; }
}

public class AccessRight
{
    public string Name { get; set; } = string.Empty;
    public string Avatar { get; set; } = "👤";
    public string Role { get; set; } = "Просмотр";
    public bool IsGroup { get; set; }
}

public class ChatMessage
{
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public bool IsMe { get; set; }
    public bool IsSystem { get; set; }
    public bool IsRead { get; set; } = true;
}

public class ChatThread
{
    public string Name { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
    public string Icon { get; set; } = "💬";
    public bool IsDocument { get; set; }
    public bool IsPinned { get; set; }
    public ObservableCollection<ChatMessage> Messages { get; set; } = new();
}

public class ExternalLink
{
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Expires { get; set; } = string.Empty;
    public string Status { get; set; } = "Активна";
    public string Permissions { get; set; } = "Только просмотр";
    public int Downloads { get; set; }
    public int MaxDownloads { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
    public bool HasWatermark { get; set; }
    public string IpRestriction { get; set; } = string.Empty;
    public string TimeWindow { get; set; } = string.Empty;
}

public class AuditEntry
{
    public string Time { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string Result { get; set; } = "Успешно";
    public string Device { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
}

public class PolicyGroup
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Members { get; set; }
    public string Template { get; set; } = string.Empty;
    public bool Inherited { get; set; }
    public List<GroupMember> MemberList { get; set; } = new();
    public string Description { get; set; } = string.Empty;
}

public class GroupMember
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Участник";
    public string Avatar { get; set; } = "👤";
    public bool IsAdmin { get; set; }
}

public class PolicyTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "🛡";
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#1E3A5F";
    public string TextColor { get; set; } = "#93C5FD";
    public List<string> Rules { get; set; } = new();
}
