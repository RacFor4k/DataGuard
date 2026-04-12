# SidebarViewModel

## Обзор

ViewModel бокового меню навигации. Управляет элементами меню, режимом отображения (развернут/свернут) и выбранным элементом.

### Путь
`ViewModels/SidebarViewModel.cs`

---

## Класс

```csharp
public partial class Client.ViewModels.SidebarViewModel
    : ViewModelBase
```

### Описание

Инициализирует 8 пунктов навигации (5 верхних + 3 нижних), управляет состоянием панели и обработкой выбора. Использует [`IconLoader`](../services/IconLoader.md) для загрузки иконок.

---

## Константы

| Имя | Тип | Значение | Описание |
|-----|-----|----------|----------|
| `TopItemsCount` | `int` | `5` | Количество элементов верхнего меню |
| `BottomItemsCount` | `int` | `3` | Количество элементов нижнего меню |

---

## Поля

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `private` | `_isExpanded` | `bool` | Состояние панели (true = развернута). Инициализируется `true`. |
| `private` | `_selectedItem` | [NavigationItem](../models/NavigationItem.md)`?` | Текущий выбранный элемент навигации. |

---

## Свойства

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `public` | `IsExpanded` | `bool` | Состояние панели. Генерируется `[ObservableProperty]`. |
| `public` | `SelectedItem` | [NavigationItem](../models/NavigationItem.md)`?` | Выбранный пункт меню. Генерируется `[ObservableProperty]`. |
| `public` | `AllItems` | `ObservableCollection<`[NavigationItem](../models/NavigationItem.md)`>` | Все элементы навигации. |
| `public` | `TopItems` | `ObservableCollection<`[NavigationItem](../models/NavigationItem.md)`>` | Элементы верхнего меню (Главная, Мессенджер, Файлы, Группы, Аудит). |
| `public` | `BottomItems` | `ObservableCollection<`[NavigationItem](../models/NavigationItem.md)`>` | Элементы нижнего меню (Настройки, Аккаунт, Выход). |

---

## События

| Имя | Тип | Описание |
|-----|-----|----------|
| `ExpandChanged` | `EventHandler<bool>` | Вызывается при изменении `IsExpanded`. Аргумент — новое значение. |

---

## Конструктор

```csharp
public Client.ViewModels.SidebarViewModel.SidebarViewModel()
```
Параметры: нет.
Возвращает: —
Описание: Вызывает `InitializeNavigationItems()` для создания пунктов меню. Устанавливает `SelectedItem` на первый элемент `TopItems`.

---

## Методы

### `private void InitializeNavigationItems()`
Параметры: нет.
Возвращает: —
Описание: Создаёт 8 элементов навигации через `CreateItem()` и добавляет в `TopItems` (5 шт.) и `BottomItems` (3 шт.).

### `private static NavigationItem CreateItem(string name, string iconFile, int order)`
Параметры:
- `name` — отображаемое имя пункта (например, `"Главная"`)
- `iconFile` — имя иконки без расширения (например, `"home"`)
- `order` — порядок сортировки
Возвращает: [NavigationItem](../models/NavigationItem.md) — заполненный элемент навигации.
Описание: Создаёт `NavigationItem`, загружая иконку через [`IconLoader.GetIcon`](../services/IconLoader.md).

### `private void SelectItem(NavigationItem item)`
Параметры:
- `item` — выбранный элемент навигации
Возвращает: —
Описание: `[RelayCommand]`. Устанавливает `SelectedItem = item`.

### `private void ToggleExpand()`
Параметры: нет.
Возвращает: —
Описание: `[RelayCommand]`. Переключает `IsExpanded` на противоположное значение. Вызывает событие `ExpandChanged`.

### `partial void OnIsExpandedChanged(bool value)`
Параметры:
- `value` — новое значение `IsExpanded`
Возвращает: —
Описание: Частичный метод от `[ObservableProperty]`. Вызывает `ExpandChanged`.

---

## Навигационные пункты

### Верхнее меню

| Имя | Иконка | Order |
|-----|--------|-------|
| Главная | `home` | 0 |
| Мессенджер | `messenger` | 1 |
| Файлы | `files` | 2 |
| Группы | `groups` | 3 |
| Аудит | `audit` | 4 |

### Нижнее меню

| Имя | Иконка | Order |
|-----|--------|-------|
| Настройки | `settings` | 0 |
| Аккаунт | `account` | 1 |
| Выход | `logout` | 2 |

---

## Связи

| Использует | Тип |
|------------|-----|
| [ViewModelBase](./ViewModelBase.md) | Наследование |
| [NavigationItem](../models/NavigationItem.md) | Коллекции, SelectedItem |
| [IconLoader](../services/IconLoader.md) | `CreateItem()` — загрузка иконок |
