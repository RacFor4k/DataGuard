# FilePage

## Обзор

UserControl страницы файлов. Содержит панель вкладок ([`FileTabs`](./FileTabs.md)), адресную строку, область контента и строку состояния.

### Путь
`Views/FilePage.axaml`

### Размеры

| Параметр | Значение |
|----------|----------|
| DesignWidth | 1000px |
| DesignHeight | 700px |

---

## Структура (XAML)

```xml
UserControl (FilePage)
└── Grid (4 строки)
    ├── Row 0: FileTabs (вкладки)
    ├── Row 1: Адресная строка (Border + DockPanel)
    │   ├── Button: Назад
    │   ├── Button: Вперёд
    │   ├── Button: Вверх
    │   ├── TextBlock: CurrentPath
    │   └── Button: + Добавить вкладку (демо)
    ├── Row 2: Основная область (заглушка с иконкой и текстом)
    └── Row 3: Строка состояния (Border + TextBlock: StatusText)
```

---

## Code-behind

### Класс
`Client.Views.FilePage : UserControl`

### Конструктор

```csharp
public Client.Views.FilePage.FilePage()
```
Параметры: нет.
Возвращает: —
Описание: Вызывает `InitializeComponent()`.

### `protected override void OnDataContextChanged(EventArgs e)`
Параметры:
- `e` — аргументы события.
Возвращает: —
Описание: Вызывает базовый метод. Резерв для дополнительной инициализации.

---

## DataContext

[FilePageViewModel](../viewmodels/FilePageViewModel.md)

| Биндинг | Назначение |
|---------|-----------|
| `Tabs` | DataContext для [FileTabs](./FileTabs.md) |
| `CurrentPath` | Текст адресной строки |
| `StatusText` | Текст строки состояния, заглушки контента |
| `AddDemoTabCommand` | Команда кнопки "+ Добавить вкладку" |

---

## Связи

| Использует | Тип |
|------------|-----|
| [FilePageViewModel](../viewmodels/FilePageViewModel.md) | DataContext |
| [FileTabsViewModel](../viewmodels/FileTabsViewModel.md) | Через `Tabs` — DataContext [FileTabs](./FileTabs.md) |
| [FileTabs](./FileTabs.md) | Вложенный UserControl (вкладки) |
