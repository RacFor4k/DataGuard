using Avalonia.Media;
using System;

namespace Client.Models
{
    /// <summary>
    /// Модель вкладки в панели файлов (FileTabs).
    /// Содержит информацию об отображаемом имени, иконке, пути и состоянии вкладки.
    /// </summary>
    public class FileTab : IEquatable<FileTab>
    {
        /// <summary>
        /// public string DisplayName — отображаемое имя вкладки (например, "Рабочий стол", "Документы").
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// public Geometry? Icon — иконка вкладки (SVG Path). null если не задана.
        /// </summary>
        public Geometry? Icon { get; set; }

        /// <summary>
        /// public string Path — путь папки, которую отображает вкладка (например, "C:\Users\Name\Desktop").
        /// Для системных папок используется идентификатор (например, "desktop://").
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// public bool IsPinned — true если вкладка закреплена (нельзя закрыть).
        /// Вкладка "Рабочий стол" всегда закреплена.
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// public Guid Id — уникальный идентификатор вкладки. Генерируется при создании.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Сравнение вкладок по Id.
        /// </summary>
        public bool Equals(FileTab? other)
        {
            if (other is null) return false;
            return Id == other.Id;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is FileTab tab && Equals(tab);

        /// <inheritdoc/>
        public override int GetHashCode() => Id.GetHashCode();
    }
}
