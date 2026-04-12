using System;
using Avalonia.Controls;
using Client.ViewModels;

namespace Client.Views
{
    /// <summary>
    /// Client.Views.FilePage — страница файлов.
    /// Содержит панель вкладок (FileTabs), адресную строку, область контента и строку состояния.
    /// </summary>
    public partial class FilePage : UserControl
    {
        /// <summary>
        /// Конструктор. Инициализирует XAML.
        /// </summary>
        public FilePage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Вызывается при изменении DataContext.
        /// Подписывается на изменения CurrentPath для обновления UI.
        /// </summary>
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            // Дополнительная инициализация при необходимости
        }
    }
}
