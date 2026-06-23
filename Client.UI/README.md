# Client.UI — Avalonia GUI Client

Клиентское GUI-приложение для **DataGuard** на базе **Avalonia 11** (.NET 8).

---

## Требования

- .NET 8.0 SDK или новее
- Visual Studio 2022 / VS Code / Rider

---

## Запуск

```bash
cd Client.UI
dotnet restore
dotnet run
```

> ⚠ Для сборки на машине без интернета: сначала выполни `dotnet restore` при наличии доступа к NuGet.

---

## Demo-вход

Пароль: `demo` (в LoginView)

Это откроет полностью рабочий UI с демо-данными без подключения к серверу.

---

## Структура проекта

```
Client.UI/
├── Assets/
│   └── AppStyles.axaml         # Глобальные стили: цвета, кнопки, карточки, типографика
├── Models/
│   └── DataModels.cs           # FileItem, ChatMessage, AuditEntry и др.
├── Protos/
│   ├── auth.proto              # gRPC контракт: Register, Login
│   └── company_manager.proto   # gRPC контракт: CreateCompany
├── Services/
│   └── GrpcClientService.cs    # Обёртка над gRPC каналом
├── ViewModels/
│   ├── MainWindowViewModel.cs  # Навигация, авторизация, уведомления
│   ├── AuthViewModels.cs       # LoginVM, RegisterVM, SetupCompanyVM
│   └── FeatureViewModels.cs    # FilesVM, MessengerVM, ExternalAccessVM, PoliciesVM, AuditVM, SettingsVM
├── Views/
│   ├── MainWindow.axaml        # Shell: title bar + sidebar + content area + toast notifications
│   ├── LoginView.axaml         # Экран входа
│   ├── RegisterView.axaml      # Регистрация с паролем (валидация)
│   ├── SetupCompanyView.axaml  # Создание компании → получение кода
│   ├── FilesView.axaml         # Дерево + список + правая панель (Свойства / Создать ссылку)
│   ├── MessengerView.axaml     # Чаты, документальные треды, одобрение доступа
│   ├── ExternalAccessView.axaml# KPI + таблица ссылок + журнал + детальная панель
│   ├── PoliciesView.axaml      # Группы, шаблоны политик, запреты файлов
│   ├── AuditView.axaml         # Лог событий + фильтры + быстрые отчёты + детали
│   └── SettingsView.axaml      # Настройки сервера, безопасность, о приложении
├── App.axaml / App.axaml.cs
├── Program.cs
└── Client.UI.csproj
```

---

## Архитектура

| Слой | Технология |
|------|------------|
| UI | Avalonia 11.2 (AXAML + code-behind) |
| Bindings | Compiled bindings (`x:DataType`) |
| ViewModel | CommunityToolkit.Mvvm (`ObservableObject`, `RelayCommand`) |
| gRPC | `Grpc.Net.Client` + generated stubs из `.proto` |
| Стили | Глобальный `AppStyles.axaml`, темная тема (#0F1117 base) |

---

## Интеграция с сервером

В `Services/GrpcClientService.cs` — обёртка над gRPC. Адрес по умолчанию: `https://localhost:7777`.

В ViewModels места для вызовов помечены `// TODO: call gRPC ...`.

Пример (Register):
```csharp
var client = new GrpcClientService();
var response = await client.AuthClient.RegisterAsync(new RegisterRequest {
    RegistrationCode = RegistrationCode,
    Password = Password
});
```

---

## Навигация (Beta 1.1)

| Раздел | Ключевые функции |
|--------|-----------------|
| Файлы | Дерево папок · Список файлов · Поиск · Правая панель: свойства + создание ссылки |
| Мессенджер | Чаты по документам · Одобрение/отклонение доступа |
| Внешний доступ | KPI · Активные ссылки · Журнал · Отзыв ссылок |
| Политики | Группы · Шаблоны · Запреты файлов |
| Аудит | Лог в реальном времени · Фильтры · Быстрые отчёты |
| Настройки | Адрес сервера · Безопасность сессии |

---

## Toast-уведомления

Всплывают в правом нижнем углу и автоматически исчезают через 5 секунд.
Управляются через `MainWindowViewModel.AddNotification(icon, title, message)`.
