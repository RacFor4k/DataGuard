# Models — Модели данных

## Обзор категории

**Models** — слой моделей данных. Содержит DTO-классы и сущности, используемые для передачи данных между слоями приложения.

### Структура папки

```
Models/
└── NavigationItem.cs  # Модель элемента навигации
```

### Роль в архитектуре

```
ViewModel ← Models → Services → Backend (DTO)
```

Модели отвечают за:
- Структурирование данных для UI
- Передачу данных между ViewModel и Services
- Представление DTO от сервера

### Правила оформления

1. **Один класс = один файл** (исключение: маленькие DTO, до 5 в файле)
2. **Без логики** — только свойства, без методов (кроме `ToString`, `Equals`)
3. **POCO** — простые классы без наследования от фреймворковых классов
4. **Nullable включён** — использовать `?` для необязательных полей
5. **Инициализация по умолчанию** — строки `= string.Empty`, коллекции `= new()`

---

## Компоненты

| Компонент | Файл | Описание |
|-----------|------|----------|
| [NavigationItem](./NavigationItem.md) | `NavigationItem.cs` | Модель элемента навигации (имя, иконка, порядок) |
| [FileTab](./FileTab.md) | `FileTab.cs` | Модель вкладки в панели файлов (имя, иконка, путь, закреплённость) |

---

## Связи с другими компонентами

| Используется в | Зависимость |
|----------------|------------|
| NavigationItem | [SidebarViewModel](../viewmodels/SidebarViewModel.md) — `TopItems`, `BottomItems`, `SelectedItem` |
| NavigationItem | [Sidebar](../views/Sidebar.md) — `Content` кнопок навигации |
| NavigationItem | `Avalonia.Media.Geometry` (внешняя) — тип `Icon` |
| FileTab | [FileTabsViewModel](../viewmodels/FileTabsViewModel.md) — коллекция `Tabs`, `SelectedTab` |
| FileTab | [FileTabs](../views/FileTabs.md) — Content кнопок вкладок |
