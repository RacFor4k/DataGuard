# Sidebar

## Обзор

Боковое меню навигации с поддержкой сворачивания/разворачивания и плавной анимацией ширины.

### Путь
`Views/Sidebar.axaml`

### Режимы отображения

| Режим | Ширина | Описание |
|-------|--------|----------|
| Развернутый | 250px | Иконки + текст |
| Свернутый | 60px | Только иконки по центру |

---

## Структура (XAML)

```xml
UserControl (Sidebar)
└── Border
    └── Grid
        ├── Row 0: ScrollViewer → Верхнее меню (5 кнопок)
        │   └── Кнопка «Свернуть» + Separator + TopItems[0-4]
        └── Row 1: Border → Нижнее меню (3 кнопки)
            └── BottomItems[0-2]
```

---

## Code-behind

### Класс
`Client.Views.Sidebar : UserControl`

### Константы

| Имя | Тип | Значение | Описание |
|-----|-----|----------|----------|
| `ExpandedWidth` | `double` | `250` | Ширина в развернутом режиме |
| `CollapsedWidth` | `double` | `60` | Ширина в свернутом режиме |
| `AnimationSpeed` | `double` | `3.0` | Коэффициент скорости анимации |
| `AnimationThreshold` | `double` | `0.5` | Порог завершения анимации |

### Поля

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `private` | `_viewModel` | `SidebarViewModel?` | Ссылка на текущий ViewModel |
| `private` | `_targetWidth` | `double` | Целевая ширина (анимация) |
| `private` | `_currentWidth` | `double` | Текущая анимированная ширина |

### Конструктор

```csharp
public Client.Views.Sidebar.Sidebar()
```
Инициализирует компоненты, устанавливает начальную ширину `ExpandedWidth`, запускает `DispatcherTimer` для анимации (16мс интервал).

### Методы

#### `private void StartAnimationTimer()`
Запускает `DispatcherTimer` с интервалом 16мс (~60fps). Каждый тик вызывает `OnAnimationTick`.

#### `private void OnAnimationTick(object? sender, EventArgs e)`
Обработчик тика таймера. Вычисляет разницу между `_currentWidth` и `_targetWidth`. Если разница > `AnimationThreshold` (0.5) — плавно приближает ширину. Если < 0.01 — фиксирует точно.

#### `protected override void OnDataContextChanged(EventArgs e)`
Вызывается при смене DataContext. Подписывается на `PropertyChanged` нового ViewModel, устанавливает `_targetWidth`, обновляет видимость текста и выделение кнопок.

#### `private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)`
Обработчик изменений свойств ViewModel. Реагирует на:
- `IsExpanded` — меняет `_targetWidth`, обновляет видимость текста
- `SelectedItem` — обновляет выделение кнопок

#### `private void UpdateTextVisibility(bool isVisible)`
Проходит по всем `TextBlock` с классом `nav-text` в визуальном дереве. Устанавливает `Opacity` в `1.0` или `0.0`.

#### `private void UpdateSelection()`
Обновляет выделение в обеих панелях (`TopButtonsPanel`, `BottomButtonsPanel`).

#### `private void UpdateSelectionInPanel(StackPanel panel)`
Проходит по кнопкам в панели. Сравнивает `CommandParameter` с `SelectedItem`. Добавляет/удаляет класс `selected`.

---

## DataContext

[SidebarViewModel](../viewmodels/SidebarViewModel.md)

| Биндинг | Назначение |
|---------|-----------|
| `TopItems[0-4]` | Content верхних кнопок |
| `BottomItems[0-2]` | Content нижних кнопок |
| `ToggleExpandCommand` | Команда кнопки «Свернуть» |
| `SelectItemCommand` | Команда выбора пункта |

---

## Стили

| Селектор | Свойства |
|----------|----------|
| `Button.nav-button` | `Background: Transparent`, `Padding: 12,10`, `Margin: 4,1`, `CornerRadius: 4` |
| `Button.nav-button:pointerover` | `Background: SystemControlBackgroundListLowBrush` |
| `Button.nav-button.selected` | `Background: SystemControlBackgroundListMediumBrush`, `Foreground: SystemControlHighlightAccentBrush` |

---

## Связи

| Использует | Тип |
|------------|-----|
| [SidebarViewModel](../viewmodels/SidebarViewModel.md) | DataContext |
| [NavigationItem](../models/NavigationItem.md) | Content кнопок, CommandParameter |
