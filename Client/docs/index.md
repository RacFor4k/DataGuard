# DataGuard Client — Документация

## Обзор проекта

**DataGuard Client** — десктопное клиентское приложение системы DataGuard, разработанное на Avalonia UI. Обеспечивает графический интерфейс для взаимодействия с backend-сервером (ASP.NET Core) через gRPC/REST.

### Основные возможности

- Боковая навигация с режимами сворачивания/разворачивания
- Страницы: Главная, Мессенджер, Файлы, Группы, Аудит
- Настройки и управление аккаунтом
- Адаптивный плоский дизайн с поддержкой светлой/тёмной темы

### Архитектура

MVVM (Model-View-ViewModel) с разделением на слои:

```
Views → ViewModels → Services → Backend
```

### Технологии

- **Avalonia UI** 11.3.12 — кроссплатформенный UI-фреймворк
- **CommunityToolkit.Mvvm** 8.2.1 — MVVM-инфраструктура
- **.NET 10.0** — платформа

---

## Структура документации

| Категория | Файл | Описание |
|-----------|------|----------|
| Views | [views/](./views/views.md) | XAML-представления и code-behind |
| ViewModels | [viewmodels/](./viewmodels/viewmodels.md) | ViewModel-классы (состояние UI, команды) |
| Services | [services/](./services/services.md) | Сервисы (API, утилиты, загрузчики) |
| Models | [models/](./models/models.md) | Модели данных (DTO, сущности) |
| Converters | [converters/](./converters/converters.md) | Конвертеры значений (IValueConverter) |

---

## Компоненты по файлам

### Views

| Компонент | Документация |
|-----------|-------------|
| MainWindow | [views/MainWindow.md](./views/MainWindow.md) |
| Sidebar | [views/Sidebar.md](./views/Sidebar.md) |
| HomePage | [views/HomePage.md](./views/HomePage.md) |

### ViewModels

| Компонент | Документация |
|-----------|-------------|
| ViewModelBase | [viewmodels/ViewModelBase.md](./viewmodels/ViewModelBase.md) |
| MainWindowViewModel | [viewmodels/MainWindowViewModel.md](./viewmodels/MainWindowViewModel.md) |
| SidebarViewModel | [viewmodels/SidebarViewModel.md](./viewmodels/SidebarViewModel.md) |
| FileTabsViewModel | [viewmodels/FileTabsViewModel.md](./viewmodels/FileTabsViewModel.md) |
| FilePageViewModel | [viewmodels/FilePageViewModel.md](./viewmodels/FilePageViewModel.md) |

### Services

| Компонент | Документация |
|-----------|-------------|
| IconLoader | [services/IconLoader.md](./services/IconLoader.md) |

### Models

| Компонент | Документация |
|-----------|-------------|
| NavigationItem | [models/NavigationItem.md](./models/NavigationItem.md) |
| FileTab | [models/FileTab.md](./models/FileTab.md) |

### Converters

| Компонент | Документация |
|-----------|-------------|
| BoolToWidthConverter | [converters/BoolToWidthConverter.md](./converters/BoolToWidthConverter.md) |

---

## Соглашения

- Документация ведётся в Markdown
- Каждый компонент — отдельный `.md` файл
- В каждой категории — обзорный файл (`category.md`) со ссылками
- Собственные зависимости указываются через ссылки `[ClassName](path/to/file.md)`
- Сторонние библиотеки не документируются (ссылки на официальную документацию)
