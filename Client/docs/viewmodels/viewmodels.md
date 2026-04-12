# ViewModels — Модели представлений

## Обзор категории

**ViewModels** — слой моделей представлений в архитектуре MVVM. Содержит состояние UI, команды и бизнес-логику отображения. Все ViewModel наследуют [`ViewModelBase`](./ViewModelBase.md), который в свою очередь наследует `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`.

### Структура папки

```
ViewModels/
├── ViewModelBase.cs          # Базовый класс (ObservableObject)
├── MainWindowViewModel.cs    # Главная ViewModel окна
└── SidebarViewModel.cs       # ViewModel бокового меню
```

### Роль в архитектуре

```
View ← Data Binding → ViewModel → Services → Backend
```

ViewModel отвечает за:
- Хранение состояния UI через `[ObservableProperty]`
- Обработку действий пользователя через `[RelayCommand]`
- Валидацию входных данных
- Навигацию между страницами
- Вызов сервисов для получения данных

### Правила оформления

1. **Одна ViewModel = один файл**
2. **Без прямой работы с UI** — никаких `Button`, `TextBlock` и т.д.
3. **Асинхронные команды** — `RelayCommand` с `async Task`
4. **CancellationToken** — передаётся во все асинхронные операции
5. **Null-безопасность** — nullable reference types включены

---

## Компоненты

| Компонент | Файл | Описание |
|-----------|------|----------|
| [ViewModelBase](./ViewModelBase.md) | `ViewModelBase.cs` | Базовый абстрактный класс для всех ViewModel |
| [MainWindowViewModel](./MainWindowViewModel.md) | `MainWindowViewModel.cs` | ViewModel главного окна |
| [SidebarViewModel](./SidebarViewModel.md) | `SidebarViewModel.cs` | ViewModel бокового меню навигации |
| [FileTabsViewModel](./FileTabsViewModel.md) | `FileTabsViewModel.cs` | ViewModel панели вкладок файлов |
| [FilePageViewModel](./FilePageViewModel.md) | `FilePageViewModel.cs` | ViewModel страницы файлов |

---

## Связи с другими компонентами

| Использует | Зависимость |
|------------|------------|
| ViewModelBase | `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` (внешняя) |
| MainWindowViewModel | [ViewModelBase](./ViewModelBase.md) — наследование |
| MainWindowViewModel | [SidebarViewModel](./SidebarViewModel.md) — свойство `Sidebar`, подписка на `SelectedItem` |
| MainWindowViewModel | [FilePageViewModel](./FilePageViewModel.md) — DataContext для кэшированного `FilePage` |
| MainWindowViewModel | [FilePage](../views/FilePage.md) — кэшированный View (`_cachedFilePage`) |
| MainWindowViewModel | [HomePage](../views/HomePage.md) — кэшированный View (`_cachedHomePage`) |
| SidebarViewModel | [NavigationItem](../models/NavigationItem.md) — коллекции `TopItems`, `BottomItems`, `SelectedItem` |
| SidebarViewModel | [IconLoader](../services/IconLoader.md) — загрузка иконок |
| FileTabsViewModel | [FileTab](../models/FileTab.md) — коллекция `Tabs`, `SelectedTab` |
| FileTabsViewModel | [IconLoader](../services/IconLoader.md) — иконки вкладок |
| FilePageViewModel | [FileTabsViewModel](./FileTabsViewModel.md) — свойство `Tabs`, подписка на `SelectedTab` |
