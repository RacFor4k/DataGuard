using System;
using System.Collections.Concurrent;
using System.IO;
using Avalonia.Media;
using Avalonia.Platform;

namespace Client.Services
{
    /// <summary>
    /// Сервис для загрузки иконок из SVG-файлов.
    /// Кэширует загруженные иконки для повышения производительности.
    /// </summary>
    public static class IconLoader
    {
        private static readonly ConcurrentDictionary<string, StreamGeometry> _cache = new();
        private const string IconsPath = "avares://Client/Assets/icons";

        /// <summary>
        /// Загружает иконку по имени файла (без расширения .svg).
        /// Результат кэшируется для последующих вызовов.
        /// </summary>
        /// <param name="iconName">Имя иконки (например, "home", "settings")</param>
        /// <returns>StreamGeometry для использования в Path.Data</returns>
        public static StreamGeometry GetIcon(string iconName)
        {
            return _cache.GetOrAdd(iconName, key =>
            {
                var uri = new Uri($"{IconsPath}/{key}.svg");
                
                using var stream = AssetLoader.Open(uri);
                using var reader = new StreamReader(stream);
                var svgContent = reader.ReadToEnd();
                
                return ParseSvgPath(svgContent);
            });
        }

        /// <summary>
        /// Извлекает Path data из SVG содержимого и преобразует в StreamGeometry.
        /// </summary>
        private static StreamGeometry ParseSvgPath(string svgContent)
        {
            // Ищем атрибут d="..." в теге path
            var pathStart = svgContent.IndexOf("d=\"", StringComparison.Ordinal);
            if (pathStart == -1)
                throw new InvalidOperationException($"SVG does not contain path data");
            
            pathStart += 3;
            var pathEnd = svgContent.IndexOf('"', pathStart);
            var pathData = svgContent.Substring(pathStart, pathEnd - pathStart);
            
            // StreamGeometry.Parse понимает тот же формат что и SVG path
            var geometry = StreamGeometry.Parse(pathData);
            return geometry;
        }

        /// <summary>
        /// Очищает кэш иконок (для тестирования или горячей перезагрузки).
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}
