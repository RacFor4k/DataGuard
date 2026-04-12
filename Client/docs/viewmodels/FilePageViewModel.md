# FilePageViewModel

## Обзор

ViewModel страницы файлов. Содержит панель вкладок ([`FileTabsViewModel`](./FileTabsViewModel.md)). В будущем будет содержать список файлов выбранной вкладки.

### Путь
`ViewModels/FilePageViewModel.cs`

---

## Класс

```csharp
public partial class Client.ViewModels.FilePageViewModel
    : ViewModelBase
```

### Описание

Обёртка над [`FileTabsViewModel`](./FileTabsViewModel.md). Автоматически создаёт вкладку "Рабочий стол" с автовыбором.

---

## Свойства

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `public` | `Tabs` | [FileTabsViewModel](./FileTabsViewModel.md) | ViewModel панели вкладок. Read-only. |

---

## Конструктор

```csharp
public Client.ViewModels.FilePageViewModel.FilePageViewModel()
```
Параметры: нет.
Возвращает: —
Описание: `Tabs` инициализируется inline. Внутри `Tabs` автоматически создаётся вкладка "Рабочий стол".

---

## Связи

| Использует | Тип |
|------------|-----|
| [ViewModelBase](./ViewModelBase.md) | Наследование |
| [FileTabsViewModel](./FileTabsViewModel.md) | Свойство `Tabs` |
