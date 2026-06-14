using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataGuard.UI.Models;

namespace DataGuard.UI.ViewModels;

// ────────────────────────────────────────────────────────────────
// MESSENGER
// ────────────────────────────────────────────────────────────────
public partial class MessengerViewModel : ObservableObject
{
    [ObservableProperty] private ChatThread? _selectedThread;
    [ObservableProperty] private string _messageInput = "";

    public ObservableCollection<ChatThread> Threads { get; } = new();

    public MessengerViewModel()
    {
        var t1 = new ChatThread
        {
            Name = "Договор Q1 2026.docx", LastMessage = "Ожидает согласования",
            Time = "14:32", UnreadCount = 2, Icon = "📝", IsDocument = true,
        };
        t1.Messages.Add(new ChatMessage { Author = "Система", IsSystem = true, Time = "14:30",
            Content = "Иванов А. запросил доступ к файлу «Договор Q1 2026.docx»" });
        t1.Messages.Add(new ChatMessage { Author = "Иванов А.", Time = "14:31",
            Content = "Добрый день! Мне необходим доступ для проверки и согласования." });

        var t2 = new ChatThread
        {
            Name = "Проект «Альфа»", LastMessage = "Обновил ТЗ — посмотри",
            Time = "13:15", Icon = "💬", IsPinned = true,
        };
        t2.Messages.Add(new ChatMessage { Author = "Сидоров К.", Time = "13:15",
            Content = "Обновил техническое задание, посмотри пожалуйста раздел 3.2" });
        t2.Messages.Add(new ChatMessage { Author = "Мне", IsMe = true, Time = "13:20",
            Content = "Окей, сейчас посмотрю 👍" });

        var t3 = new ChatThread
        {
            Name = "ООО «ТехПартнёр»", LastMessage = "Ссылка получена, спасибо",
            Time = "Вчера", Icon = "🏢",
        };
        t3.Messages.Add(new ChatMessage { Author = "Мне", IsMe = true, Time = "Вчера",
            Content = "Добрый день! Отправляю ссылку на папку с документами для согласования." });
        t3.Messages.Add(new ChatMessage { Author = "Петров В.", Time = "Вчера",
            Content = "Получили, спасибо! Рассмотрим до конца недели." });

        Threads.Add(t1); Threads.Add(t2); Threads.Add(t3);
        SelectedThread = t1;
    }

    [RelayCommand]
    private void SelectThread(ChatThread thread) => SelectedThread = thread;

    [RelayCommand]
    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageInput) || SelectedThread is null) return;
        SelectedThread.Messages.Add(new ChatMessage
        {
            Author = "Мне", Content = MessageInput, IsMe = true,
            Time = DateTime.Now.ToString("HH:mm")
        });
        MessageInput = "";
    }

    [RelayCommand]
    private void ApproveAccess()
    {
        if (SelectedThread is null) return;
        SelectedThread.Messages.Add(new ChatMessage
        {
            Author = "Система", IsSystem = true, Time = DateTime.Now.ToString("HH:mm"),
            Content = "✅ Доступ выдан: Только просмотр · Срок 7 дней"
        });
    }

    [RelayCommand]
    private void DenyAccess()
    {
        if (SelectedThread is null) return;
        SelectedThread.Messages.Add(new ChatMessage
        {
            Author = "Система", IsSystem = true, Time = DateTime.Now.ToString("HH:mm"),
            Content = "❌ В доступе отказано"
        });
    }
}

// ────────────────────────────────────────────────────────────────
// EXTERNAL ACCESS
// ────────────────────────────────────────────────────────────────
public partial class ExternalAccessViewModel : ObservableObject
{
    [ObservableProperty] private ExternalLink? _selectedLink;
    [ObservableProperty] private bool _showDetail = false;
    [ObservableProperty] private int _activeTab = 0;

    public int ActiveContractors => 12;
    public int OpenSessions => 4;
    public string TotalTransferred => "2.8 ГБ";
    public int Warnings => 1;

    public ObservableCollection<ExternalLink> Links { get; } = new();
    public ObservableCollection<AuditEntry> Journal { get; } = new();

    public ExternalAccessViewModel()
    {
        Links.Add(new ExternalLink { Name = "Договор Q1 2026.docx", Target = "ООО «ТехПартнёр»",
            Expires = "15.06.2026 18:00", Status = "Активна", Permissions = "Только просмотр",
            Downloads = 2, MaxDownloads = 5, CreatedBy = "Иванов А.", Created = "10.06.2026",
            HasWatermark = true, TimeWindow = "10:00–18:00" });
        Links.Add(new ExternalLink { Name = "Финансовый отчёт.xlsx", Target = "Внешний аудитор",
            Expires = "13.06.2026 23:59", Status = "Активна", Permissions = "Просмотр + скачивание",
            Downloads = 1, MaxDownloads = 1, CreatedBy = "Петрова М.", Created = "11.06.2026" });
        Links.Add(new ExternalLink { Name = "Презентация.pptx", Target = "Инвесторы",
            Expires = "Истекла", Status = "Истекла", Permissions = "Только просмотр",
            Downloads = 3, MaxDownloads = 3, CreatedBy = "Сидоров К.", Created = "05.06.2026" });
        Links.Add(new ExternalLink { Name = "NDA Партнёр.pdf", Target = "Юр. отдел",
            Expires = "20.06.2026", Status = "Активна", Permissions = "Только просмотр",
            Downloads = 0, MaxDownloads = 2, CreatedBy = "Волков С.", Created = "12.06.2026",
            HasWatermark = true, IpRestriction = "RU" });

        Journal.Add(new AuditEntry { Time = "14:32", User = "ООО ТехПартнёр", Action = "Просмотр", Target = "Договор Q1 2026.docx", Ip = "87.245.*.1", Result = "Успешно" });
        Journal.Add(new AuditEntry { Time = "13:10", User = "Аудитор", Action = "Скачивание", Target = "Финансовый отчёт.xlsx", Ip = "95.108.*.44", Result = "Успешно" });
        Journal.Add(new AuditEntry { Time = "11:02", User = "Неизвестный", Action = "Попытка доступа", Target = "Договор Q1 2026.docx", Ip = "193.*.*.89", Result = "Заблокировано" });
    }

    [RelayCommand] private void SelectLink(ExternalLink link) { SelectedLink = link; ShowDetail = true; }
    [RelayCommand] private void CloseDetail() => ShowDetail = false;
    [RelayCommand] private void RevokeLink()
    {
        if (SelectedLink == null) return;
        SelectedLink.Status = "Отозвана";
        OnPropertyChanged(nameof(SelectedLink));
    }
    [RelayCommand] private void SelectTab(int tab) => ActiveTab = tab;
}

// ────────────────────────────────────────────────────────────────
// AUDIT
// ────────────────────────────────────────────────────────────────
public partial class AuditViewModel : ObservableObject
{
    [ObservableProperty] private AuditEntry? _selectedEntry;
    [ObservableProperty] private bool _showDetail = false;
    [ObservableProperty] private string _filterUser = "";
    [ObservableProperty] private string _filterDocument = "";

    public ObservableCollection<AuditEntry> Entries { get; } = new();
    public ObservableCollection<AuditEntry> Filtered { get; } = new();

    public AuditViewModel()
    {
        var raw = new[]
        {
            new AuditEntry { Time = "14:45", User = "Иванов А.", Action = "Создание ссылки", Target = "Договор Q1 2026.docx", Ip = "10.0.0.5", Result = "Успешно", Device = "Windows 11", Browser = "Chrome 124" },
            new AuditEntry { Time = "14:32", User = "Внешний", Action = "Просмотр файла", Target = "Договор Q1 2026.docx", Ip = "87.245.12.1", Result = "Успешно", Device = "macOS", Browser = "Safari 17" },
            new AuditEntry { Time = "14:10", User = "Петрова М.", Action = "Скачивание", Target = "Финансовый отчёт.xlsx", Ip = "10.0.0.8", Result = "Успешно", Device = "Windows 11", Browser = "Edge 124" },
            new AuditEntry { Time = "13:55", User = "Сидоров К.", Action = "Изменение прав", Target = "Проект Альфа/", Ip = "10.0.0.12", Result = "Успешно", Device = "Ubuntu 22", Browser = "Firefox 126" },
            new AuditEntry { Time = "13:10", User = "Неизвестный", Action = "Попытка доступа", Target = "Договор Q1 2026.docx", Ip = "193.0.45.89", Result = "Заблокировано" },
            new AuditEntry { Time = "11:30", User = "Иванов А.", Action = "Загрузка файла", Target = "NDA_Партнёр.pdf", Ip = "10.0.0.5", Result = "Успешно", Device = "Windows 11", Browser = "Chrome 124" },
            new AuditEntry { Time = "10:00", User = "Система", Action = "Авто-бэкап", Target = "Все файлы", Ip = "10.0.0.1", Result = "Успешно" },
        };
        foreach (var e in raw) { Entries.Add(e); Filtered.Add(e); }
    }

    partial void OnFilterUserChanged(string value) => ApplyFilter();
    partial void OnFilterDocumentChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Filtered.Clear();
        foreach (var e in Entries)
        {
            if ((string.IsNullOrWhiteSpace(FilterUser) || e.User.Contains(FilterUser, StringComparison.OrdinalIgnoreCase))
             && (string.IsNullOrWhiteSpace(FilterDocument) || e.Target.Contains(FilterDocument, StringComparison.OrdinalIgnoreCase)))
                Filtered.Add(e);
        }
    }

    [RelayCommand] private void SelectEntry(AuditEntry e) { SelectedEntry = e; ShowDetail = true; }
    [RelayCommand] private void CloseDetail() => ShowDetail = false;
}

// ────────────────────────────────────────────────────────────────
// SETTINGS
// ────────────────────────────────────────────────────────────────
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private string _serverAddress = "https://localhost:7777";
    [ObservableProperty] private bool _autoLock = true;
    [ObservableProperty] private int _sessionHours = 8;
    [ObservableProperty] private bool _savedOk = false;

    [RelayCommand]
    private async Task Save()
    {
        await Task.Delay(300);
        SavedOk = true;
        await Task.Delay(2500);
        SavedOk = false;
    }
}

// ────────────────────────────────────────────────────────────────
// POLICIES
// ────────────────────────────────────────────────────────────────
public partial class PoliciesViewModel : ObservableObject
{
    public System.Collections.ObjectModel.ObservableCollection<DataGuard.UI.Models.PolicyGroup> Groups { get; } = new();

    public PoliciesViewModel()
    {
        Groups.Add(new DataGuard.UI.Models.PolicyGroup { Name = "Отдел разработки", Type = "Отдел", Members = 8, Template = "IT-стандарт", Inherited = true });
        Groups.Add(new DataGuard.UI.Models.PolicyGroup { Name = "Бухгалтерия", Type = "Отдел", Members = 5, Template = "Финансы", Inherited = true });
        Groups.Add(new DataGuard.UI.Models.PolicyGroup { Name = "Проект «Альфа»", Type = "Проект", Members = 12, Template = "Базовый", Inherited = false });
        Groups.Add(new DataGuard.UI.Models.PolicyGroup { Name = "Контрагенты", Type = "Внешние", Members = 3, Template = "Гостевой", Inherited = false });
    }
}
