using System;
using System.Collections.Generic;
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
    /// Client.ViewModels.FileTabsViewModel — ViewModel панели вкладок файлов.
    /// Управляет коллекцией вкладок, выбранной вкладкой, прокруткой и порядком.
    /// </summary>
    public partial class FileTabsViewModel : ViewModelBase
    {
        // === КОНСТАНТЫ ===
        /// <summary>
        /// private const double MaxTabWidth — максимальная ширина одной вкладки в пикселях.
        /// </summary>
        private const double MaxTabWidth = 160.0;

        /// <summary>
        /// private const double MinTabWidth — минимальная ширина вкладки при переполнении.
        /// </summary>
        private const double MinTabWidth = 60.0;

        /// <summary>
        /// private const double TabSpacing — расстояние между вкладками в пикселях.
        /// </summary>
        private const double TabSpacing = 2.0;

        /// <summary>
        /// private const double AddButtonWidth — ширина кнопки "+" в пикселях.
        /// </summary>
        private const double AddButtonWidth = 32.0;

        /// <summary>
        /// private const double ScrollArrowWidth — ширина кнопки прокрутки (стрелки) в пикселях.
        /// </summary>
        private const double ScrollArrowWidth = 24.0;

        // === ПОЛЯ ===
        /// <summary>
        /// private double _horizontalOffset — текущее смещение прокрутки вкладок.
        /// </summary>
        [ObservableProperty]
        private double _horizontalOffset;

        /// <summary>
        /// private FileTab? _selectedTab — текущая выбранная вкладка.
        /// </summary>
        [ObservableProperty]
        private FileTab? _selectedTab;

        /// <summary>
        /// private double _calculatedTabWidth — рассчитанная ширина каждой вкладки (адаптивная).
        /// </summary>
        [ObservableProperty]
        private double _calculatedTabWidth = MaxTabWidth;

        /// <summary>
        /// private bool _showScrollButtons — показывать ли кнопки прокрутки по краям.
        /// true если суммарная ширина вкладок превышает доступное пространство.
        /// </summary>
        [ObservableProperty]
        private bool _showScrollButtons;

        /// <summary>
        /// private bool _canScrollLeft — можно ли прокрутить влево.
        /// </summary>
        [ObservableProperty]
        private bool _canScrollLeft;

        /// <summary>
        /// private bool _canScrollRight — можно ли прокрутить вправо.
        /// </summary>
        [ObservableProperty]
        private bool _canScrollRight;

        /// <summary>
        /// private int _newTabCounter — счётчик для генерации имён новых вкладок.
        /// </summary>
        private int _newTabCounter;

        // === КОЛЛЕКЦИИ ===
        /// <summary>
        /// public ObservableCollection&lt;FileTab&gt; Tabs — коллекция всех открытых вкладок.
        /// Первая вкладка всегда "Рабочий стол" (закреплённая).
        /// </summary>
        public ObservableCollection<FileTab> Tabs { get; } = new();

        // === СОБЫТИЯ ===
        /// <summary>
        /// public event Action&lt;double&gt;? ScrollRequested — вызывается для прокрутки с анимацией.
        /// Аргумент — целевое смещение.
        /// </summary>
        public event Action<double>? ScrollRequested;

        // === КОНСТРУКТОР ===
        /// <summary>
        /// Инициализирует панель вкладок с закреплённой вкладкой "Рабочий стол".
        /// Загружает иконку монитора через IconLoader.
        /// </summary>
        public FileTabsViewModel()
        {
            InitializeDesktopTab();
        }

        // === ИНИЦИАЛИЗАЦИЯ ===
        /// <summary>
        /// Создаёт закреплённую вкладку "Рабочий стол" и добавляет её первой в коллекцию.
        /// Устанавливает SelectedTab на неё (автовыбор при первом запуске).
        /// </summary>
        private void InitializeDesktopTab()
        {
            var desktopTab = new FileTab
            {
                DisplayName = "Рабочий стол",
                Icon = IconLoader.GetIcon("desktop"),
                Path = "desktop://",
                IsPinned = true
            };

            Tabs.Add(desktopTab);
            SelectedTab = desktopTab;
        }

        // === КОМАНДЫ ===

        /// <summary>
        /// Создаёт новую вкладку с именем "Новая вкладка N".
        /// В будущем здесь будет логика создания вкладки с путём.
        /// </summary>
        [RelayCommand]
        public void AddTab()
        {
            _newTabCounter++;

            var newTab = new FileTab
            {
                DisplayName = $"Новая вкладка {_newTabCounter}",
                Icon = IconLoader.GetIcon("folder"),
                Path = $"newtab://{_newTabCounter}",
                IsPinned = false
            };

            Tabs.Add(newTab);
            SelectedTab = newTab;
        }

        /// <summary>
        /// Закрывает указанную вкладку.
        /// Не закрывает закреплённые вкладки.
        /// Если закрывается выбранная — выбирает соседнюю слева, или справа если слева нет.
        /// </summary>
        /// <param name="tab">Вкладка для закрытия.</param>
        [RelayCommand]
        public void CloseTab(FileTab tab)
        {
            if (tab is null || tab.IsPinned)
                return;

            var index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);

            // Если закрыли выбранную — выбираем соседнюю
            if (SelectedTab == tab)
            {
                if (Tabs.Count > 0)
                {
                    // Пробуем слева, если не получилось — справа
                    var newIndex = Math.Max(0, index - 1);
                    if (newIndex >= Tabs.Count)
                        newIndex = Tabs.Count - 1;
                    SelectedTab = Tabs[newIndex];
                }
                else
                {
                    SelectedTab = null;
                }
            }
        }

        /// <summary>
        /// Выбирает указанную вкладку.
        /// </summary>
        /// <param name="tab">Вкладка для выбора.</param>
        [RelayCommand]
        public void SelectTab(FileTab tab)
        {
            if (tab is not null)
                SelectedTab = tab;
        }

        /// <summary>
        /// Перемещает вкладку из одной позиции в другую (drag & drop reorder).
        /// Вкладка следует за курсором по оси X, при отпускании остаётся на новом месте.
        /// Не позволяет переместить закреплённую вкладку с позиции 0.
        /// </summary>
        /// <param name="args">Кортеж (fromIndex, toIndex) для перемещения.</param>
        [RelayCommand]
        public void ReorderTab((int From, int To) args)
        {
            if (args.From < 0 || args.From >= Tabs.Count)
                return;
            if (args.To < 0 || args.To >= Tabs.Count)
                return;
            if (args.From == args.To)
                return;

            // Нельзя перемещать закреплённую вкладку (позиция 0)
            var tab = Tabs[args.From];
            if (tab.IsPinned && args.To != 0)
                return;

            // Нельзя перемещать на позицию 0 не-закреплённую вкладку
            if (!tab.IsPinned && args.To == 0)
                return;

            Tabs.Move(args.From, args.To);
        }

        /// <summary>
        /// Прокручивает вкладки влево на 1 элемент (CalculatedTabWidth + spacing).
        /// Вызывает ScrollRequested для анимации.
        /// </summary>
        [RelayCommand]
        public void ScrollLeft()
        {
            var step = CalculatedTabWidth + TabSpacing;
            var targetOffset = Math.Max(0, HorizontalOffset - step);
            HorizontalOffset = targetOffset;
            ScrollRequested?.Invoke(targetOffset);
        }

        /// <summary>
        /// Прокручивает вкладки вправо на 1 элемент (CalculatedTabWidth + spacing).
        /// Вызывает ScrollRequested для анимации.
        /// </summary>
        [RelayCommand]
        public void ScrollRight()
        {
            var step = CalculatedTabWidth + TabSpacing;
            var targetOffset = HorizontalOffset + step;
            HorizontalOffset = targetOffset;
            ScrollRequested?.Invoke(targetOffset);
        }

        /// <summary>
        /// Прокручивает на указанную дельту (положительная = вправо, отрицательная = влево).
        /// Вызывается при прокрутке колесика мыши.
        /// </summary>
        /// <param name="delta">Дельта прокрутки в пикселях.</param>
        public void ScrollBy(double delta)
        {
            HorizontalOffset = Math.Max(0, HorizontalOffset + delta);
        }

        /// <summary>
        /// Пересчитывает ширину вкладок и необходимость кнопок прокрутки.
        /// Если суммарная ширина вкладок + кнопка "+" превышает доступное пространство —
        /// фиксирует MinTabWidth и включает кнопки прокрутки.
        /// </summary>
        /// <param name="availableWidth">Доступная ширина контейнера.</param>
        public void Recalculate(double availableWidth)
        {
            if (Tabs.Count == 0)
            {
                CalculatedTabWidth = MaxTabWidth;
                ShowScrollButtons = false;
                return;
            }

            // Место для кнопки "+"
            var usableWidth = availableWidth - AddButtonWidth;
            if (usableWidth <= 0)
            {
                CalculatedTabWidth = MinTabWidth;
                ShowScrollButtons = true;
                UpdateScrollState(0);
                return;
            }

            // Сколько места нужно на все вкладки при MaxTabWidth (с учётом spacing)
            var totalWidth = Tabs.Count * MaxTabWidth + (Tabs.Count - 1) * TabSpacing;

            if (totalWidth <= usableWidth)
            {
                // Все влезают — максимальная ширина, прокрутка не нужна
                CalculatedTabWidth = MaxTabWidth;
                ShowScrollButtons = false;
                CanScrollLeft = false;
                CanScrollRight = false;
                HorizontalOffset = 0;
            }
            else
            {
                // Не влезают — фиксируем MinTabWidth, включаем прокрутку
                CalculatedTabWidth = MinTabWidth;
                ShowScrollButtons = true;
                UpdateScrollState(usableWidth);
            }
        }

        /// <summary>
        /// Обновляет состояние кнопок прокрутки на основе текущего смещения.
        /// Вызывается при изменении HorizontalOffset.
        /// </summary>
        /// <param name="usableWidth">Доступная ширина (минус AddButtonWidth).</param>
        private void UpdateScrollState(double usableWidth)
        {
            var totalTabsWidth = Tabs.Count * MinTabWidth + (Tabs.Count - 1) * TabSpacing;
            var maxOffset = Math.Max(0, totalTabsWidth - usableWidth);

            CanScrollLeft = HorizontalOffset > 0.5;
            CanScrollRight = HorizontalOffset < maxOffset - 0.5;
        }

        // === ЧАСТИЧНЫЕ МЕТОДЫ (ObservableProperty) ===

        /// <summary>
        /// Вызывается при изменении HorizontalOffset.
        /// Обновляет CanScrollLeft/Right если включены кнопки прокрутки.
        /// </summary>
        partial void OnHorizontalOffsetChanged(double value)
        {
            if (!ShowScrollButtons) return;
            CanScrollLeft = value > 0.5;
        }
    }
}
