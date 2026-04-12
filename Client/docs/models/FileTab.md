# FileTab

## Обзор

Модель вкладки в панели файлов (FileTabs). Содержит информацию об отображаемом имени, иконке, пути и состоянии вкладки.

### Путь
`Models/FileTab.cs`

---

## Класс

```csharp
public class Client.Models.FileTab
    : IEquatable<FileTab>
```

### Описание

POCO-класс с поддержкой сравнения по `Guid Id`. Представляет одну вкладку в [`FileTabs`](../views/FileTabs.md). Первая вкладка "Рабочий стол" всегда закреплена (`IsPinned = true`).

---

## Конструктор

```csharp
public Client.Models.FileTab.FileTab()
```
Параметры: нет.
Возвращает: —
Описание: Конструктор по умолчанию. Генерирует новый `Guid` для `Id`. Свойства инициализируются значениями по умолчанию.

---

## Свойства

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `public` | `DisplayName` | `string` | Отображаемое имя (например, `"Рабочий стол"`, `"Документы"`). Инициализируется `string.Empty`. |
| `public` | `Icon` | `Geometry?` | Иконка вкладки. Загружается через [`IconLoader`](../services/IconLoader.md). Может быть `null`. |
| `public` | `Path` | `string` | Путь папки (реальный или идентификатор: `"desktop://"`, `"documents://"`). Инициализируется `string.Empty`. |
| `public` | `IsPinned` | `bool` | `true` если вкладка закреплена (нельзя закрыть). Закреплённая — только первая вкладка. |
| `public` | `Id` | `Guid` | Уникальный идентификатор. Генерируется при создании, read-only. |

---

## Методы

### `public bool Equals(FileTab? other)`
Параметры:
- `other` — сравниваемая вкладка.
Возвращает: `bool` — `true` если `Id` совпадают.
Описание: Сравнение по `Id` для `IEquatable`.

### `public override bool Equals(object? obj)`
Параметры:
- `obj` — объект для сравнения.
Возвращает: `bool` — `true` если `obj` — `FileTab` с тем же `Id`.
Описание: Переопределение `object.Equals`.

### `public override int GetHashCode()`
Возвращает: `int` — хэш `Id`.
Описание: Переопределение `object.GetHashCode`.

---

## Связи

| Используется в | Тип |
|----------------|-----|
| [FileTabsViewModel](../viewmodels/FileTabsViewModel.md) | Коллекция `Tabs` |
| [FileTabs](../views/FileTabs.md) | Content кнопок вкладок |
| [IconLoader](../services/IconLoader.md) | Свойство `Icon` |
