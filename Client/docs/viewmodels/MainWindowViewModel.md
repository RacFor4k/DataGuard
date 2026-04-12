# MainWindowViewModel

## Обзор

ViewModel главного окна. Содержит приветствие, боковую панель и кэшированные Views страниц. Сохраняет состояние каждой страницы за счёт переиспользования UserControl.

### Путь
`ViewModels/MainWindowViewModel.cs`

---

## Класс

```csharp
public partial class Client.ViewModels.MainWindowViewModel
    : ViewModelBase
```

### Описание

Корневая ViewModel приложения. Управляет кэшированием Views для сохранения состояния при переключении между страницами. Каждый View создаётся один раз (`??=`) и переиспользуется.

---

## Поля

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `private` | `_currentView` | `Control?` | Текущий отображаемый View. Генерируется `[ObservableProperty]`. |
| `private` | `_cachedFilePage` | [FilePage](../views/FilePage.md)`?` | Кэшированный View страницы файлов. |
| `private` | `_cachedHomePage` | [HomePage](../views/HomePage.md)`?` | Кэшированный View домашней страницы. |

---

## Свойства

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `public` | `Greeting` | `string` | Текст приветствия. Значение: `"Welcome to Avalonia!"`. Read-only. |
| `public` | `Sidebar` | [SidebarViewModel](./SidebarViewModel.md) | ViewModel боковой панели. Инициализируется в конструкторе. Read-only. |
| `public` | `FilePage` | [FilePageViewModel](./FilePageViewModel.md) | ViewModel страницы файлов. Read-only. |
| `public` | `CurrentView` | `Control?` | Кэшированный View для отображения в ContentControl. |

---

## Конструктор

```csharp
public Client.ViewModels.MainWindowViewModel.MainWindowViewModel()
```
Параметры: нет.
Возвращает: —
Описание: Вызывает `SubscribeToSidebarChanges()`. Инициализирует `CurrentView` стартовой страницей через `GetOrCreatePage(null)`.

---

## Методы

### `private void SubscribeToSidebarChanges()`
Параметры: нет.
Возвращает: —
Описание: Подписывается на `PropertyChanged` в `Sidebar`. При изменении `SelectedItem` вызывает `GetOrCreatePage()` и обновляет `CurrentView`.

### `private Control GetOrCreatePage(string? selectedItemName)`
Параметры:
- `selectedItemName` — имя выбранного пункта навигации, или `null` для стартовой.
Возвращает: `Control` — кэшированный View.
Описание: Возвращает или создаёт View для пункта меню. Кэширует через `??=`:
- `"Файлы"` → `_cachedFilePage` ([FilePage](../views/FilePage.md) с [FilePageViewModel](./FilePageViewModel.md))
- Остальное → `_cachedHomePage` ([HomePage](../views/HomePage.md))

### `private FilePage CreateFilePage()`
Параметры: нет.
Возвращает: [FilePage](../views/FilePage.md).
Описание: Создаёт `FilePage` с `DataContext = FilePage`.

### `private HomePage CreateHomePage()`
Параметры: нет.
Возвращает: [HomePage](../views/HomePage.md).
Описание: Создаёт `HomePage`.

---

## Связи

| Использует | Тип |
|------------|-----|
| [ViewModelBase](./ViewModelBase.md) | Наследование |
| [SidebarViewModel](./SidebarViewModel.md) | Свойство `Sidebar`, подписка на `SelectedItem` |
| [FilePageViewModel](./FilePageViewModel.md) | DataContext для `FilePage` |
| [FilePage](../views/FilePage.md) | Кэшированный View |
| [HomePage](../views/HomePage.md) | Кэшированный View |
