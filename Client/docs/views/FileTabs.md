# FileTabs

## Обзор

UserControl панели вкладок в стиле проводника Windows 11. Поддерживает drag & drop для перемещения вкладок, прокрутку колесиком мыши, адаптивную ширину вкладок и кнопки прокрутки по краям.

### Путь
`Views/FileTabs.axaml`

### Размеры

| Параметр | Значение |
|----------|----------|
| DesignWidth | 800px |
| DesignHeight | 40px |

---

## Структура (XAML)

```xml
UserControl (FileTabs)
└── Grid (3 колонки)
    ├── Col 0: Кнопка "←" (прокрутка влево, 32px)
    ├── Col 1: ScrollViewer → StackPanel (вкладки)
    └── Col 2: Кнопка "→" (прокрутка вправо, 32px)
```

---

## Code-behind

### Класс
`Client.Views.FileTabs : UserControl`

### Константы

| Имя | Тип | Значение | Описание |
|-----|-----|----------|----------|
| `DragThreshold` | `double` | `5.0` | Минимальное перемещение (px) для начала drag-операции |

### Поля

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `private` | `_viewModel` | [FileTabsViewModel](../viewmodels/FileTabsViewModel.md)`?` | Ссылка на DataContext |
| `private` | `_dragStartPoint` | `Point?` | Начальная точка нажатия для drag |
| `private` | `_dragFromIndex` | `int` | Индекс перетаскиваемой вкладки |
| `private` | `_dragIndicator` | `Border?` | Визуальный индикатор места вставки (синяя полоска 3px) |

---

## Конструктор

```csharp
public Client.Views.FileTabs.FileTabs()
```
Параметры: нет.
Возвращает: —
Описание: Вызывает `InitializeComponent()` и `CreateDragIndicator()`.

---

## Методы

### `private void CreateDragIndicator()`
Параметры: нет.
Возвращает: —
Описание: Создаёт `Border` (ширина 3px, цвет `#0078D4`, `IsVisible = false`) — индикатор места вставки при drag & drop.

### `protected override void OnDataContextChanged(EventArgs e)`
Параметры:
- `e` — аргументы события.
Возвращает: —
Описание: При DataContext = [`FileTabsViewModel`](../viewmodels/FileTabsViewModel.md) вызывает `BuildTabButtons()` и `SubscribeToScrollViewer()`.

### `private void SubscribeToScrollViewer()`
Параметры: нет.
Возвращает: —
Описание: Находит `ScrollViewer` по имени `TabsScrollViewer`. Подписывается на `PointerWheelChangedEvent` для прокрутки колесиком.

### `private void BuildTabButtons()`
Параметры: нет.
Возвращает: —
Описание: Создаёт кнопки вкладок динамически в `StackPanel`. Для каждой вкладки из `Tabs`:
1. Создаёт `StackPanel` (контейнер) с `Width = CalculatedTabWidth`.
2. Создаёт `Button` с `DockPanel` (иконка + текст).
3. Если не закреплена — добавляет кнопку закрытия (крестик).
4. Подписывает на `Click`, `PointerPressed`, `PointerMoved`, `PointerReleased` для drag & drop.

### `private StackPanel CreateTabButton(FileTab tab, int index)`
Параметры:
- `tab` — модель вкладки ([`FileTab`](../models/FileTab.md)).
- `index` — индекс в коллекции.
Возвращает: `StackPanel` — контейнер с кнопкой вкладки.
Описание: Создаёт кнопку вкладки с иконкой, текстом и (опционально) крестиком закрытия. Подписывает на drag & drop события.

### `private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e, FileTab tab, int index)`
Параметры:
- `sender` — источник события.
- `e` — аргументы нажатия.
- `tab` — модель вкладки.
- `index` — индекс.
Возвращает: —
Описание: Запоминает `_dragStartPoint` и `_dragFromIndex` для определения начала drag-операции.

### `private void OnTabPointerMoved(object? sender, PointerEventArgs e, FileTab tab, int index)`
Параметры:
- `sender` — источник события.
- `e` — аргументы перемещения.
- `tab` — модель вкладки.
- `index` — индекс.
Возвращает: —
Описание: Если перемещение > `DragThreshold` (5px) — показывает `_dragIndicator` и обновляет его позицию через `GetInsertIndex()` и `UpdateDragIndicatorPosition()`.

### `private void OnTabPointerReleased(object? sender, PointerReleasedEventArgs e)`
Параметры:
- `sender` — источник события.
- `e` — аргументы отпускания.
Возвращает: —
Описание: Вычисляет `targetIndex` через `GetInsertIndex()`. Если `targetIndex != fromIndex` — вызывает `ReorderTabCommand`. Перестраивает кнопки. Вызывает `ResetDrag()`.

### `private int GetInsertIndex(Point position)`
Параметры:
- `position` — позиция курсора относительно контрола.
Возвращает: `int` — индекс для вставки (0..Count-1).
Описание: Делит `position.X` на `CalculatedTabWidth + 2` (spacing). Ограничивает `Clamp(0, Count-1)`.

### `private void UpdateDragIndicatorPosition(Point position, int targetIndex)`
Параметры:
- `position` — позиция курсора.
- `targetIndex` — целевой индекс.
Возвращает: —
Описание: Устанавливает `Margin.X = targetIndex * (CalculatedTabWidth + 2)`, `IsVisible = true`.

### `private void ResetDrag()`
Параметры: нет.
Возвращает: —
Описание: Скрывает `_dragIndicator`, удаляет из родителя, очищает `_dragStartPoint` и `_dragFromIndex`.

### `private void OnCloseTabClick(object? sender, RoutedEventArgs e)`
Параметры:
- `sender` — кнопка закрытия.
- `e` — аргументы клика.
Возвращает: —
Описание: Вызывает `CloseTabCommand` с вкладкой из `Button.Tag`. Перестраивает кнопки.

### `private void OnScrollViewerWheel(object? sender, PointerWheelEventArgs e)`
Параметры:
- `sender` — ScrollViewer.
- `e` — аргументы колесика.
Возвращает: —
Описание: Вычисляет `delta` из горизонтальной или вертикальной прокрутки. Вызывает `ScrollBy(delta * 30)`.

### `protected override void OnSizeChanged(SizeChangedEventArgs e)`
Параметры:
- `e` — аргументы изменения размера.
Возвращает: —
Описание: При изменении размера контрола вызывает `RecalculateTabWidth()` с `availableWidth = NewSize.Width - arrowWidth` (если стрелки видны).

---

## Стили

| Селектор | Свойства |
|----------|----------|
| `Button.tab-button` | `Background: Transparent`, `Padding: 8,4`, `CornerRadius: 4,4,0,0`, `BorderThickness: 0,0,0,1` |
| `Button.tab-button.selected` | `Background: SystemControlBackgroundChromeMediumBrush`, `BorderBrush: SystemControlHighlightAccentBrush` |
| `Button.tab-button:pointerover` | `Background: SystemControlBackgroundListLowBrush` |
| `Button.close-tab-button` | `Width: 16`, `Height: 16`, `Padding: 2`, `CornerRadius: 3` |
| `Button.scroll-button` | `Width: 32`, `Padding: 4`, `CornerRadius: 2` |

---

## Связи

| Использует | Тип |
|------------|-----|
| [FileTabsViewModel](../viewmodels/FileTabsViewModel.md) | DataContext |
| [FileTab](../models/FileTab.md) | Content кнопок |
| [IconLoader](../services/IconLoader.md) | Иконки вкладок, крестик закрытия |
