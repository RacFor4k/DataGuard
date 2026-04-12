using Avalonia.Media;

namespace Client.Models
{
    /// <summary>
    /// Элемент навигации в боковом меню.
    /// </summary>
    public class NavigationItem
    {
        public string Name { get; set; } = string.Empty;
        public Geometry? Icon { get; set; }
        public int Order { get; set; }
    }
}
