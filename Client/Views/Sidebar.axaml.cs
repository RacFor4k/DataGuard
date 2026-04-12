using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Client.ViewModels;

namespace Client.Views
{
    /// <summary>
    /// Боковое меню навигации.
    /// Поддерживает анимацию изменения ширины и выделение активного элемента.
    /// </summary>
    public partial class Sidebar : UserControl
    {
        private const double ExpandedWidth = 250;
        private const double CollapsedWidth = 60;
        private const double AnimationSpeed = 3.0;
        private const double AnimationThreshold = 0.5;

        private SidebarViewModel? _viewModel;
        private double _targetWidth = ExpandedWidth;
        private double _currentWidth = ExpandedWidth;

        public Sidebar()
        {
            InitializeComponent();

            Width = ExpandedWidth;

            StartAnimationTimer();
        }

        /// <summary>
        /// Запускает таймер для плавной анимации изменения ширины.
        /// </summary>
        private void StartAnimationTimer()
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            timer.Tick += OnAnimationTick;
            timer.Start();
        }

        /// <summary>
        /// Обработчик тика анимации.
        /// </summary>
        private void OnAnimationTick(object? sender, EventArgs e)
        {
            var difference = Math.Abs(_currentWidth - _targetWidth);
            
            if (difference > AnimationThreshold)
            {
                _currentWidth += (_targetWidth - _currentWidth) / AnimationSpeed;
                Width = _currentWidth;
            }
            else if (difference > 0.01)
            {
                _currentWidth = _targetWidth;
                Width = _currentWidth;
            }
        }

        /// <summary>
        /// Вызывается при изменении DataContext.
        /// Подписывается на события нового ViewModel.
        /// </summary>
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is SidebarViewModel vm)
            {
                _viewModel = vm;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _targetWidth = _viewModel.IsExpanded ? ExpandedWidth : CollapsedWidth;
                
                UpdateTextVisibility(_viewModel.IsExpanded);
                UpdateSelection();
            }
        }

        /// <summary>
        /// Обработчик изменений свойств ViewModel.
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_viewModel == null) return;

            switch (e.PropertyName)
            {
                case nameof(SidebarViewModel.IsExpanded):
                    _targetWidth = _viewModel.IsExpanded ? ExpandedWidth : CollapsedWidth;
                    UpdateTextVisibility(_viewModel.IsExpanded);
                    break;
                    
                case nameof(SidebarViewModel.SelectedItem):
                    UpdateSelection();
                    break;
            }
        }

        /// <summary>
        /// Обновляет видимость текста в кнопках навигации.
        /// </summary>
        private void UpdateTextVisibility(bool isVisible)
        {
            foreach (var textBlock in this.GetVisualDescendants().OfType<TextBlock>())
            {
                if (textBlock.Classes.Contains("nav-text"))
                {
                    textBlock.Opacity = isVisible ? 1.0 : 0.0;
                }
            }
        }

        /// <summary>
        /// Обновляет выделение активной кнопки.
        /// </summary>
        private void UpdateSelection()
        {
            if (_viewModel == null) return;

            UpdateSelectionInPanel(TopButtonsPanel);
            UpdateSelectionInPanel(BottomButtonsPanel);
        }

        /// <summary>
        /// Обновляет выделение кнопок в панели.
        /// </summary>
        private void UpdateSelectionInPanel(StackPanel panel)
        {
            if (panel.Children == null) return;

            foreach (var child in panel.Children)
            {
                if (child is Button btn && btn.CommandParameter is Models.NavigationItem item)
                {
                    var isSelected = _viewModel?.SelectedItem == item;
                    
                    if (isSelected && !btn.Classes.Contains("selected"))
                    {
                        btn.Classes.Add("selected");
                    }
                    else if (!isSelected)
                    {
                        btn.Classes.Remove("selected");
                    }
                }
            }
        }
    }
}
