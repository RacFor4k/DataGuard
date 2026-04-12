# IconLoader

## Обзор

Статический сервис для загрузки и кэширования SVG-иконок из ресурсов приложения. Преобразует SVG path data в `StreamGeometry` для использования в Avalonia.

### Путь
`Services/IconLoader.cs`

---

## Класс

```csharp
public static class Client.Services.IconLoader
```

### Описание

Предоставляет метод `GetIcon` для загрузки SVG-файлов из `avares://Client/Assets/icons/`. Результат кэшируется в `ConcurrentDictionary` для избежания повторных чтений файлов.

---

## Константы

| Имя | Тип | Значение | Описание |
|-----|-----|----------|----------|
| `IconsPath` | `string` | `"avares://Client/Assets/icons"` | Базовый URI папки с иконками |

---

## Поля

| Доступ | Имя | Тип | Описание |
|--------|-----|-----|----------|
| `private static readonly` | `_cache` | `ConcurrentDictionary<string, StreamGeometry>` | Кэш загруженных иконок. Ключ — имя иконки без расширения. |

---

## Методы

### `public static StreamGeometry GetIcon(string iconName)`
Параметры:
- `iconName` — имя иконки без расширения (например, `"home"`, `"settings"`)
Возвращает: `StreamGeometry` — геометрия для использования в `Path.Data`.
Описание: Загружает SVG-файл по пути `{IconsPath}/{iconName}.svg`. Если иконка уже в кэше — возвращает кэшированную. Иначе читает файл, парсит path data и кэширует результат. Потокобезопасный благодаря `ConcurrentDictionary.GetOrAdd`.
Исключения: `InvalidOperationException` — если SVG не содержит атрибут `d="..."` в теге `<path>`.

### `private static StreamGeometry ParseSvgPath(string svgContent)`
Параметры:
- `svgContent` — содержимое SVG-файла (строка)
Возвращает: `StreamGeometry` — распарсенная геометрия.
Описание: Извлекает значение атрибута `d` из тега `<path>` через поиск подстроки. Передаёт в `StreamGeometry.Parse()`. Формат SVG path совместим с Avalonia.
Ограничения: Работает только с простыми SVG, содержащими один `<path>`. Не поддерживает multi-path SVG.

### `public static void ClearCache()`
Параметры: нет.
Возвращает: —
Описание: Очищает `_cache`. Предназначен для тестирования или горячей перезагрузки ресурсов.

---

## Пример использования

```csharp
var homeIcon = IconLoader.GetIcon("home");
// Возвращает StreamGeometry из Assets/icons/home.svg
```

---

## Связи

| Используется в | Тип |
|----------------|-----|
| [SidebarViewModel](../viewmodels/SidebarViewModel.md) | `CreateItem()` — загрузка иконок для пунктов меню |
