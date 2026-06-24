using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Client.Manager.Models;

namespace Client.Manager.ViewModels;

public partial class FilesViewModel : ObservableObject
{
    // ── Search / filters ─────────────────────────────────────────
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _sortColumn = "Name";
    [ObservableProperty] private bool _sortAscending = true;

    // ── Selection ─────────────────────────────────────────────────
    [ObservableProperty] private FileItem? _selectedFile;
    [ObservableProperty] private int _selectedCount = 0;
    [ObservableProperty] private string _currentPath = "/ Мои файлы";

    // ── Panels ────────────────────────────────────────────────────
    [ObservableProperty] private bool _showRightPanel = false;
    [ObservableProperty] private bool _showSharePanel = false;
    [ObservableProperty] private bool _showPropertiesPanel = true;
    [ObservableProperty] private bool _showPreview = false;
    [ObservableProperty] private bool _showContextChat = false;
    [ObservableProperty] private bool _showRenameInput = false;
    [ObservableProperty] private string _renameValue = "";

    // ── Preview ───────────────────────────────────────────────────
    [ObservableProperty] private string _previewTitle = "";
    [ObservableProperty] private string _previewType = ""; // image | pdf | text | office | unsupported
    [ObservableProperty] private string _previewContent = "";

    // ── Share form ────────────────────────────────────────────────
    [ObservableProperty] private int _linkTypeIndex = 1;      // 0=internal 1=guest
    [ObservableProperty] private int _expiryIndex = 1;
    [ObservableProperty] private bool _selfDestruct = false;
    [ObservableProperty] private bool _allowDownload = true;
    [ObservableProperty] private bool _watermark = false;
    [ObservableProperty] private string _watermarkText = "";
    [ObservableProperty] private string _timeFrom = "09:00";
    [ObservableProperty] private string _timeTo = "18:00";
    [ObservableProperty] private bool _timeRestrict = false;
    [ObservableProperty] private string _attemptLimit = "";
    [ObservableProperty] private string _ipRestrict = "";
    [ObservableProperty] private string _generatedLink = "";
    [ObservableProperty] private bool _linkGenerated = false;

    // ── Context chat ──────────────────────────────────────────────
    [ObservableProperty] private string _chatInput = "";
    [ObservableProperty] private ObservableCollection<ChatMessage> _chatMessages = new();

    // ── Properties panel extras ───────────────────────────────────
    [ObservableProperty] private ObservableCollection<AccessRight> _accessRights = new();
    [ObservableProperty] private string _addUserSearch = "";

    // ── Data ──────────────────────────────────────────────────────
    public ObservableCollection<FolderNode> FolderTree { get; } = new();
    public ObservableCollection<FileItem> Files { get; } = new();
    public ObservableCollection<FileItem> FilteredFiles { get; } = new();

    public FilesViewModel() { LoadDemoData(); }

    private void LoadDemoData()
    {
        var root = new FolderNode { Name = "Мои файлы", Icon = "", IsExpanded = true, IsSelected = true, Level = 0 };
        root.Children.Add(new FolderNode { Name = "Общие документы", Icon = "", Level = 1 });
        root.Children.Add(new FolderNode
        {
            Name = "Проекты", Icon = "", IsExpanded = true, Level = 1,
            Children = new()
            {
                new FolderNode { Name = "Проект Альфа", Icon = "", Level = 2 },
                new FolderNode { Name = "Архив 2025", Icon = "", Level = 2 },
            }
        });
        root.Children.Add(new FolderNode { Name = "Контрагенты", Icon = "", Level = 1 });
        FolderTree.Add(root);

        var items = new[]
        {
            new FileItem { Name = "Договор Q1 2026.docx", Type = "file", Icon = "", Extension = "docx",
                Size = "245 КБ", Owner = "Иванов А.", Modified = "12.06.2026", Access = "Полный доступ" },
            new FileItem { Name = "Финансовый отчёт.xlsx", Type = "file", Icon = "", Extension = "xlsx",
                Size = "1.2 МБ", Owner = "Петрова М.", Modified = "11.06.2026", Access = "Только просмотр" },
            new FileItem { Name = "Презентация партнёрам.pptx", Type = "file", Icon = "", Extension = "pptx",
                Size = "8.7 МБ", Owner = "Сидоров К.", Modified = "10.06.2026", Access = "Полный доступ" },
            new FileItem { Name = "NDA_ООО_Партнёр.pdf", Type = "file", Icon = "", Extension = "pdf",
                Size = "340 КБ", Owner = "Иванов А.", Modified = "09.06.2026", Access = "Ограниченный", IsConfidential = true },
            new FileItem { Name = "Техническое задание v3.docx", Type = "file", Icon = "", Extension = "docx",
                Size = "580 КБ", Owner = "Разработка", Modified = "08.06.2026", Access = "Полный доступ" },
            new FileItem { Name = "Скриншот интерфейса.png", Type = "file", Icon = "", Extension = "png",
                Size = "1.8 МБ", Owner = "Дизайнер", Modified = "07.06.2026", Access = "Полный доступ" },
            new FileItem { Name = "Конфиг сервера.json", Type = "file", Icon = "", Extension = "json",
                Size = "12 КБ", Owner = "DevOps", Modified = "06.06.2026", Access = "Ограниченный", IsConfidential = true },
            new FileItem { Name = "Архив проектов 2025", Type = "folder", Icon = "", Extension = "",
                Modified = "01.01.2026", Owner = "Система", Access = "Только просмотр" },
        };
        foreach (var it in items) { Files.Add(it); FilteredFiles.Add(it); }

        AccessRights.Add(new AccessRight { Name = "Иванов Александр", Avatar = "", Role = "Полный доступ" });
        AccessRights.Add(new AccessRight { Name = "Петрова Мария", Avatar = "", Role = "Только просмотр" });
        AccessRights.Add(new AccessRight { Name = "Отдел разработки", Avatar = "", Role = "Редактирование", IsGroup = true });
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredFiles.Clear();
        var q = SearchText;
        var src = string.IsNullOrWhiteSpace(q)
            ? Files
            : Files.Where(f => f.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        foreach (var f in src) FilteredFiles.Add(f);
    }

    [RelayCommand]
    private void SelectFile(FileItem? file)
    {
        foreach (var f in FilteredFiles) f.IsSelected = false;
        SelectedFile = file;
        if (file != null) { file.IsSelected = true; SelectedCount = 1; }
        else { SelectedCount = 0; }
        ShowRightPanel = file != null;
        ShowSharePanel = false;
        ShowPropertiesPanel = true;
        ShowPreview = false;
        LinkGenerated = false;
        GeneratedLink = "";
    }

    [RelayCommand]
    private void OpenPreview(FileItem? file)
    {
        if (file == null || file.Type == "folder") return;
        PreviewTitle = file.Name;
        PreviewType = file.Extension switch
        {
            "png" or "jpg" or "jpeg" or "gif" or "webp" => "image",
            "pdf" => "pdf",
            "txt" or "json" or "xml" or "cs" or "js" or "md" => "text",
            "docx" or "xlsx" or "pptx" or "doc" or "xls" => "office",
            _ => "unsupported"
        };
        PreviewContent = PreviewType == "text"
            ? "// Содержимое файла будет загружено с сервера\n{\n  \"version\": \"1.0\",\n  \"config\": {\n    \"server\": \"localhost:7777\",\n    \"secure\": true\n  }\n}"
            : "";
        ShowPreview = true;
    }

    [RelayCommand]
    private void ClosePreview() => ShowPreview = false;

    [RelayCommand]
    private void OpenShare()
    {
        ShowSharePanel = true;
        ShowPropertiesPanel = false;
        LinkGenerated = false;
        GeneratedLink = "";
    }

    [RelayCommand]
    private void BackToProperties()
    {
        ShowSharePanel = false;
        ShowPropertiesPanel = true;
    }

    [RelayCommand]
    private void GenerateLink()
    {
        if (SelectedFile == null) return;
        if (SelectedFile.IsConfidential && LinkTypeIndex == 1)
        {
            // block — confidential can't be shared externally
            return;
        }
        GeneratedLink = $"https://dg.app/s/{Guid.NewGuid():N}";
        LinkGenerated = true;
    }

    [RelayCommand]
    private void CopyLink() { /* clipboard */ }

    [RelayCommand]
    private void ClosePanel()
    {
        ShowRightPanel = false;
        ShowPreview = false;
        SelectedFile = null;
        SelectedCount = 0;
    }

    [RelayCommand]
    private void OpenContextChat()
    {
        ShowContextChat = true;
        if (!ChatMessages.Any())
        {
            var fileName = SelectedFile?.Name ?? "файл";
            ChatMessages.Add(new ChatMessage
            {
                Author = "Система", IsSystem = true, Time = DateTime.Now.ToString("HH:mm"),
                Content = $"Запрос доступа к «{fileName}»"
            });
            ChatInput = $"Добрый день! Прошу предоставить доступ к файлу «{fileName}».";
        }
    }

    [RelayCommand]
    private void CloseContextChat() => ShowContextChat = false;

    [RelayCommand]
    private void SendChatMessage()
    {
        if (string.IsNullOrWhiteSpace(ChatInput)) return;
        ChatMessages.Add(new ChatMessage
        {
            Author = "Мне", IsMe = true,
            Time = DateTime.Now.ToString("HH:mm"),
            Content = ChatInput
        });
        ChatInput = "";
    }

    [RelayCommand]
    private void StartRename()
    {
        if (SelectedFile == null) return;
        RenameValue = SelectedFile.Name;
        ShowRenameInput = true;
    }

    [RelayCommand]
    private void ConfirmRename()
    {
        if (SelectedFile != null && !string.IsNullOrWhiteSpace(RenameValue))
            SelectedFile.Name = RenameValue;
        ShowRenameInput = false;
    }

    [RelayCommand]
    private void CancelRename() => ShowRenameInput = false;

    [RelayCommand]
    private void SortBy(string col)
    {
        if (SortColumn == col) SortAscending = !SortAscending;
        else { SortColumn = col; SortAscending = true; }
        var sorted = SortAscending
            ? FilteredFiles.OrderBy(f => f.Type == "folder" ? 0 : 1).ThenBy(GetSortKey(col)).ToList()
            : FilteredFiles.OrderBy(f => f.Type == "folder" ? 0 : 1).ThenByDescending(GetSortKey(col)).ToList();
        FilteredFiles.Clear();
        foreach (var f in sorted) FilteredFiles.Add(f);
    }

    private static Func<FileItem, string> GetSortKey(string col) => col switch
    {
        "Owner" => f => f.Owner,
        "Modified" => f => f.Modified,
        "Size" => f => f.Size,
        _ => f => f.Name
    };
}