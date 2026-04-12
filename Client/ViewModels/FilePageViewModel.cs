using Client.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels
{
    /// <summary>
    /// Client.ViewModels.FilePageViewModel — ViewModel страницы файлов.
    /// Содержит панель вкладок (FileTabsViewModel). В будущем будет содержать список файлов.
    /// </summary>
    public partial class FilePageViewModel : ViewModelBase
    {
        // === СВОЙСТВА ===
        /// <summary>
        /// public FileTabsViewModel Tabs — ViewModel панели вкладок.
        /// Управляет коллекцией вкладок, прокруткой, порядком и выбранной вкладкой.
        /// </summary>
        public FileTabsViewModel Tabs { get; } = new();

        // === КОНСТРУКТОР ===
        /// <summary>
        /// Инициализирует страницу файлов.
        /// Вкладка "Рабочий стол" создаётся автоматически с автовыбором.
        /// </summary>
        public FilePageViewModel()
        {
            // Tabs уже инициализирован, внутри него InitializeDesktopTab()
        }
    }
}
