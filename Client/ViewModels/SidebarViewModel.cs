using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Client.ViewModels
{
    /// <summary>
    /// ViewModel бокового меню навигации.
    /// Управляет элементами меню, режимом отображения (развернут/свернут) и выбранным элементом.
    /// </summary>
    public partial class SidebarViewModel : ViewModelBase
    {
        // === КОНСТАНТЫ ===
        private const int TopItemsCount = 5;
        private const int BottomItemsCount = 3;

        // === ПОЛЯ ===
        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private NavigationItem? _selectedItem;

        // === КОЛЛЕКЦИИ ===
        
        /// <summary>
        /// Все элементы навигации (внутреннее хранилище).
        /// </summary>
        public ObservableCollection<NavigationItem> AllItems { get; } = new();

        /// <summary>
        /// Элементы верхнего меню (главная навигация).
        /// Инициализируется один раз в конструкторе.
        /// </summary>
        public ObservableCollection<NavigationItem> TopItems { get; } = new();

        /// <summary>
        /// Элементы нижнего меню (настройки, аккаунт, выход).
        /// Инициализируется один раз в конструкторе.
        /// </summary>
        public ObservableCollection<NavigationItem> BottomItems { get; } = new();

        // === СОБЫТИЯ ===
        public event EventHandler<bool>? ExpandChanged;

        // === КОНСТРУКТОР ===
        public SidebarViewModel()
        {
            InitializeNavigationItems();
            SelectedItem = TopItems.FirstOrDefault();
        }

        // === ИНИЦИАЛИЗАЦИЯ ===

        /// <summary>
        /// Создает все элементы навигации и распределяет их по панелям.
        /// </summary>
        private void InitializeNavigationItems()
        {
            // Верхнее меню (5 элементов)
            TopItems.Add(CreateItem("Главная", "home", 0));
            TopItems.Add(CreateItem("Мессенджер", "messenger", 1));
            TopItems.Add(CreateItem("Файлы", "files", 2));
            TopItems.Add(CreateItem("Группы", "groups", 3));
            TopItems.Add(CreateItem("Аудит", "audit", 4));

            // Нижнее меню (3 элемента)
            BottomItems.Add(CreateItem("Настройки", "settings", 0));
            BottomItems.Add(CreateItem("Аккаунт", "account", 1));
            BottomItems.Add(CreateItem("Выход", "logout", 2));
        }

        /// <summary>
        /// Создает один элемент навигации.
        /// </summary>
        private static NavigationItem CreateItem(string name, string iconFile, int order)
        {
            return new NavigationItem
            {
                Name = name,
                Icon = IconLoader.GetIcon(iconFile),
                Order = order
            };
        }

        // === КОМАНДЫ ===

        [RelayCommand]
        private void SelectItem(NavigationItem item)
        {
            SelectedItem = item;
        }

        [RelayCommand]
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
            ExpandChanged?.Invoke(this, IsExpanded);
        }

        partial void OnIsExpandedChanged(bool value)
        {
            ExpandChanged?.Invoke(this, value);
        }
    }
}
