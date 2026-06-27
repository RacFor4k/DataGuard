using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Common.Client.UI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Client.Manager.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly List<FolderNodePlaceholder> _allFolderNodes;
    private readonly List<FilePlaceholder> _allFiles;
    private string _sortColumn = "Size";
    private bool _sortDescending = true;

    [ObservableProperty]
    private bool _isListView = true;

    public bool IsTilesView => !IsListView;

    public string NameSortArrow => GetSortArrow("Name");

    public string TypeSortArrow => GetSortArrow("Type");

    public string SizeSortArrow => GetSortArrow("Size");

    public string UpdatedAtSortArrow => GetSortArrow("UpdatedAt");

    public string StatusSortArrow => GetSortArrow("Status");

    public ObservableCollection<FolderNodePlaceholder> VisibleFolderNodes { get; } = [];

    public ObservableCollection<FilePlaceholder> Files { get; } = [];

    public HomeViewModel()
    {
        FolderNodePlaceholder root = new("Корпоративные файлы", 0, true);
        FolderNodePlaceholder finance = new("Финансы", 1, true);
        FolderNodePlaceholder contracts = new("Договоры", 1, true);
        FolderNodePlaceholder projects = new("Проекты", 1, false);
        FolderNodePlaceholder archive = new("Архив", 1, false);

        _allFolderNodes =
        [
            root,
            finance,
            new("Отчеты", 2, false, finance),
            new("Сметы", 2, false, finance),
            contracts,
            new("Поставщики", 2, false, contracts),
            new("Клиенты", 2, false, contracts),
            projects,
            new("DataGuard", 2, false, projects),
            new("Интеграции", 2, false, projects),
            archive,
            new("2025", 2, false, archive),
            new("2024", 2, false, archive),
        ];

        foreach (FolderNodePlaceholder node in _allFolderNodes)
        {
            node.HasChildren = _allFolderNodes.Any(child => child.Parent == node);
        }

        root.IsSelected = true;

        _allFiles =
        [
            new("Финансовый отчет Q2.pdf", "PDF", "2.4 МБ", "Сегодня", "Конфиденциально", 2_400, new DateTime(2026, 6, 26)),
            new("Договор поставки.docx", "DOCX", "860 КБ", "Вчера", "На проверке", 860, new DateTime(2026, 6, 25)),
            new("Схема доступа.png", "PNG", "1.1 МБ", "24.06.2026", "Доступен", 1_100, new DateTime(2026, 6, 24)),
            new("Материалы проекта", "Папка", "18 файлов", "21.06.2026", "Доступен", 18_000, new DateTime(2026, 6, 21)),
        ];

        RefreshVisibleFolders();
        ApplySort();
    }

    partial void OnIsListViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsTilesView));
    }

    [RelayCommand]
    private void ShowListView() => IsListView = true;

    [RelayCommand]
    private void ShowTilesView() => IsListView = false;

    [RelayCommand]
    private void SelectFolderNode(FolderNodePlaceholder node)
    {
        foreach (FolderNodePlaceholder item in _allFolderNodes)
        {
            item.IsSelected = item == node;
        }

        if (node.HasChildren)
        {
            node.IsExpanded = !node.IsExpanded;
            RefreshVisibleFolders();
        }
    }

    [RelayCommand]
    private void SelectFile(FilePlaceholder file)
    {
        foreach (FilePlaceholder item in _allFiles)
        {
            item.IsSelected = item == file;
        }
    }

    [RelayCommand]
    private void SortFiles(string column)
    {
        if (_sortColumn == column)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortColumn = column;
            _sortDescending = true;
        }

        ApplySort();
        NotifySortArrowsChanged();
    }

    private string GetSortArrow(string column) => _sortColumn == column ? _sortDescending ? "↓" : "↑" : string.Empty;

    private void NotifySortArrowsChanged()
    {
        OnPropertyChanged(nameof(NameSortArrow));
        OnPropertyChanged(nameof(TypeSortArrow));
        OnPropertyChanged(nameof(SizeSortArrow));
        OnPropertyChanged(nameof(UpdatedAtSortArrow));
        OnPropertyChanged(nameof(StatusSortArrow));
    }

    private void RefreshVisibleFolders()
    {
        VisibleFolderNodes.Clear();

        foreach (FolderNodePlaceholder node in _allFolderNodes.Where(IsVisibleNode))
        {
            VisibleFolderNodes.Add(node);
        }
    }

    private bool IsVisibleNode(FolderNodePlaceholder node)
    {
        FolderNodePlaceholder? parent = node.Parent;

        while (parent is not null)
        {
            if (!parent.IsExpanded)
            {
                return false;
            }

            parent = parent.Parent;
        }

        return true;
    }

    private void ApplySort()
    {
        IEnumerable<FilePlaceholder> query = _sortColumn switch
        {
            "Name" => _allFiles.OrderBy(file => file.Name),
            "Type" => _allFiles.OrderBy(file => file.Type),
            "Size" => _allFiles.OrderBy(file => file.SizeSortValue),
            "UpdatedAt" => _allFiles.OrderBy(file => file.UpdatedAtSortValue),
            "Status" => _allFiles.OrderBy(file => file.Status),
            _ => _allFiles.OrderBy(file => file.Name),
        };

        if (_sortDescending)
        {
            query = query.Reverse();
        }

        Files.Clear();

        foreach (FilePlaceholder file in query)
        {
            Files.Add(file);
        }
    }
}

public partial class FolderNodePlaceholder : ObservableObject
{
    [ObservableProperty]
    private bool _hasChildren;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    public FolderNodePlaceholder(string name, int depth, bool isExpanded, FolderNodePlaceholder? parent = null)
    {
        Name = name;
        Depth = depth;
        IsExpanded = isExpanded;
        Parent = parent;
    }

    public string Name { get; }

    public int Depth { get; }

    public FolderNodePlaceholder? Parent { get; }

    public string ExpanderText => HasChildren ? IsExpanded ? "▾" : "▸" : string.Empty;

    public int IndentWidth => Depth * 12;

    partial void OnHasChildrenChanged(bool value) => OnPropertyChanged(nameof(ExpanderText));

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpanderText));
}

public partial class FilePlaceholder : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public FilePlaceholder(string name, string type, string size, string updatedAt, string status, int sizeSortValue, DateTime updatedAtSortValue)
    {
        Name = name;
        Type = type;
        Size = size;
        UpdatedAt = updatedAt;
        Status = status;
        SizeSortValue = sizeSortValue;
        UpdatedAtSortValue = updatedAtSortValue;
    }

    public string Name { get; }

    public string Type { get; }

    public string Size { get; }

    public string UpdatedAt { get; }

    public string Status { get; }

    public int SizeSortValue { get; }

    public DateTime UpdatedAtSortValue { get; }
}