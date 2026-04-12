using Avalonia.Controls;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels
{
    /// <summary>
    /// Client.ViewModels.MainWindowViewModel — корневая ViewModel главного окна.
    /// Управляет приветствием, боковой панелью и навигацией между страницами.
    /// Сохраняет состояние страниц за счёт кэширования View (UserControl).
    /// </summary>
    public partial class MainWindowViewModel : ViewModelBase
    {
        // === ПОЛЯ ===
        /// <summary>
        /// private Control? _currentView — текущий отображаемый View (UserControl).
        /// Кэшируется для сохранения состояния при переключении страниц.
        /// </summary>
        [ObservableProperty]
        private Control? _currentView;

        /// <summary>
        /// private FilePage? _cachedFilePage — кэшированный View страницы файлов.
        /// Создаётся один раз и переиспользуется для сохранения состояния вкладок.
        /// </summary>
        private FilePage? _cachedFilePage;

        /// <summary>
        /// private HomePage? _cachedHomePage — кэшированный View домашней страницы.
        /// </summary>
        private HomePage? _cachedHomePage;

        // === СВОЙСТВА ===
        /// <summary>
        /// public string Greeting — текст приветствия. Read-only.
        /// </summary>
        public string Greeting { get; } = "Welcome to Avalonia!";

        /// <summary>
        /// public SidebarViewModel Sidebar — ViewModel боковой панели навигации.
        /// Инициализируется в конструкторе.
        /// </summary>
        public SidebarViewModel Sidebar { get; } = new();

        /// <summary>
        /// public FilePageViewModel FilePage — ViewModel страницы файлов.
        /// Инициализируется в конструкторе, используется для кэшированного View.
        /// </summary>
        public FilePageViewModel FilePage { get; } = new();

        // === КОНСТРУКТОР ===
        /// <summary>
        /// Инициализирует ViewModel и подписывается на изменения Sidebar.SelectedItem
        /// для навигации между страницами.
        /// </summary>
        public MainWindowViewModel()
        {
            // Инициализируем стартовую страницу (HomePage)
            _cachedHomePage = CreateHomePage();
            _currentView = _cachedHomePage;

            // Автовыбор "Главная" в сайдбаре при запуске
            SelectDefaultSidebarItem();

            SubscribeToSidebarChanges();
        }

        /// <summary>
        /// Выбирает первый пункт "Главная" в сайдбаре при запуске приложения.
        /// </summary>
        private void SelectDefaultSidebarItem()
        {
            if (Sidebar.TopItems.Count > 0)
                Sidebar.SelectedItem = Sidebar.TopItems[0];
        }

        /// <summary>
        /// Подписывается на свойство SelectedItem в SidebarViewModel.
        /// При смене пункта меню обновляет CurrentView, сохраняя состояние страниц.
        /// </summary>
        private void SubscribeToSidebarChanges()
        {
            Sidebar.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SidebarViewModel.SelectedItem)
                    && Sidebar.SelectedItem is not null)
                {
                    CurrentView = GetOrCreatePage(Sidebar.SelectedItem.Name);
                }
            };
        }

        /// <summary>
        /// Создаёт или возвращает кэшированный View для указанного пункта меню.
        /// Каждый View создаётся один раз и переиспользуется для сохранения состояния.
        /// </summary>
        /// <param name="selectedItemName">Имя выбранного пункта навигации, или null для стартовой.</param>
        /// <returns>Control — кэшированный View для отображения в ContentControl.</returns>
        private Control GetOrCreatePage(string? selectedItemName)
        {
            return selectedItemName switch
            {
                "Файлы" => _cachedFilePage ??= CreateFilePage(),
                _ => _cachedHomePage ??= CreateHomePage()
            };
        }

        /// <summary>
        /// Создаёт View страницы файлов с привязкой к FilePageViewModel.
        /// </summary>
        /// <returns>FilePage — UserControl страницы файлов.</returns>
        private FilePage CreateFilePage()
        {
            return new FilePage { DataContext = FilePage };
        }

        /// <summary>
        /// Создаёт View домашней страницы.
        /// </summary>
        /// <returns>HomePage — UserControl стартовой страницы.</returns>
        private HomePage CreateHomePage()
        {
            return new HomePage();
        }
    }
}
