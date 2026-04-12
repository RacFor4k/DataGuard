# NavigationItem

## Обзор

Модель элемента навигации в боковом меню. Содержит отображаемое имя, иконку и порядок сортировки.

### Путь
`Models/NavigationItem.cs`

---

## Класс

```csharp
public class Client.Models.NavigationItem
```

### Описание

Простой POCO-класс для передачи данных элемента навигации между [`SidebarViewModel`](../viewmodels/SidebarViewModel.md) и [`Sidebar`](../views/Sidebar.md). Не содержит логики, только данные.

---

## Конструктор

```csharp
public Client.Models.NavigationItem.NavigationItem()
```
Параметры: нет.
Возвращает: —
Описание: Конструктор по умолчанию. Свойства инициализируются значениями по умолчанию (`string.Empty`, `null`, `0`).

---

## Свойства

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `public` | `Name` | `string` | Отображаемое имя пункта (например, `"Главная"`). Инициализируется `string.Empty`. |
| `public` | `Icon` | `Geometry?` | Иконка пункта в виде `StreamGeometry`. Загружается через [`IconLoader`](../services/IconLoader.md). Может быть `null`. |
| `public` | `Order` | `int` | Порядок сортировки внутри коллекции. |

---

## Пример использования

```csharp
var item = new NavigationItem
{
    Name = "Главная",
    Icon = IconLoader.GetIcon("home"),
    Order = 0
};
```

---

## Связи

| Используется в | Тип |
|----------------|-----|
| [SidebarViewModel](../viewmodels/SidebarViewModel.md) | `TopItems`, `BottomItems`, `SelectedItem` |
| [Sidebar](../views/Sidebar.md) | `Content` кнопок навигации (DataTemplate) |
| `Avalonia.Media.Geometry` (внешняя) | Тип свойства `Icon` |
