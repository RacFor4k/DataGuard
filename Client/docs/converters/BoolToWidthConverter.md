# BoolToWidthConverter

## Обзор

Конвертер значений, преобразующий `bool` в `double` (ширину) и обратно. Используется для привязки состояния разворачивания к ширине элемента.

### Путь
`Converters/BoolToWidthConverter.cs`

---

## Класс

```csharp
public class Client.Converters.BoolToWidthConverter
    : IValueConverter
```

### Описание

Реализует `IValueConverter` для двухсторонней привязки. Преобразует `true` → `250.0`, `false` → `5.0`. Обратное преобразование: ширина > 100 → `true`, иначе `false`.

---

## Методы

### `public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)`
Параметры:
- `value` — входное значение (`bool?`). Ожидается `true` или `false`.
- `targetType` — целевой тип (игнорируется).
- `parameter` — дополнительный параметр (не используется).
- `culture` — культура (не используется).
Возвращает: `double` — `250.0` если `true`, `5.0` если `false`. Значение по умолчанию `250.0` при `null`.
Описание: Прямое преобразование для отображения состояния `IsExpanded` в ширину панели.

### `public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)`
Параметры:
- `value` — входное значение (`double?`). Ширина элемента.
- `targetType` — целевой тип (игнорируется).
- `parameter` — дополнительный параметр (не используется).
- `culture` — культура (не используется).
Возвращает: `bool` — `true` если ширина > 100, иначе `false`. Значение по умолчанию `true` при `null`.
Описание: Обратное преобразование для двухсторонней привязки. Порог 100 выбран как промежуточное значение между `CollapsedWidth` (60) и `ExpandedWidth` (250).

---

## Пример использования (XAML)

```xml
<ColumnDefinition Width="{Binding IsExpanded, Converter={StaticResource BoolToWidthConverter}}"/>
```

---

## Связи

| Используется в | Тип |
|----------------|-----|
| [Sidebar](../views/Sidebar.md) | Потенциально для биндинга ширины колонки (вместо code-behind анимации) |
