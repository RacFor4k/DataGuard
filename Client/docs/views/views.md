# Views — Представления

## Обзор категории

**Views** — слой представлений в архитектуре MVVM. Содержит XAML-файлы и соответствующие code-behind классы, отвечающие за отображение данных и обработку пользовательского ввода через привязки к ViewModel.

### Структура папки

```
Views/
├── MainWindow.axaml(.cs)  # Главное окно приложения
├── Sidebar.axaml(.cs)     # Боковое меню навигации
└── HomePage.axaml         # Домашняя страница
```

### Роль в архитектуре

```
View (XAML + code-behind)
    ↓ DataContext
ViewModel (состояние, команды)
```

View получает данные через `DataContext` (биндинг к ViewModel). Code-behind используется только для:
- Инициализации компонентов (`InitializeComponent`)
- Подписки на события ViewModel (анимации, визуальные эффекты)
- Работы с визуальным деревом (анимации ширины, opacity)

### Правила оформления

1. **Один View = один файл `.axaml` + один `.axaml.cs`**
2. **Минимум логики в code-behind** — всё состояние в ViewModel
3. **Compiled Bindings** — используется `x:DataType` для типизированных привязок
4. **Без хардкода цветов** — `{DynamicResource}` для адаптивности

---

## Компоненты

| Компонент | Файл | Описание |
|-----------|------|----------|
| [MainWindow](./MainWindow.md) | `MainWindow.axaml` | Главное окно (1444×900), содержит Sidebar + контент |
| [Sidebar](./Sidebar.md) | `Sidebar.axaml` | Боковая панель навигации с анимацией сворачивания |
| [HomePage](./HomePage.md) | `HomePage.axaml` | Стартовая страница с приветствием |
| [FileTabs](./FileTabs.md) | `FileTabs.axaml` | Панель вкладок файлов (drag & drop, прокрутка) |
| [FilePage](./FilePage.md) | `FilePage.axaml` | Страница файлов с адресной строкой и контентом |

---

## Связи с другими компонентами

| Использует | Зависимость |
|------------|------------|
| MainWindow | [MainWindowViewModel](../viewmodels/MainWindowViewModel.md) — DataContext |
| MainWindow | [Sidebar](./Sidebar.md) — вложенный контрол |
| MainWindow | [SidebarViewModel](../viewmodels/SidebarViewModel.md) — биндинг через Sidebar.DataContext |
| MainWindow | [FilePageViewModel](../viewmodels/FilePageViewModel.md) — через `CurrentPage` → DataTemplate |
| Sidebar | [NavigationItem](../models/NavigationItem.md) — Content кнопок |
| HomePage | — (пока не использует ViewModel) |
| FileTabs | [FileTabsViewModel](../viewmodels/FileTabsViewModel.md) — DataContext |
| FileTabs | [FileTab](../models/FileTab.md) — Content кнопок вкладок |
| FilePage | [FilePageViewModel](../viewmodels/FilePageViewModel.md) — DataContext |
| FilePage | [FileTabs](./FileTabs.md) — вложенный UserControl |
