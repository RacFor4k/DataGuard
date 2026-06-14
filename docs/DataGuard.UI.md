# DataGuard.UI — Документация клиентского GUI-приложения

## Обзор

**DataGuard.UI** — десктопное клиентское приложение для системы DataGuard, построенное на базе **Avalonia 11** (.NET 10). Предоставляет графический интерфейс для работы с файловым хранилищем, мессенджером, политиками безопасности, аудитом и управлением внешним доступом.

**Технологии:**
- Avalonia 11.2 (кроссплатформенный UI-фреймворк)
- CommunityToolkit.Mvvm (MVVM-паттерн, source generators)
- gRPC (связь с Client.Engine / сервером)
- Protobuf (сериализация контрактов)
- Тёмная тема (цвет фона `#0F1117`)

---

## Архитектура

```
DataGuard.UI/
├── Assets/
│   └── AppStyles.axaml              # Глобальные стили (цвета, кнопки, карточки, типографика)
├── Models/
│   └── DataModels.cs                # Модели данных: FileItem, ChatMessage, AuditEntry и др.
├── Protos/
│   ├── auth.proto                   # gRPC контракт аутентификации (Register, Login)
│   └── company_manager.proto        # gRPC контракт управления компанией (CreateCompany)
├── Services/
│   └── GrpcClientService.cs         # Обёртка над gRPC-каналом для связи с сервером
├── ViewModels/
│   ├── MainWindowViewModel.cs       # Главная VM: навигация, авторизация, уведомления
│   ├── AuthViewModels.cs            # VM аутентификации: Login, Register, SetupCompany
│   └── FeatureViewModels.cs         # VM разделов: Files, Messenger, ExternalAccess, Policies, Audit, Settings
├── Views/
│   ├── MainWindow.axaml(.cs)        # Главное окно: title bar + sidebar + content + toasts
│   ├── LoginView.axaml(.cs)         # Экран входа
│   ├── RegisterView.axaml(.cs)      # Экран регистрации
│   ├── SetupCompanyView.axaml(.cs)  # Экран создания компании
│   ├── FilesView.axaml(.cs)         # Раздел «Файлы»
│   ├── MessengerView.axaml(.cs)     # Раздел «Мессенджер»
│   ├── ExternalAccessView.axaml(.cs)# Раздел «Внешний доступ»
│   ├── PoliciesView.axaml(.cs)      # Раздел «Политики»
│   ├── AuditView.axaml(.cs)         # Раздел «Аудит»
│   └── SettingsView.axaml(.cs)      # Раздел «Настройки»
├── App.axaml(.cs)                   # Точка входа приложения
├── Program.cs                       # Main-метод, конфигурация Avalonia
└── DataGuard.UI.csproj              # Файл проекта
```

### Паттерн

- **MVVM**: Все View привязаны к ViewModel через `x:DataType` (compiled bindings).
- **Code-behind**: Минимальный — только `InitializeComponent()` и обработка перетаскивания окна.
- **Навигация**: Через `NavigateCommand` в `MainWindowViewModel` — переключение булевых флагов `Show*`.
- **Стили**: Глобальные стили в `AppStyles.axaml` с CSS-подобными классами (`primary`, `secondary`, `danger`, `card`, `badge-green` и т.д.).

---

## Точка входа

**Файл:** `Program.cs`

```csharp
public static void Main(string[] args) => BuildAvaloniaApp()
    .StartWithClassicDesktopLifetime(args);
```

**Файл:** `App.axaml.cs`

При инициализации создаёт `MainWindow` с `MainWindowViewModel` в качестве `DataContext`:

```csharp
desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
```

---

## Экраны и элементы интерфейса

### 1. Главное окно (MainWindow)

**Файл:** `Views/MainWindow.axaml`
**ViewModel:** `MainWindowViewModel.cs`
**Code-behind:** `MainWindow.axaml.cs` (обработка перетаскивания окна)

#### Структура визуального дерева

```
Window (1280×800, тёмная тема)
├── Grid (Row 0: Title Bar, Row 1: Content)
│   ├── Title Bar (видим только при IsAuthenticated)
│   │   ├── Логотип "🛡" + "DataGuard" + название компании
│   │   └── Кнопки: — (Minimize), ⊡ (Maximize), ✕ (Close)
│   │
│   ├── Content
│   │   ├── Панель аутентификации (IsVisible по флагам ShowLogin/ShowRegister/ShowSetup)
│   │   │   ├── LoginView
│   │   │   ├── RegisterView
│   │   │   └── SetupCompanyView
│   │   │
│   │   └── Основная оболочка (IsVisible = IsAuthenticated)
│   │       ├── Sidebar (220px, левая панель)
│   │       │   ├── Заголовок: "DataGuard" + статус "Online" + имя пользователя
│   │       │   ├── Навигация: Файлы, Мессенджер, Внешний доступ, Политики, Аудит, Настройки
│   │       │   └── Футер: прогресс-бар хранилища + кнопка "Выйти"
│   │       │
│   │       └── Content Area (правая часть)
│   │           ├── FilesView
│   │           ├── MessengerView
│   │           ├── ExternalAccessView
│   │           ├── PoliciesView
│   │           ├── AuditView
│   │           └── SettingsView
│   │
│   └── Toast-уведомления (правый нижний угол)
```

#### Свойства ViewModel (MainWindowViewModel)

| Свойство | Тип | Описание |
|---|---|---|
| `ShowLogin` | `bool` | Показывать экран входа |
| `ShowRegister` | `bool` | Показывать экран регистрации |
| `ShowSetup` | `bool` | Показывать экран создания компании |
| `IsAuthenticated` | `bool` | Пользователь аутентицирован |
| `CurrentUserName` | `string` | Имя текущего пользователя |
| `CurrentCompanyName` | `string` | Название компании |
| `StorageUsed` | `string` | Текст использования хранилища |
| `StoragePercent` | `double` | Процент заполнения хранилища |
| `ShowFiles` | `bool` | Показывать раздел «Файлы» |
| `ShowMessenger` | `bool` | Показывать раздел «Мессенджер» |
| `ShowExternalAccess` | `bool` | Показывать раздел «Внешний доступ» |
| `ShowPolicies` | `bool` | Показывать раздел «Политики» |
| `ShowAudit` | `bool` | Показывать раздел «Аудит» |
| `ShowSettings` | `bool` | Показывать раздел «Настройки» |
| `Notifications` | `ObservableCollection<NotificationItem>` | Список toast-уведомлений |

#### Команды (MainWindowViewModel)

| Команда | Метод | Описание |
|---|---|---|
| `NavigateCommand` | `Navigate(string page)` | Переключение между разделами. Параметр: `"Files"`, `"Messenger"`, `"ExternalAccess"`, `"Policies"`, `"Audit"`, `"Settings"` |
| `LogoutCommand` | `Logout()` | Выход из системы, сброс состояния |
| `MinimizeCommand` | `Minimize()` | Сворачивание окна |
| `MaximizeCommand` | `Maximize()` | Переключение максимизации окна |
| `CloseCommand` | `Close()` | Закрытие окна |
| `DismissNotificationCommand` | `DismissNotification(NotificationItem n)` | Удаление уведомления из списка |

#### События между ViewModel

- `LoginVM.LoginSucceeded` → `OnLoginSucceeded()` — переход к основному экрану
- `LoginVM.GoToRegister` → показ `RegisterView`
- `LoginVM.GoToSetup` → показ `SetupCompanyView`
- `RegisterVM.RegisterSucceeded` → возврат к `LoginView`
- `RegisterVM.GoBack` → возврат к `LoginView`
- `SetupVM.SetupSucceeded` → возврат к `LoginView`
- `SetupVM.GoBack` → возврат к `LoginView`

---

### 2. Экран входа (LoginView)

**Файл:** `Views/LoginView.axaml`
**ViewModel:** `LoginViewModel` (в `AuthViewModels.cs`)

#### Элементы интерфейса

| Элемент | Тип | Привязка | Описание |
|---|---|---|---|
| Логотип | `Border` + `TextBlock` | — | Иконка 🛡️ + название "DataGuard" |
| Подзаголовок | `TextBlock` | — | "Защищённый корпоративный документооборот" |
| Блок ошибки | `Border` | `IsVisible="{Binding HasError}"` | Красный блок с сообщением об ошибке |
| Сообщение ошибки | `TextBlock` | `Text="{Binding ErrorMessage}"` | Текст ошибки |
| Поле пароля | `TextBox` | `Text="{Binding Password}"`, `PasswordChar="●"` | Ввод пароля |
| Демо-подсказка | `Border` | — | "Демо: введите пароль demo" |
| Кнопка входа | `Button` (primary) | `Command="{Binding LoginCommand}"` | Вызов аутентификации |
| Индикатор загрузки | `StackPanel` | `IsVisible="{Binding IsLoading}"` | "Подключение к серверу..." |
| Кнопка регистрации | `Button` (transparent) | `Command="{Binding NavigateToRegisterCommand}"` | Переход к регистрации |
| Кнопка создания компании | `Button` (transparent) | `Command="{Binding NavigateToSetupCommand}"` | Переход к созданию компании |

#### Свойства ViewModel (LoginViewModel)

| Свойство | Тип | Описание |
|---|---|---|
| `Password` | `string` | Введённый пароль |
| `ErrorMessage` | `string` | Текст ошибки |
| `HasError` | `bool` | Флаг видимости ошибки |
| `IsLoading` | `bool` | Флаг загрузки |

#### Команды (LoginViewModel)

| Команда | Метод | Описание |
|---|---|---|
| `LoginCommand` | `Login()` | Проверка пароля. Демо: пароль `"demo"` вызывает `LoginSucceeded`. Иначе — ошибка |
| `NavigateToRegisterCommand` | `NavigateToRegister()` | Вызывает событие `GoToRegister` |
| `NavigateToSetupCommand` | `NavigateToSetup()` | Вызывает событие `GoToSetup` |

#### Обработка действия

**Метод `Login()`** (строки 22–56 `AuthViewModels.cs`):
1. Проверяет что пароль не пустой
2. Устанавливает `IsLoading = true`
3. Делает задержку 800мс (симуляция сети)
4. Если пароль == `"demo"` — вызывает `LoginSucceeded?.Invoke("Александр И.", "ООО DataGuard")`
5. Иначе — показывает ошибку "Неверный пароль"
6. **TODO**: заменить на реальный gRPC-вызов

---

### 3. Экран регистрации (RegisterView)

**Файл:** `Views/RegisterView.axaml`
**ViewModel:** `RegisterViewModel` (в `AuthViewModels.cs`)

#### Элементы интерфейса

| Элемент | Тип | Привязка | Описание |
|---|---|---|---|
| Заголовок | `TextBlock` | — | "Регистрация" |
| Подзаголовок | `TextBlock` | — | "Введите код от администратора компании" |
| Блок ошибки | `Border` | `IsVisible="{Binding HasError}"` | Сообщение об ошибке |
| Блок успеха | `Border` | `IsVisible="{Binding SuccessVisible}"` | "Регистрация прошла успешно!" |
| Поле кода | `TextBox` | `Text="{Binding RegistrationCode}"`, `MaxLength="12"` | Код регистрации (12 символов) |
| Поле пароля | `TextBox` | `Text="{Binding Password}"`, `PasswordChar="●"` | Пароль |
| Индикаторы пароля | `StackPanel` | `HasMinLength`, `HasUpperCase`, `HasDigit`, `HasSpecial` | Визуальные индикаторы требований к паролю |
| Поле подтверждения | `TextBox` | `Text="{Binding PasswordConfirm}"`, `PasswordChar="●"` | Подтверждение пароля |
| Кнопка «Назад» | `Button` (secondary) | `Command="{Binding BackCommand}"` | Возврат к входу |
| Кнопка регистрации | `Button` (primary) | `Command="{Binding RegisterCommand}"` | Вызов регистрации |

#### Свойства ViewModel (RegisterViewModel)

| Свойство | Тип | Описание |
|---|---|---|
| `RegistrationCode` | `string` | Код регистрации |
| `Password` | `string` | Пароль |
| `PasswordConfirm` | `string` | Подтверждение пароля |
| `ErrorMessage` | `string` | Текст ошибки |
| `HasError` | `bool` | Флаг ошибки |
| `SuccessVisible` | `bool` | Флаг успешной регистрации |
| `IsLoading` | `bool` | Флаг загрузки |
| `HasUpperCase` | `bool` | Есть заглавная буква |
| `HasLowerCase` | `bool` | Есть строчная буква |
| `HasDigit` | `bool` | Есть цифра |
| `HasSpecial` | `bool` | Есть спецсимвол |
| `HasMinLength` | `bool` | Минимум 8 символов |

#### Команды (RegisterViewModel)

| Команда | Метод | Описание |
|---|---|---|
| `RegisterCommand` | `Register()` | Валидация → задержка 1000мс → `SuccessVisible = true` → задержка 2000мс → `RegisterSucceeded`. **TODO**: gRPC-вызов |
| `BackCommand` | `Back()` | Вызывает событие `GoBack` |

#### Обработка действия

**Метод `Register()`** (строки 95–133 `AuthViewModels.cs`):
1. Валидация кода (12 буквенно-цифровых символов)
2. Валидация совпадения паролей
3. Валидация требований к паролю (верхний/нижний регистр, цифра, спецсимвол, длина)
4. Задержка 1000мс (симуляция)
5. Показывает сообщение об успехе, через 2 секунды вызывает `RegisterSucceeded`

---

### 4. Экран создания компании (SetupCompanyView)

**Файл:** `Views/SetupCompanyView.axaml`
**ViewModel:** `SetupCompanyViewModel` (в `AuthViewModels.cs`)

#### Элементы интерфейса

| Элемент | Тип | Привязка | Описание |
|---|---|---|---|
| Иконка | `TextBlock` | — | "🏢" |
| Заголовок | `TextBlock` | — | "Создание компании" |
| Блок ошибки | `Border` | `IsVisible="{Binding HasError}"` | Сообщение об ошибке |
| Блок результата | `Border` | `IsVisible="{Binding ShowResult}"` | "Компания создана!" + код регистрации + кнопка "Перейти к входу" |
| Поле мастер-ключа | `TextBox` | `Text="{Binding MasterKey}"`, `PasswordChar="●"` | Мастер-ключ администратора |
| Поле названия | `TextBox` | `Text="{Binding CompanyName}"` | Название компании |
| Поле email | `TextBox` | `Text="{Binding CompanyEmail}"` | Email компании |
| Кнопка «Назад» | `Button` (secondary) | `Command="{Binding BackCommand}"` | Возврат к входу |
| Кнопка создания | `Button` (primary) | `Command="{Binding CreateCompanyCommand}"` | Создание компании |

#### Свойства ViewModel (SetupCompanyViewModel)

| Свойство | Тип | Описание |
|---|---|---|
| `MasterKey` | `string` | Мастер-ключ |
| `CompanyName` | `string` | Название компании |
| `CompanyEmail` | `string` | Email компании |
| `ErrorMessage` | `string` | Текст ошибки |
| `HasError` | `bool` | Флаг ошибки |
| `IsLoading` | `bool` | Флаг загрузки |
| `RegistrationCode` | `string` | Сгенерированный код регистрации |
| `ShowResult` | `bool` | Показать результат |

#### Команды (SetupCompanyViewModel)

| Команда | Метод | Описание |
|---|---|---|
| `CreateCompanyCommand` | `CreateCompany()` | Валидация → задержка 1000мс → `RegistrationCode = "ABC123DEF456"` → `ShowResult = true`. **TODO**: gRPC-вызов |
| `BackCommand` | `Back()` | Вызывает событие `GoBack` |
| `DoneCommand` | `Done()` | Вызывает событие `SetupSucceeded` (возврат к входу) |

---

### 5. Раздел «Файлы» (FilesView)

**Файл:** `Views/FilesView.axaml`
**ViewModel:** `FilesViewModel.cs`

#### Структура интерфейса

```
Grid (3 колонки: 260px | * | 300px)
├── Левая панель: Дерево папок
│   ├── Заголовок "Файлы"
│   ├── Поле поиска
│   ├── Кнопки "+ Загрузить" и "📁" (новая папка)
│   └── ItemsControl: FolderTree (дерево папок)
│
├── Центральная панель: Список файлов
│   ├── Путь (Breadcrumb)
│   ├── Кнопки сортировки: "Тип", "Автор", "Дата"
│   ├── Заголовки таблицы: Название | Размер | Владелец | Изменён | Доступ
│   └── ItemsControl: FilteredFiles (список файлов)
│
└── Правая панель (контекстная, 300px)
    ├── Панель свойств файла
    │   ├── Иконка + имя файла
    │   ├── Свойства: Размер, Владелец, Изменён, Доступ
    │   └── Кнопки: "Поделиться", "Чат по документу", "История действий"
    │
    └── Панель создания ссылки
        ├── Тип ссылки (ComboBox: Внутренняя / Гостевая)
        ├── Срок действия (ComboBox: 1 час — Самоуничтожение)
        ├── Права (CheckBox: Скачивание, Водяной знак)
        ├── Часы доступа (два TextBox: 10:00 — 18:00)
        ├── Лимит попыток (TextBox)
        ├── Кнопка "Сгенерировать ссылку"
        └── Результат: ссылка + кнопка "Копировать"
```

#### Свойства ViewModel (FilesViewModel)

| Свойство | Тип | Описание |
|---|---|---|
| `SearchText` | `string` | Текст поиска (фильтрация списка) |
| `SortColumn` | `string` | Колонка сортировки |
| `SortAscending` | `bool` | Направление сортировки |
| `SelectedFile` | `FileItem?` | Выбранный файл |
| `SelectedCount` | `int` | Количество выбранных |
| `CurrentPath` | `string` | Текущий путь |
| `ShowRightPanel` | `bool` | Видимость правой панели |
| `ShowSharePanel` | `bool` | Видимость панели шаринга |
| `ShowPropertiesPanel` | `bool` | Видимость панели свойств |
| `ShowPreview` | `bool` | Видимость предпросмотра |
| `ShowContextChat` | `bool` | Видимость контекстного чата |
| `ShowRenameInput` | `bool` | Видимость поля переименования |
| `RenameValue` | `string` | Новое имя файла |
| `PreviewTitle` | `string` | Заголовок предпросмотра |
| `PreviewType` | `string` | Тип предпросмотра (image/pdf/text/office/unsupported) |
| `PreviewContent` | `string` | Содержимое предпросмотра |
| `LinkTypeIndex` | `int` | Индекс типа ссылки (0=internal, 1=guest) |
| `ExpiryIndex` | `int` | Индекс срока действия |
| `SelfDestruct` | `bool` | Самоуничтожение |
| `AllowDownload` | `bool` | Разрешить скачивание |
| `Watermark` | `bool` | Водяной знак |
| `WatermarkText` | `string` | Текст водяного знака |
| `TimeFrom` | `string` | Начало окна доступа |
| `TimeTo` | `string` | Конец окна доступа |
| `TimeRestrict` | `bool` | Ограничение по времени |
| `AttemptLimit` | `string` | Лимит попыток |
| `IpRestrict` | `string` | IP-ограничение |
| `GeneratedLink` | `string` | Сгенерированная ссылка |
| `LinkGenerated` | `bool` | Ссылка создана |
| `ChatInput` | `string` | Ввод сообщения чата |
| `ChatMessages` | `ObservableCollection<ChatMessage>` | Сообщения контекстного чата |
| `AccessRights` | `ObservableCollection<AccessRight>` | Список прав доступа |
| `AddUserSearch` | `string` | Поиск пользователя для добавления |
| `FolderTree` | `ObservableCollection<FolderNode>` | Дерево папок |
| `Files` | `ObservableCollection<FileItem>` | Все файлы |
| `FilteredFiles` | `ObservableCollection<FileItem>` | Отфильтрованные файлы |

#### Команды (FilesViewModel)

| Команда | Метод | Описание |
|---|---|---|
| `SelectFileCommand` | `SelectFile(FileItem? file)` | Выбор файла → показ правой панели со свойствами |
| `OpenPreviewCommand` | `OpenPreview(FileItem? file)` | Открытие предпросмотра файла (определение типа по расширению) |
| `ClosePreviewCommand` | `ClosePreview()` | Закрытие предпросмотра |
| `OpenShareCommand` | `OpenShare()` | Переключение на панель создания ссылки |
| `BackToPropertiesCommand` | `BackToProperties()` | Возврат к панели свойств |
| `GenerateLinkCommand` | `GenerateLink()` | Генерация ссылки. Блокирует шаринг конфиденциальных файлов для гостевых ссылок. **TODO**: gRPC-вызов |
| `CopyLinkCommand` | `CopyLink()` | Копирование ссылки в буфер обмена (пустая реализация) |
| `ClosePanelCommand` | `ClosePanel()` | Закрытие правой панели |
| `OpenContextChatCommand` | `OpenContextChat()` | Открытие контекстного чата по документу |
| `CloseContextChatCommand` | `CloseContextChat()` | Закрытие контекстного чата |
| `SendChatMessageCommand` | `SendChatMessage()` | Отправка сообщения в контекстный чат |
| `StartRenameCommand` | `StartRename()` | Начало переименования файла |
| `ConfirmRenameCommand` | `ConfirmRename()` | Подтверждение переименования |
| `CancelRenameCommand` | `CancelRename()` | Отмена переименования |
| `SortByCommand` | `SortBy(string col)` | Сортировка по колонке (Name/Owner/Modified/Size) |

---

### 6. Раздел «Мессенджер» (MessengerView)

**Файл:** `Views/MessengerView.axaml`
**ViewModel:** `MessengerViewModel` (в `FeatureViewModels.cs`)

#### Структура интерфейса

```
Grid (2 колонки: 280px | *)
├── Список тредов (левая панель)
│   ├── Заголовок "Мессенджер"
│   └── ItemsControl: Threads
│       ├── Аватар + Имя + Последнее сообщение + Время
│       └── Выбор треда → SelectThreadCommand
│
└── Область чата (правая панель, видна при SelectedThread != null)
    ├── Заголовок: иконка + имя треда
    │   └── Кнопки (для документальных тредов): "Одобрить", "Отклонить", "К файлу"
    ├── Сообщения (ItemsControl)
    │   ├── Системные сообщения (по центру, серый фон)
    │   └── Обычные сообщения (автор + текст + время)
    └── Поле ввода: TextBox + кнопка "Отправить"
```

#### Свойства ViewModel (MessengerViewModel)

| Свойство | Тип | Описание |
|---|---|---|
| `SelectedThread` | `ChatThread?` | Выбранный чат-тред |
| `MessageInput` | `string` | Текст вводимого сообщения |
| `Threads` | `ObservableCollection<ChatThread>` | Список тредов |

#### Команды (MessengerViewModel)

| Команда | Метод | Описание |
|---|---|---|
| `SelectThreadCommand` | `SelectThread(ChatThread thread)` | Выбор треда для отображения |
| `SendMessageCommand` | `SendMessage()` | Отправка сообщения в выбранный тред |
| `ApproveAccessCommand` | `ApproveAccess()` | Одобрение запроса доступа (добавляет системное сообщение "✅ Доступ выдан") |
| `DenyAccessCommand` | `DenyAccess()` | Отклонение запроса доступа (добавляет системное сообщение "❌ В доступе отказано") |

---

### 7. Раздел «Внешний доступ» (ExternalAccessView)

**Файл:** `Views/ExternalAccessView.axaml`
**ViewModel:** `ExternalAccessViewModel` (в `FeatureViewModels.cs`)

#### Структура интерфейса

```
Grid (2 колонки: * | 280px)
├── Основная область
│   ├── Заголовок "Внешний доступ"
│   ├── KPI-карточки: Активных контрагентов | Открытых сессий | Передано файлов | Предупреждений
│   ├── Секция "Активные ссылки"
│   │   ├── Кнопка "+ Создать ссылку"
│   │   └── Таблица: Файл | Контрагент | Истекает | Права | Загрузок | Статус
│   └── Секция "Журнал внешних действий"
│       └── Список: Время | Пользователь · Действие | Результат
│
└── Панель деталей (280px)
    ├── Имя файла + статус
    ├── Свойства: Контрагент, Истекает, Права, Создал
    └── Кнопки: "Продлить", "Изменить", "Отозвать"
```

#### Свойства ViewModel (ExternalAccessViewModel)

| Свойство | Тип | Описание |
|---|---|---|
| `SelectedLink` | `ExternalLink?` | Выбранная ссылка |
| `ShowDetail` | `bool` | Видимость панели деталей |
| `ActiveTab` | `int` | Индекс активной вкладки |
| `ActiveContractors` | `int` | Количество активных контрагентов (демо: 12) |
| `OpenSessions` | `int` | Количество открытых сессий (демо: 4) |
| `TotalTransferred` | `string` | Объём переданных файлов (демо: "2.8 ГБ") |
| `Warnings` | `int` | Количество предупреждений (демо: 1) |
| `Links` | `ObservableCollection<ExternalLink>` | Список внешних ссылок |
| `Journal` | `ObservableCollection<AuditEntry>` | Журнал внешних действий |

#### Команды (ExternalAccessViewModel)

| Команда | Метод | Описание |
|---|---|---|
| `SelectLinkCommand` | `SelectLink(ExternalLink link)` | Выбор ссылки → показ панели деталей |
| `CloseDetailCommand` | `CloseDetail()` | Закрытие панели деталей |
| `RevokeLinkCommand` | `RevokeLink()` | Отзыв ссылки (устанавливает `Status = "Отозвана"`) |
| `SelectTabCommand` | `SelectTab(int tab)` | Переключение вкладки |

---

### 8. Раздел «Политики» (PoliciesView)

**Файл:** `Views/PoliciesView.axaml`
**ViewModel:** `PoliciesViewModel` (в `FeatureViewModels.cs`)

#### Структура интерфейса

```
DockPanel
├── Заголовок: "Политики безопасности" + кнопка "+ Создать группу"
├── Вкладки: "Группы и роли" | "Шаблоны политик" | "Запреты файлов" | "Наследование"
├── Секция "Группы доступа"
│   └── ItemsControl: Groups
│       ├── Аватар + Название + Тип · N участников
│       ├── Шаблон + бейдж "Наследование"
│       └── Кнопки: ✏ (редактировать), ✕ (удалить)
├── Секция "Шаблоты политик" (3 карточки)
│   ├── "🏦 Финансы" — запрет .exe, макросов, аудит 90 дней
│   ├── "💻 IT-стандарт" — полный доступ к коду, версионность
│   └── "🚪 Гостевой" — только просмотр, срок 24ч, водяной знак
└── Секция "Запреты по типам файлов"
    ├── Бейджи: .exe, .bat, .ps1 (красные), .xlsm, .docm (жёлтые)
    └── Кнопка "+ Добавить"
```

#### Свойства ViewModel (PoliciesViewModel)

| Свойство | Тип | Описание |
|---|---|---|
| `Groups` | `ObservableCollection<PolicyGroup>` | Список групп доступа |

---

### 9. Раздел «Аудит» (AuditView)

**Файл:** `Views/AuditView.axaml`
**ViewModel:** `AuditViewModel` (в `FeatureViewModels.cs`)

#### Структура интерфейса

```
Grid (2 колонки: * | 280px)
├── Основная область
│   ├── Заголовок "Аудит" + кнопки "Экспорт", "Обновить"
│   ├── Фильтры: Пользователь | Документ | Действие (ComboBox) | Период (ComboBox)
│   ├── Быстрые отчёты: "Кто скачивал чаще", "Кто делился наружу", "Часто утекавшие файлы"
│   ├── Заголовки таблицы: Время | Пользователь | Файл | Действие | IP | Результат
│   └── ItemsControl: Filtered (лог событий)
│
└── Панель деталей (280px)
    ├── Свойства: Время, Пользователь, Действие, Объект, IP, Результат
    └── Кнопки: "К файлу", "История пользователя"
```

#### Свойства ViewModel (AuditViewModel)

| Свойство | Тип | Описание |
|---|---|---|
| `SelectedEntry` | `AuditEntry?` | Выбранная запись |
| `ShowDetail` | `bool` | Видимость панели деталей |
| `FilterUser` | `string` | Фильтр по пользователю |
| `FilterDocument` | `string` | Фильтр по документу |
| `Entries` | `ObservableCollection<AuditEntry>` | Все записи |
| `Filtered` | `ObservableCollection<AuditEntry>` | Отфильтрованные записи |

#### Команды (AuditViewModel)

| Команда | Метод | Описание |
|---|---|---|
| `SelectEntryCommand` | `SelectEntry(AuditEntry e)` | Выбор записи → показ деталей |
| `CloseDetailCommand` | `CloseDetail()` | Закрытие панели деталей |

---

### 10. Раздел «Настройки» (SettingsView)

**Файл:** `Views/SettingsView.axaml`
**ViewModel:** `SettingsViewModel` (в `FeatureViewModels.cs`)

#### Структура интерфейса

```
DockPanel
├── Заголовок "Настройки"
└── Контент (MaxWidth=640)
    ├── Карточка "Подключение"
    │   ├── Поле "Адрес сервера" (TextBox)
    │   ├── Бейдж "● Подключено"
    │   └── Кнопка "Проверить соединение"
    ├── Карточка "Безопасность"
    │   ├── CheckBox "Автоматически блокировать сессию"
    │   ├── Поле "Длительность сессии (часов)"
    │   └── Кнопка "Сменить пароль"
    ├── Карточка "О приложении"
    │   ├── "🛡️ DataGuard Beta 1.1 · GUI Client"
    │   └── ".NET 9 + Avalonia 11.2 + gRPC"
    ├── Уведомление "Настройки сохранены" (SavedOk)
    └── Кнопка "Сохранить"
```

#### Свойства ViewModel (SettingsViewModel)

| Свойство | Тип | Описание |
|---|---|---|
| `ServerAddress` | `string` | Адрес сервера (по умолчанию `https://localhost:7777`) |
| `AutoLock` | `bool` | Автоблокировка сессии |
| `SessionHours` | `int` | Длительность сессии в часах |
| `SavedOk` | `bool` | Флаг "Настройки сохранены" |

#### Команды (SettingsViewModel)

| Команда | Метод | Описание |
|---|---|---|
| `SaveCommand` | `Save()` | Сохранение настроек (задержка 300мс → `SavedOk = true` → 2500мс → `SavedOk = false`) |

---

## Модели данных

**Файл:** `Models/DataModels.cs`

| Модель | Назначение | Ключевые свойства |
|---|---|---|
| `NotificationItem` | Toast-уведомление | `Icon`, `Title`, `Message`, `Type` |
| `FileItem` | Файл/папка в списке | `Name`, `Type`, `Icon`, `Size`, `Owner`, `Modified`, `Access`, `IsSelected`, `IsConfidential`, `Extension` |
| `FolderNode` | Узел дерева папок | `Name`, `Icon`, `IsExpanded`, `IsSelected`, `Children`, `Level` |
| `AccessRight` | Право доступа | `Name`, `Avatar`, `Role`, `IsGroup` |
| `ChatMessage` | Сообщение чата | `Author`, `Content`, `Time`, `IsMe`, `IsSystem`, `IsRead` |
| `ChatThread` | Тред мессенджера | `Name`, `LastMessage`, `Time`, `UnreadCount`, `Icon`, `IsDocument`, `IsPinned`, `Messages` |
| `ExternalLink` | Внешняя ссылка | `Name`, `Target`, `Expires`, `Status`, `Permissions`, `Downloads`, `MaxDownloads`, `CreatedBy`, `HasWatermark`, `IpRestriction`, `TimeWindow` |
| `AuditEntry` | Запись аудита | `Time`, `User`, `Action`, `Target`, `Ip`, `Result`, `Device`, `Browser` |
| `PolicyGroup` | Группа политик | `Name`, `Type`, `Members`, `Template`, `Inherited`, `MemberList`, `Description` |
| `GroupMember` | Участник группы | `Name`, `Email`, `Role`, `Avatar`, `IsAdmin` |
| `PolicyTemplate` | Шаблон политики | `Name`, `Icon`, `Description`, `Color`, `TextColor`, `Rules` |

---

## gRPC-клиент

**Файл:** `Services/GrpcClientService.cs`

### Текущая реализация

```csharp
public class GrpcClientService : IDisposable
{
    private GrpcChannel? _channel;
    private readonly string _serverAddress;  // default: "https://localhost:7777"

    public Authentication.AuthenticationClient AuthClient => new(Channel);
    public CompanyManager.CompanyManagerClient CompanyClient => new(Channel);
}
```

### Сгенерированные proto-контракты

**`Protos/auth.proto`** (namespace: `DataGuard.UI.Grpc.Auth`):
- `Authentication` сервис: `Register(RegisterRequest) → RegisterResponse`, `Login(LoginRequest) → LoginResponse`

**`Protos/company_manager.proto`** (namespace: `DataGuard.UI.Grpc.CompanyManager`):
- `CompanyManager` сервис: `CreateCompany(CreateCompanyRequest) → CreateCompanyResponse`

### Текущий статус интеграции

Все вызовы к серверу в ViewModels помечены `// TODO: call gRPC ...` и используют демо-данные:

| ViewModel | Метод | Текущее поведение | Что нужно заменить |
|---|---|---|---|
| `LoginViewModel` | `Login()` | Сравнение с `"demo"` | gRPC `AuthClient.LoginAsync()` |
| `RegisterViewModel` | `Register()` | Валидация + задержка | gRPC `AuthClient.RegisterAsync()` |
| `SetupCompanyViewModel` | `CreateCompany()` | Задержка + фейковый код | gRPC `CompanyClient.CreateCompanyAsync()` |
| `FilesViewModel` | `GenerateLink()` | `Guid.NewGuid()` | gRPC `StorageClient.GenerateLinkAsync()` |
| `FilesViewModel` | `LoadDemoData()` | Хардкод файлов | gRPC `StorageClient.ListDirectoryAsync()` |

---

## Навигация

Навигация реализована через команду `NavigateCommand` в `MainWindowViewModel`:

```
NavigateCommand("Files")          → ShowFiles = true, остальные false
NavigateCommand("Messenger")      → ShowMessenger = true
NavigateCommand("ExternalAccess") → ShowExternalAccess = true
NavigateCommand("Policies")       → ShowPolicies = true
NavigateCommand("Audit")          → ShowAudit = true
NavigateCommand("Settings")       → ShowSettings = true
```

Кнопки навигации в Sidebar привязаны к `NavigateCommand` с параметром `CommandParameter`.

---

## Toast-уведомления

Управляются через `MainWindowViewModel`:

```csharp
AddNotification(string icon, string title, string message, string type = "info")
```

- Появляются в правом нижнем углу окна
- Автоматически исчезают через 6 секунд
- Могут быть удалены вручную через `DismissNotificationCommand`
- Типы: `info`, `warning`, `success`, `error`

---

## Стилизация

**Файл:** `Assets/AppStyles.axaml`

### Цветовая палитра

| Роль | Цвет |
|---|---|
| Фон базовый | `#0F1117` |
| Фон поверхности | `#161B27` |
| Фон карточки | `#1E2535` |
| Фон при наведении | `#252D3F` |
| Акцент синий | `#3B82F6` |
| Акцент зелёный | `#22C55E` |
| Акцент красный | `#EF4444` |
| Акцент жёлтый | `#F59E0B` |
| Текст основной | `#F1F5F9` |
| Текст вторичный | `#94A3B8` |
| Текст приглушённый | `#475569` |
| Граница | `#2D3748` |

### CSS-классы кнопок

| Класс | Назначение |
|---|---|
| `primary` | Основное действие (синий фон) |
| `secondary` | Вторичное действие (тёмный фон, граница) |
| `danger` | Опасное действие (красный) |
| `success` | Положительное действие (зелёный) |
| `ghost` | Прозрачная кнопка |
| `nav-btn` | Кнопка навигации в сайдбаре |

### CSS-классы карточек и бейджей

| Класс | Назначение |
|---|---|
| `card` | Карточка (тёмный фон, скругление 12px) |
| `card-sm` | Маленькая карточка (скругление 8px) |
| `badge-blue` | Синий бейдж |
| `badge-green` | Зелёный бейдж |
| `badge-red` | Красный бейдж |
| `badge-yellow` | Жёлтый бейдж |
| `kpi-card` | KPI-карточка |
| `toast` | Toast-уведомление |
| `divider` | Разделитель (1px линия) |

---

## Демо-режим

Приложение работает в полностью автономном демо-режиме:

1. **Вход**: пароль `demo`
2. **Файлы**: 8 демо-файлов в дереве папок
3. **Мессенджер**: 3 треда с демо-сообщениями
4. **Внешний доступ**: 4 демо-ссылки + 3 записи журнала
5. **Политики**: 4 демо-группы + 3 шаблона + запреты файлов
6. **Аудит**: 7 демо-записей
7. **Настройки**: адрес сервера `https://localhost:7777`

---

## Сборка и запуск

```bash
cd DataGuard.UI
dotnet restore
dotnet run
```

**Требования:** .NET 10.0 SDK или новее.
