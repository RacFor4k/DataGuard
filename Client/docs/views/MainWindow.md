# MainWindow

## Обзор

Главное окно приложения DataGuard. Содержит боковую панель навигации (`Sidebar`) и область контента.

### Путь
`Views/MainWindow.axaml`

### Размер
1444×900px, центрирование при запуске

---

## Структура

```xml
Window
└── Grid
    ├── Column 0: Sidebar (автоматическая ширина)
    └── Column 1: Main Content (*)
```

---

## Properties (XAML)

| Свойство | Значение | Описание |
|----------|----------|----------|
| `x:DataType` | `vm:MainWindowViewModel` | Типизированный DataContext |
| `Title` | `"DataGuard"` | Заголовок окна |
| `Width` | `1444` | Ширина по умолчанию |
| `Height` | `900` | Высота по умолчанию |
| `WindowStartupLocation` | `CenterScreen` | Позиция при запуске |
| `Icon` | `/Assets/avalonia-logo.ico` | Иконка окна |

---

## DataContext

[MainWindowViewModel](../viewmodels/MainWindowViewModel.md)

| Биндинг | Назначение |
|---------|-----------|
| `Sidebar` | DataContext для [Sidebar](./Sidebar.md) |
| `Greeting` | Текст приветствия в центральной области |

---

## Связи

| Использует | Тип |
|------------|-----|
| [MainWindowViewModel](../viewmodels/MainWindowViewModel.md) | DataContext |
| [Sidebar](./Sidebar.md) | Вложенный UserControl |
| [SidebarViewModel](../viewmodels/SidebarViewModel.md) | Через `Sidebar.DataContext` |
