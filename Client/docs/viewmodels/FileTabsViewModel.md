# FileTabsViewModel

## Обзор

ViewModel панели вкладок файлов. Управляет коллекцией вкладок, выбранной вкладкой, прокруткой и порядком. Чистая строка вкладок — без адресной строки и строки состояния.

### Путь
`ViewModels/FileTabsViewModel.cs`

---

## Класс

```csharp
public partial class Client.ViewModels.FileTabsViewModel
    : ViewModelBase
```

### Описание

Управляет вкладками типа [`FileTab`](../models/FileTab.md): добавление (кнопка "+"), закрытие, перемещение (drag & drop), прокрутка (стрелки + колесико). Первая вкладка "Рабочий стол" всегда закреплена. Адаптивная ширина: MaxTabWidth (160px) → MinTabWidth (60px) + кнопки прокрутки.

---

## Константы

| Имя | Тип | Значение | Описание |
|-----|-----|----------|----------|
| `MaxTabWidth` | `double` | `160.0` | Максимальная ширина одной вкладки |
| `MinTabWidth` | `double` | `60.0` | Минимальная ширина вкладки при переполнении |
| `TabSpacing` | `double` | `2.0` | Расстояние между вкладками |
| `AddButtonWidth` | `double` | `32.0` | Ширина кнопки "+" |
| `ScrollArrowWidth` | `double` | `24.0` | Ширина кнопки прокрутки |

---

## Поля

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `private` | `_horizontalOffset` | `double` | Текущее смещение прокрутки. `[ObservableProperty]`. |
| `private` | `_selectedTab` | [FileTab](../models/FileTab.md)`?` | Выбранная вкладка. `[ObservableProperty]`. |
| `private` | `_calculatedTabWidth` | `double` | Адаптивная ширина (160 → 60). `[ObservableProperty]`. |
| `private` | `_showScrollButtons` | `bool` | Показывать кнопки прокрутки. `[ObservableProperty]`. |
| `private` | `_canScrollLeft` | `bool` | Можно прокрутить влево. `[ObservableProperty]`. |
| `private` | `_canScrollRight` | `bool` | Можно прокрутить вправо. `[ObservableProperty]`. |
| `private` | `_newTabCounter` | `int` | Счётчик для имён "Новая вкладка N". |

---

## Свойства

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `public` | `Tabs` | `ObservableCollection<`[FileTab](../models/FileTab.md)`>` | Все вкладки. Первая — "Рабочий стол". |

---

## События

| Имя | Тип | Описание |
|-----|-----|----------|
| `ScrollRequested` | `Action<double>` | Вызывается при прокрутке (стрелки). Аргумент — целевое смещение для анимации. |

---

## Конструктор

```csharp
public Client.ViewModels.FileTabsViewModel.FileTabsViewModel()
```
Параметры: нет.
Возвращает: —
Описание: Вызывает `InitializeDesktopTab()`. Создаёт закреплённую вкладку "Рабочий стол" и автовыбирает её.

---

## Методы

### `public void AddTab()`
Параметры: нет.
Возвращает: —
Описание: `[RelayCommand]`. Создаёт вкладку "Новая вкладка N" (N = счётчик). В будущем — логика создания с путём. Выбирает новую вкладку.

### `public void CloseTab(FileTab tab)`
Параметры:
- `tab` — вкладка для закрытия.
Возвращает: —
Описание: `[RelayCommand]`. Не закрывает закреплённые. При закрытии `SelectedTab` — выбирает соседнюю слева (или справа).

### `public void SelectTab(FileTab tab)`
Параметры:
- `tab` — вкладка для выбора.
Возвращает: —
Описание: `[RelayCommand]`. Устанавливает `SelectedTab = tab`.

### `public void ReorderTab((int From, int To) args)`
Параметры:
- `args.From` — исходный индекс.
- `args.To` — целевой индекс.
Возвращает: —
Описание: `[RelayCommand]`. Перемещает через `Tabs.Move()`. Запрещает: перемещение закреплённой с позиции 0, не-закреплённой на позицию 0.

### `public void ScrollLeft()`
Параметры: нет.
Возвращает: —
Описание: `[RelayCommand]`. Прокрутка влево на 1 элемент (`CalculatedTabWidth + TabSpacing`). Вызывает `ScrollRequested`.

### `public void ScrollRight()`
Параметры: нет.
Возвращает: —
Описание: `[RelayCommand]`. Прокрутка вправо на 1 элемент. Вызывает `ScrollRequested`.

### `public void ScrollBy(double delta)`
Параметры:
- `delta` — дельта (положительная = вправо).
Возвращает: —
Описание: Вызывается при прокрутке колесиком. `HorizontalOffset = max(0, current + delta)`.

### `public void Recalculate(double availableWidth)`
Параметры:
- `availableWidth` — доступная ширина контейнера.
Возвращает: —
Описание: Пересчитывает `CalculatedTabWidth` и `ShowScrollButtons`. Логика:
1. Если все вкладки + кнопка "+" влезают при `MaxTabWidth` — использует её, прокрутка выключена.
2. Иначе — фиксирует `MinTabWidth`, включает `ShowScrollButtons`.

---

## Связи

| Использует | Тип |
|------------|-----|
| [ViewModelBase](./ViewModelBase.md) | Наследование |
| [FileTab](../models/FileTab.md) | Коллекция `Tabs`, `SelectedTab` |
| [IconLoader](../services/IconLoader.md) | Иконки вкладок |
