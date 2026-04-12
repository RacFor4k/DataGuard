using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Client.Models;
using Client.Services;
using Client.ViewModels;

namespace Client.Views
{
    /// <summary>
    /// Client.Views.FileTabs — UserControl панели вкладок в стиле Windows 11.
    /// Только строка вкладок: выбор, закрытие, создание, drag-reorder, прокрутка.
    /// </summary>
    public partial class FileTabs : UserControl
    {
        // === КОНСТАНТЫ ===
        /// <summary>
        /// private const double DragThreshold — минимальное перемещение (px) для начала drag.
        /// </summary>
        private const double DragThreshold = 8.0;

        /// <summary>
        /// private const double ScrollAnimationDurationMs — длительность анимации прокрутки (мс).
        /// </summary>
        private const double ScrollAnimationDurationMs = 150.0;

        // === ПОЛЯ ===
        /// <summary>
        /// private FileTabsViewModel? _viewModel — ссылка на DataContext.
        /// </summary>
        private FileTabsViewModel? _viewModel;

        /// <summary>
        /// private ScrollViewer? _scrollViewer — ScrollViewer области вкладок.
        /// </summary>
        private ScrollViewer? _scrollViewer;

        /// <summary>
        /// private StackPanel? _tabsPanel — панель содержащая кнопки вкладок.
        /// </summary>
        private StackPanel? _tabsPanel;

        /// <summary>
        /// private Point? _dragStartPoint — начальная точка нажатия для drag.
        /// </summary>
        private Point? _dragStartPoint;

        /// <summary>
        /// private int _dragFromIndex — индекс перетаскиваемой вкладки.
        /// </summary>
        private int _dragFromIndex = -1;

        /// <summary>
        /// private Border? _dragIndicator — индикатор места вставки.
        /// </summary>
        private Border? _dragIndicator;

        /// <summary>
        /// private bool _isInitialized — флаг что контрол загружен и DataContext установлен.
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// Конструктор. Инициализирует XAML, создаёт индикатор drag.
        /// </summary>
        public FileTabs()
        {
            InitializeComponent();
            CreateDragIndicator();
        }

        /// <summary>
        /// Создаёт визуальный индикатор (вертикальную полоску 3px) для места вставки при drag.
        /// </summary>
        private void CreateDragIndicator()
        {
            _dragIndicator = new Border
            {
                Width = 3,
                Height = 24,
                Background = new SolidColorBrush(Color.Parse("#0078D4")),
                CornerRadius = new CornerRadius(1.5),
                IsVisible = false,
                ZIndex = 100
            };
        }

        /// <summary>
        /// Вызывается при загрузке контрола. Кэширует ссылки, строит вкладки.
        /// </summary>
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _scrollViewer = this.FindControl<ScrollViewer>("TabsScrollViewer");
            _tabsPanel = this.FindControl<StackPanel>("TabsPanel");

            if (_scrollViewer is not null)
            {
                _scrollViewer.AddHandler(InputElement.PointerWheelChangedEvent, OnScrollViewerWheel);
            }

            TryInitialize();
        }

        /// <summary>
        /// Вызывается при изменении DataContext. Сохраняет ссылку, пытается инициализировать.
        /// </summary>
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is FileTabsViewModel vm)
            {
                // Отписываемся от старого
                if (_viewModel is not null)
                    _viewModel.ScrollRequested -= AnimateScrollTo;

                _viewModel = vm;
                _viewModel.ScrollRequested += AnimateScrollTo;
                TryInitialize();
            }
        }

        /// <summary>
        /// Вызывается при выгрузке контрола. Отписывается от событий.
        /// </summary>
        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (_viewModel is not null)
                _viewModel.ScrollRequested -= AnimateScrollTo;

            _isInitialized = false;
        }

        /// <summary>
        /// Инициализирует вкладки когда оба условия выполнены: контрол загружен и DataContext установлен.
        /// </summary>
        private void TryInitialize()
        {
            if (_isInitialized) return;
            if (_tabsPanel is null || _viewModel is null) return;

            _isInitialized = true;
            BuildTabButtons();
            SubscribeToTabCollection();
        }

        /// <summary>
        /// Подписывается на изменения коллекции Tabs (добавление/удаление).
        /// </summary>
        private void SubscribeToTabCollection()
        {
            if (_viewModel is null) return;

            _viewModel.Tabs.CollectionChanged += (s, e) =>
            {
                var newIndex = _viewModel.Tabs.Count - 1;
                BuildTabButtons();
                RecalculateLayout();

                // Прокрутить к новой вкладке (добавлена в конец)
                if (e.NewStartingIndex >= 0)
                    ScrollToTab(newIndex);
            };

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileTabsViewModel.SelectedTab))
                    UpdateSelection();
            };
        }

        // === ПОСТРОЕНИЕ ВКЛАДОК ===

        /// <summary>
        /// Перестраивает все кнопки вкладок в _tabsPanel.
        /// </summary>
        private void BuildTabButtons()
        {
            if (_tabsPanel is null || _viewModel is null) return;

            _tabsPanel.Children.Clear();

            for (int i = 0; i < _viewModel.Tabs.Count; i++)
            {
                var tab = _viewModel.Tabs[i];
                _tabsPanel.Children.Add(CreateTabButton(tab, i));
            }

            UpdateSelection();
        }

        /// <summary>
        /// Создаёт контейнер вкладки: кнопка (иконка + текст) + крестик.
        /// </summary>
        private StackPanel CreateTabButton(FileTab tab, int index)
        {
            var tabWidth = _viewModel?.CalculatedTabWidth ?? 160;

            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 0,
                Width = tabWidth,
                Height = 32,
                Tag = index,
                Cursor = new Cursor(StandardCursorType.Hand),
                ClipToBounds = true
            };

            var button = new Button
            {
                Classes = { "tab-button" },
                Width = tab.IsPinned ? tabWidth : tabWidth - 18,
                Height = 32,
                Padding = new Thickness(8, 4),
                Tag = tab,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };

            if (_viewModel?.SelectedTab == tab)
                button.Classes.Add("selected");

            var dockPanel = new DockPanel();
            var iconBrush = (SolidColorBrush)(this.FindResource("SystemControlForegroundBaseHighBrush")
                ?? Brushes.Gray);

            var iconPath = new Path
            {
                Data = tab.Icon,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                Fill = iconBrush,
                Margin = new Thickness(0, 0, 6, 0)
            };
            DockPanel.SetDock(iconPath, Dock.Left);
            dockPanel.Children.Add(iconPath);

            var textBlock = new TextBlock
            {
                Text = tab.DisplayName,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = (tab.IsPinned ? tabWidth : tabWidth - 18) - 32
            };
            dockPanel.Children.Add(textBlock);

            button.Content = dockPanel;
            button.Click += (s, e) => _viewModel?.SelectTabCommand.Execute(tab);
            container.Children.Add(button);

            // Кнопка закрытия
            if (!tab.IsPinned)
            {
                var closeBtn = new Button
                {
                    Classes = { "close-tab-button" },
                    Tag = tab,
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 2, 0)
                };

                closeBtn.Content = new Path
                {
                    Data = IconLoader.GetIcon("close"),
                    Width = 10,
                    Height = 10,
                    Stretch = Stretch.Uniform,
                    Fill = iconBrush
                };
                closeBtn.Click += OnCloseTabClick;
                container.Children.Add(closeBtn);
            }

            // Drag & drop
            container.PointerPressed += (s, e) => OnTabPointerPressed(s, e, index);
            container.PointerMoved += (s, e) => OnTabPointerMoved(s, e, index);
            container.PointerReleased += (s, e) => OnTabPointerReleased(s, e);

            return container;
        }

        /// <summary>
        /// Обновляет класс "selected" на кнопках.
        /// </summary>
        private void UpdateSelection()
        {
            if (_tabsPanel is null || _viewModel is null) return;

            foreach (var child in _tabsPanel.Children)
            {
                if (child is StackPanel sp && sp.Children.Count > 0
                    && sp.Children[0] is Button btn && btn.Tag is FileTab tab)
                {
                    if (_viewModel.SelectedTab == tab)
                    {
                        if (!btn.Classes.Contains("selected"))
                            btn.Classes.Add("selected");
                    }
                    else
                    {
                        btn.Classes.Remove("selected");
                    }
                }
            }
        }

        // === DRAG & DROP ===

        private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e, int index)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _dragStartPoint = e.GetPosition(this);
                _dragFromIndex = index;
            }
        }

        private void OnTabPointerMoved(object? sender, PointerEventArgs e, int index)
        {
            if (_dragStartPoint is null || _dragFromIndex < 0
                || _viewModel is null || _tabsPanel is null)
                return;

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - _dragStartPoint!.Value;

            if (Math.Abs(delta.X) < DragThreshold)
                return;

            if (_dragIndicator is not null && _dragIndicator.Parent is null)
            {
                _tabsPanel.Children.Add(_dragIndicator);
                _dragIndicator.IsVisible = true;
            }

            var targetIndex = GetInsertIndex(currentPosition.X);
            UpdateDragIndicatorPosition(currentPosition.X, targetIndex);
        }

        private void OnTabPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_dragFromIndex < 0 || _viewModel is null)
            {
                ResetDrag();
                return;
            }

            var currentPosition = e.GetPosition(this);
            var targetIndex = GetInsertIndex(currentPosition.X);

            if (targetIndex != _dragFromIndex && targetIndex >= 0)
            {
                _viewModel.ReorderTabCommand.Execute((_dragFromIndex, targetIndex));
                BuildTabButtons();
            }

            ResetDrag();
        }

        private int GetInsertIndex(double x)
        {
            if (_viewModel is null || _viewModel.Tabs.Count == 0)
                return -1;

            var adjustedX = x + (_scrollViewer?.Offset.X ?? 0);
            var tabWidth = _viewModel.CalculatedTabWidth + 2;
            var index = (int)(adjustedX / tabWidth);

            return Math.Clamp(index, 0, _viewModel.Tabs.Count - 1);
        }

        private void UpdateDragIndicatorPosition(double x, int targetIndex)
        {
            if (_dragIndicator is null || _viewModel is null) return;

            var tabWidth = _viewModel.CalculatedTabWidth + 2;
            var offsetX = _scrollViewer?.Offset.X ?? 0;
            var insertX = targetIndex * tabWidth - offsetX;

            _dragIndicator.Margin = new Thickness(Math.Max(0, insertX), 4, 0, 4);
            _dragIndicator.IsVisible = true;
        }

        private void ResetDrag()
        {
            if (_dragIndicator is not null)
            {
                _dragIndicator.IsVisible = false;
                if (_dragIndicator.Parent is Panel p)
                    p.Children.Remove(_dragIndicator);
            }

            _dragStartPoint = null;
            _dragFromIndex = -1;
        }

        // === ЗАКРЫТИЕ ===

        private void OnCloseTabClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FileTab tab } && _viewModel is not null)
            {
                _viewModel.CloseTabCommand.Execute(tab);
            }
        }

        // === ПРОКРУТКА КОЛЕСИКОМ ===

        private void OnScrollViewerWheel(object? sender, PointerWheelEventArgs e)
        {
            if (_viewModel is null) return;
            if (!_viewModel.ShowScrollButtons) return;

            var delta = e.Delta.Y;
            _viewModel.ScrollBy(-delta * 30);
            e.Handled = true;
        }

        // === АНИМАЦИЯ ПРОКРУТКИ ===

        private void AnimateScrollTo(double targetOffset)
        {
            if (_scrollViewer is null) return;

            var startOffset = _scrollViewer.Offset.X;
            var target = Math.Max(0, targetOffset);
            var duration = TimeSpan.FromMilliseconds(ScrollAnimationDurationMs);

            var animation = new Animation
            {
                Duration = duration,
                FillMode = FillMode.Forward,
                Easing = new LinearEasing(),
                IterationCount = new IterationCount(1),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0.0),
                        Setters = { new Avalonia.Styling.Setter(ScrollViewer.OffsetProperty, new Vector(startOffset, 0)) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1.0),
                        Setters = { new Avalonia.Styling.Setter(ScrollViewer.OffsetProperty, new Vector(target, 0)) }
                    }
                }
            };

            animation.RunAsync(_scrollViewer);
        }

        /// <summary>
        /// Прокручивает к указанному индексу вкладки.
        /// Используется для автопрокрутки к новой вкладке.
        /// </summary>
        /// <param name="tabIndex">Индекс вкладки для прокрутки.</param>
        public void ScrollToTab(int tabIndex)
        {
            if (_viewModel is null || _scrollViewer is null || tabIndex < 0) return;
            if (!_viewModel.ShowScrollButtons) return;

            var tabWidth = _viewModel.CalculatedTabWidth + 2;
            var currentOffset = _scrollViewer.Offset.X;
            var visibleWidth = _scrollViewer.Bounds.Width;

            // Позиция начала вкладки
            var tabStartX = tabIndex * tabWidth;
            // Позиция конца вкладки
            var tabEndX = tabStartX + _viewModel.CalculatedTabWidth;

            double targetOffset;

            if (tabStartX < currentOffset)
            {
                // Вкладка слева за пределами видимости — прокрутить к ней
                targetOffset = tabStartX;
            }
            else if (tabEndX > currentOffset + visibleWidth)
            {
                // Вкладка справа за пределами — прокрутить чтобы была видна
                targetOffset = tabEndX - visibleWidth;
            }
            else
            {
                // Уже видна — не прокручиваем
                return;
            }

            AnimateScrollTo(targetOffset);
        }

        // === ПЕРЕСЧЁТ МАКЕТА ===

        private void RecalculateLayout()
        {
            if (_viewModel is null) return;

            // Доступная ширина = ширина ScrollViewer (минус padding)
            var scrollWidth = _scrollViewer?.Bounds.Width ?? 0;
            if (scrollWidth > 0)
                _viewModel.Recalculate(scrollWidth);
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            RecalculateLayout();
        }
    }
}
