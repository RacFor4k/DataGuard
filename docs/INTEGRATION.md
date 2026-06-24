# Client.UI ↔ Client.Engine — План интеграции

## 1. Обзор текущего состояния

На данный момент **Client.UI** и **Client.Engine** — два полностью независимых проекта без прямой связи друг с другом:

- **Client.UI** — десктопное Avalonia-приложение. Все ViewModels работают с **демо-данными** (хардкод). Вызовы к серверу помечены `// TODO: call gRPC ...`. Собственный `GrpcClientService` подключается напрямую к `https://localhost:7777` (Server.Auth).

- **Client.Engine** — gRPC-сервис (Background Worker), который сам является **промежуточным слоем** между GUI и серверами. Он принимает gRPC-вызовы от GUI, выполняет клиентскую криптографию (хеширование паролей, шифрование ключей, Argon2), и проксирует запросы к Server.Auth, Server.Storage, CompanyManager серверам. Работает через **named pipe** (`DataGuardPipe`, HTTP/2).

**Ключевая проблема**: Client.UI пытается общаться напрямую с Server.Auth (минуя Client.Engine), но при этом:
1. Не выполняет клиентскую криптографию (хеширование, шифрование ключей).
2. Использует **упрощённые proto-контракты** (пароль как `string`, нет `encrypted_key`, `password_hash`, `nonce_token`).
3. Client.Engine уже содержит **полную реализацию** всех криптографических операций и 21 метода хранилища.

---

## 2. Архитектура "как есть"

```
┌─────────────────────────────────────────────────────────┐
│                  Client.UI (Avalonia)                │
│                                                         │
│  ViewModels → GrpcClientService ──→ https://localhost:  │
│  .Login()        (Auth + Company)    7777 (Server.Auth) │
│  .Register()         │                                   │
│  .CreateCompany()    │               ⛔ НЕПРАВИЛЬНО      │
│                      │                                   │
│  Все данные —        │         Прямое подключение к      │
│  демо (хардкод)      │         Server.Auth без крипто   │
│                      │                                   │
└──────────────────────┼──────────────────────────────────┘
                       │
                       │  GrpcClientService
                       │  (прямое соединение)
                       ▼
              ┌─────────────────┐
              │   Server.Auth   │
              │   (порт 7203)   │
              └─────────────────┘

┌─────────────────────────────────────────────────────────┐
│                  Client.Engine (gRPC сервис)            │
│                                                         │
│  Named Pipe "DataGuardPipe" (HTTP/2)                    │
│       │                                                 │
│  ┌────┴──────────────────────────────────────────┐      │
│  │ AuthenticationService (gRPC сервис)           │      │
│  │  .Register(registration_code, password)        │      │
│  │  .Login(account_id, password)                 │      │
│  │  → Валидация → Argon2 → Шифрование → JWT     │      │
│  ├────────────────────────────────────────────────┤      │
│  │ CompanyManagerService (gRPC сервис)           │      │
│  │  .CreateCompany(master_key, name, email)       │      │
│  ├────────────────────────────────────────────────┤      │
│  │ StorageClientService (IStorageService)         │      │
│  │  21 метод: Upload, Download, CRUD, Links...   │      │
│  └────┬───────────────────────────────────────────┘      │
│       │                                                  │
│       │  gRPC клиенты к серверам                         │
│       ▼                                                  │
│  ┌──────────┐  ┌──────────┐  ┌────────────────┐         │
│  │Server.   │  │Company   │  │Server.Storage  │         │
│  │Auth      │  │Manager   │  │(порт 8081)     │         │
│  │(7203)    │  │(7203)    │  │                │         │
│  └──────────┘  └──────────┘  └────────────────┘         │
│  + PostgreSQL + Redis + MinIO (через docker-compose)     │
└─────────────────────────────────────────────────────────┘
```

## 3. Архитектура "как должно быть"

```
┌─────────────────────────────────────────────────────────┐
│                  Client.UI (Avalonia)                │
│                                                         │
│  ViewModels → ClientEngineGrpcClient                   │
│  .Login()        (Named Pipe "DataGuardPipe")           │
│  .Register()         │                                   │
│  .CreateCompany()    │         ТОЛЬКО gRPC-вызовы      │
│  .UploadFile()       │         к Client.Engine           │
│  .ListDirectory()    │                                   │
│  .GetAllStorageOps() │         Никакой криптографии     │
│                      │         на стороне UI            │
└──────────────────────┼──────────────────────────────────┘
                       │
                       │  Named Pipe (HTTP/2)
                       │  "DataGuardPipe"
                       ▼
              ┌─────────────────────┐
              │   Client.Engine     │
              │                     │
              │ • Валидация паролей │
              │ • Argon2id хешир.   │
              │ • AES шифрование    │
              │ • JWT управление    │
              │ • Nonce защита      │
              │ • 21 storage методов│
              │                     │
              │   → Server.Auth     │
              │   → CompanyManager  │
              │   → Server.Storage  │
              │   → SecurityService │
              └─────────────────────┘
```

---

## 4. Детальный анализ несоответствий

### 4.1. Протокольные несоответствия (Proto)

#### Auth: Нехватка critical-полей в UI proto

**UI `Protos/auth.proto`** (текущий, упрощённый):
```protobuf
service Authentication {
    rpc Register (RegisterRequest) returns (RegisterResponse);
    rpc Login (LoginRequest) returns (LoginResponse);
}

message RegisterRequest {
    string registration_code = 1;
    string password = 2;          // ← plaintext! НЕПРАВИЛЬНО
}
message LoginRequest {
    string password = 1;          // ← plaintext! НЕПРАВИЛЬНО
}
```

**Client.Engine `Contracts/Protos/Client/auth.proto`** (правильный, полный):
```protobuf
service Authentication {
    rpc Register (RegisterRequest) returns (RegisterResponse);
    rpc Login (LoginRequest) returns (LoginResponse);
}

message RegisterRequest {
    string registration_code = 1;
    string password = 2;
    optional string company_public_key_pem = 3;
}
message RegisterResponse {
    int32 status = 1;
    string message = 2;
    // ← НЕТ user_id, email, JWT токенов в клиентском контракте
    // (Engine сохраняет сам после внутреннего вызова к Server.Auth)
}

message LoginRequest {
    string account_id = 1;       // ← НЕТ в UI proto!
    string password = 2;
}
message LoginResponse {
    int32 status = 1;
    string message = 2;
    // ← НЕТ encrypted_key (Engine расшифровывает сам)
}
```

| Поле | UI proto | Client.Engine proto | Проблема |
|---|---|---|---|
| `LoginRequest.account_id` | ❌ Нет | ✅ Есть | UI отправляет только пароль, Engine требует `account_id` |
| `RegisterRequest.company_public_key_pem` | ❌ Нет | ✅ Есть (optional) | UI не может передать публичный ключ компании |
| `RegisterResponse.user_id` | ❌ Нет | ❌ Нет | Engine возвращает статус 200, но `user_id` сохраняет в локальную SQLite |

**Вывод**: UI proto-контракты **не совместимы** с сервисами Client.Engine. Требуется замена UI proto на клиентские контракты из `Contracts/Protos/Client/`.

#### CompanyManager: Промежуток в данных

**UI `Protos/company_manager.proto`** — совместим с `Contracts/Protos/Client/company_manager.proto` по структуре сообщений `CreateCompany*`, но UI отправляет `master_key` как plaintext `string`, а Client.Engine ожидает `master_key` как Base64-сериализованный массив байтов (Engine декодирует `Convert.FromBase64String(request.MasterKey)` перед хешированием).

**Несоответствие**: UI передаёт `master_key` в plaintext, а Engine ожидает Base64-закодированную строку.

### 4.2. Отсутствующие сервисы в UI

| Сервис в Client.Engine | Реализован в UI | Что отсутствует |
|---|---|---|
| `AuthenticationService.Register()` | ⚠️ Пустая (TODO) | Валидация, отправка через Named Pipe |
| `AuthenticationService.Login()` | ⚠️ Демо-сравнение с "demo" | Отправка `account_id` + `password` через Named Pipe |
| `CompanyManagerService.CreateCompany()` | ⚠️ Пустая (TODO) | Отправка `master_key` (Base64) через Named Pipe |
| `StorageClientService` (21 метод) | ❌ Полностью отсутствует | UI не имеет доступа ни к одной операции хранилища |
| — | ❌ | UI не имеет `IStorageService` интерфейса |
| — | ❌ | UI не имеет моделей `StorageModels.cs` (StorageUploadResult и др.) |

### 4.3. Отсутствующие proto-контракты в UI

| Proto файл | Назначение | Есть в UI | Есть в Engine |
|---|---|---|---|
| `auth.proto` | Аутентификация (Server) | ✅ (упрощённый) | ✅ |
| `Client/auth.proto` | Аутентификация (Client→Engine) | ❌ | ✅ |
| `company_manager.proto` | Управление компанией (Server) | ✅ (упрощённый) | ✅ |
| `Client/company_manager.proto` | Управление компанией (Client→Engine) | ❌ | ✅ |
| `security.proto` | Nonce, Salt | ❌ | ✅ |
| `storage.proto` | Операции с хранилищем (21 метод) | ❌ | ✅ |

### 4.4. Отсутствующая функциональность в ViewModels

#### LoginViewModel
- **Текущее**: Сравнение пароля с `"demo"` (строка 37 `AuthViewModels.cs`).
- **Нужное**: gRPC-вызов `AuthenticationService.Login(account_id, password)` через Named Pipe.
- **Нет**: Поле `AccountId` в ViewModel. LoginView запрашивает только пароль, но Engine требует `account_id`.

#### RegisterViewModel
- **Текущее**: Валидация + задержка 1000мс + `SuccessVisible = true`.
- **Нужное**: gRPC-вызов `AuthenticationService.Register(registration_code, password)`.
- **Нет**: Поле `CompanyPublicKeyPem` (optional в контракте).

#### SetupCompanyViewModel
- **Текущее**: Валидация + задержка 1000мс + фейковый код.
- **Нужное**: gRPC-вызов `CompanyManagerService.CreateCompany(master_key, company_name, company_email)` где `master_key` передаётся в Base64.

#### FilesViewModel
- **Текущее**: `LoadDemoData()` — 8 хардкод-файлов.
- **Нужное**: Полный доступ к хранилищу через `IStorageService` (21 метод):
  - `ListDirectoryAsync()` — загрузка дерева файлов
  - `UploadFileAsync()` — загрузка файла (+ кнопка "Загрузить" в UI)
  - `GetFileAsync()` — скачивание/предпросмотр файла
  - `DeleteFileAsync()` — удаление
  - `MoveFileAsync()`, `CopyFileAsync()`, `RenameFileAsync()` — операции с файлами
  - `GetMetadataAsync()`, `UpdateMetadataAsync()` — свойства файла
  - `NewDirectoryAsync()` — создание папки (+ кнопка "📁" в UI)
  - `RenameDirectoryAsync()`, `DeleteDirectoryAsync()`, `MoveDirectoryAsync()`, `CopyDirectoryAsync()` — операции с папками
  - `GenerateLinkAsync()`, `GenerateDirectLinkAsync()` — создание ссылок (+ форма в UI)
  - `DownloadFileViaLinkAsync()`, `DownloadFileViaDirectLinkAsync()` — скачивание по ссылке

#### MessengerViewModel
- **Текущее**: 3 хардкод-треда.
- **Нужное**: Интеграция с сервисом мессенджера (в текущем Client.Engine мессенджер **не реализован**). Это отдельная задача.

#### ExternalAccessViewModel
- **Текущее**: 4 хардкод-ссылки + 3 записи журнала.
- **Нужное**:
  - Список через `ListDirectoryAsync()` + данные из `StorageFileAccess` (нет в Engine)
  - Генерация через `StorageClientService.GenerateLinkAsync()`
  - Отзыв ссылки (нет метода в Engine)

#### AuditViewModel
- **Текущее**: 7 хардкод-записей.
- **Нужное**: Метод получения лога аудита (в текущем Engine **не реализован**).

#### PoliciesViewModel
- **Текущее**: 4 хардкод-группы + 3 шаблона.
- **Нужное**: gRPC-методы управления группами/политиками (в текущем Engine **не реализованы**).

---

## 5. План интеграции

### Этап 1: Подключение к Named Pipe

**Задача**: Настроить gRPC-канал от UI к Client.Engine через Named Pipe.

#### 1.1. Добавить NuGet-пакеты в Client.UI.csproj

```xml
<!-- Уже есть: Grpc.Net.Client, Google.Protobuf, Grpc.Tools -->
<!-- Нужно добавить reference на Contracts проект -->
<ProjectReference Include="..\Contracts\Contracts.csproj" />
```

#### 1.2. Переписать GrpcClientService.cs

**Текущее**: HTTP-подключение `https://localhost:7777`
**Нужное**: Named Pipe `"DataGuardPipe"` (HTTP/2)

```csharp
// Новый ClientEngineGrpcClient.cs
using Grpc.Net.Client;
using Contracts.Protos.Client.Auth;
using Contracts.Protos.Client.CompanyManager;

namespace Client.UI.Services;

public class ClientEngineGrpcClient : IDisposable
{
    private GrpcChannel? _channel;

    private GrpcChannel Channel => _channel ??= GrpcChannel.ForAddress(
        "http://localhost", // Named Pipe не требует реального URL
        new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, cancellationToken) =>
                {
                    var socket = new System.Net.Sockets.Socket(
                        System.Net.Sockets.AddressFamily.Unix,
                        System.Net.Sockets.SocketType.Stream,
                        System.Net.Sockets.ProtocolType.Unspecified);
                    await socket.ConnectAsync(
                        new System.Net.Sockets.UnixDomainSocketEndPoint(
                            @"\\.\pipe\DataGuardPipe"), cancellationToken);
                    return new System.Net.Sockets.NetworkStream(socket, true);
                }
            }
        });

    // Auth service
    public Authentication.AuthenticationClient AuthClient =>
        new Authentication.AuthenticationClient(Channel);

    // Company manager service
    public CompanyManager.CompanyManagerClient CompanyClient =>
        new CompanyManager.CompanyManagerClient(Channel);

    // Storage service — НЕ нужен gRPC клиент, т.к. StorageClientService
    // в Engine не предоставляет gRPC сервис для внешних клиентов.
    // Вместо этого — добавитьgRPC сервис IStorageService в Engine.
    // См. Этап 2.

    public void Dispose() => _channel?.Dispose();
}
```

#### Шаги

| # | Действие | Файл |
|---|---|---|
| 1.2.1 | Добавить `<ProjectReference Include="..\Contracts\Contracts.csproj" />` | `Client.UI.csproj` |
| 1.2.2 | Скопировать proto-файлы из `Contracts/Protos/Client/` в проект UI | `Protos/Client/auth.proto`, `Protos/Client/company_manager.proto` |
| 1.2.3 | Обновить `.csproj` — заменить `Protobuf` references на клиентские | `Client.UI.csproj` |
| 1.2.4 | Создать `ClientEngineGrpcClient.cs` с Named Pipe | `Services/ClientEngineGrpcClient.cs` |
| 1.2.5 | Зарегистрировать в DI / передать в ViewModels | `App.axaml.cs` или `MainWindowViewModel` |

---

### Этап 2: Добавление gRPC-сервиса хранилища в Client.Engine

**Проблема**: `StorageClientService` в Client.Engine реализует `IStorageService`, но **не предоставляет gRPC-сервис** для внешних клиентов. UI не может вызывать методы хранилища.

#### 2.1. Создать StorageGrpcService в Client.Engine

Необходимо создать новый gRPC-сервис, который будет обёрткой над `IStorageService`:

```csharp
// Client.Engine/Services/StorageGrpcService.cs
public class StorageGrpcService : Contracts.Protos.Client.Storage.StorageServiceBase
{
    private readonly IStorageService _storageService;

    public StorageGrpcService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    // 21 метод, проксирующий вызовы к _storageService
    public async Task<StorageListResultResponse> ListDirectory(
        ListDirectoryRequest request, ServerCallContext context)
    {
        var result = await _storageService.ListDirectoryAsync(
            Guid.Parse(request.DirectoryId), request.Recursive);
        // ... маппинг в protobuf
    }
    // ... остальные 20 методов
}
```

#### 2.2. Создать клиентский storage.proto

Нужен файл `Contracts/Protos/Client/storage.proto` с 21 методом + всеми сообщениями (mirroring `storage.proto`).

#### 2.3. Зарегистрировать сервис в Program.cs Client.Engine

```csharp
// Client.Engine/Program.cs — добавить:
builder.Services.AddScoped<StorageGrpcService>();
// ...
app.MapGrpcService<StorageGrpcService>();
```

#### Шаги

| # | Действие | Файл |
|---|---|---|
| 2.1 | Создать `Contracts/Protos/Client/storage.proto` | `Contracts/Protos/Client/storage.proto` |
| 2.2 | Сгенерировать C# классы из proto | `Contracts.csproj` |
| 2.3 | Создать `StorageGrpcService.cs` в Engine | `Client.Engine/Services/StorageGrpcService.cs` |
| 2.4 | Зарегистрировать в `Program.cs` | `Client.Engine/Program.cs` |
| 2.5 | Добавить gRPC клиент в `ClientEngineGrpcClient.cs` | `Client.UI/Services/ClientEngineGrpcClient.cs` |
| 2.6 | Скопировать `StorageModels.cs` из Engine в UI | `Client.UI/Models/StorageModels.cs` |

---

### Этап 3: Перевод Auth-ViewModel на реальные gRPC-вызовы

#### 3.1. LoginViewModel

**Изменения:**

1. Добавить поле `AccountId` в ViewModel и в LoginView.axaml (новое поле ввода).
2. Заменить демо-логику на gRPC-вызов:

```csharp
[RelayCommand]
private async Task Login()
{
    HasError = false;
    if (string.IsNullOrWhiteSpace(Password))
    {
        ErrorMessage = "Введите пароль";
        HasError = true;
        return;
    }
    IsLoading = true;
    try
    {
        // Если пустой AccountId — использовать сохранённый из локального хранилища
        var accountId = string.IsNullOrWhiteSpace(AccountId)
            ? _savedAccountId : AccountId;

        var response = await _grpcClient.AuthClient.LoginAsync(
            new LoginRequest { AccountId = accountId, Password = Password });

        if (response.Status == 200)
        {
            // JWT токен уже сохранён в Client.Engine (SQLite)
            LoginSucceeded?.Invoke("Пользователь", "Компания");
        }
        else
        {
            ErrorMessage = response.Message;
            HasError = true;
        }
    }
    catch (RpcException ex)
    {
        ErrorMessage = $"Ошибка подключения к фоновой службе: {ex.Status.Detail}";
        HasError = true;
    }
    finally
    {
        IsLoading = false;
    }
}
```

**Файлы для изменения:**

| Файл | Что изменить |
|---|---|
| `Views/LoginView.axaml` | Добавить `TextBox` для `AccountId` (или `UserId`) |
| `ViewModels/AuthViewModels.cs` | Добавить свойство `AccountId`, внедрить `IClientEngineGrpcClient`, заменить `Login()` |
| `Services/ClientEngineGrpcClient.cs` | Добавить Auth gRPC-клиент |

#### 3.2. RegisterViewModel

```csharp
[RelayCommand]
private async Task Register()
{
    // ... существующая валидация ...
    try
    {
        var response = await _grpcClient.AuthClient.RegisterAsync(
            new RegisterRequest
            {
                RegistrationCode = RegistrationCode,
                Password = Password
            });

        if (response.Status == 200)
        {
            SuccessVisible = true;
            await Task.Delay(2000);
            RegisterSucceeded?.Invoke();
        }
        else
        {
            ErrorMessage = response.Message;
            HasError = true;
        }
    }
    catch (RpcException ex) { /* ... */ }
}
```

#### 3.3. SetupCompanyViewModel

```csharp
[RelayCommand]
private async Task CreateCompany()
{
    // ... существующая валидация ...
    try
    {
        // MasterKey кодируется в Base64 для передачи
        var masterKeyBase64 = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(MasterKey));

        var response = await _grpcClient.CompanyClient.CreateCompanyAsync(
            new CreateCompanyRequest
            {
                CompanyName = CompanyName,
                CompanyEmail = CompanyEmail,
                MasterKey = masterKeyBase64
            });

        if (response.Status == 200)
        {
            RegistrationCode = response.RegistrationCode;
            ShowResult = true;
        }
        else
        {
            ErrorMessage = response.Message;
            HasError = true;
        }
    }
    catch (RpcException ex) { /* ... */ }
}
```

#### Шаги

| # | Действие | Файл |
|---|---|---|
| 3.1 | Добавить `AccountId` в `LoginViewModel` + `LoginView.axaml` | `AuthViewModels.cs`, `LoginView.axaml` |
| 3.2 | Заменить `Login()` на gRPC-вызов | `AuthViewModels.cs` |
| 3.3 | Заменить `Register()` на gRPC-вызов | `AuthViewModels.cs` |
| 3.4 | Заменить `CreateCompany()` на gRPC-вызов | `AuthViewModels.cs` |
| 3.5 | Добавить обработку `RpcException` во все 3 метода | `AuthViewModels.cs` |
| 3.6 | Внедрить `ClientEngineGrpcClient` через DI или конструктор | `MainWindowViewModel.cs` |

---

### Этап 4: Перевод FilesViewModel на реальное хранилище

#### 4.1. Удалить LoadDemoData()

Заменить хардкод-данные на загрузку с сервера:

```csharp
// Новый метод — загрузка дерева папок и файлов
public async Task LoadFilesAsync(Guid directoryId)
{
    var result = await _storageClient.ListDirectoryAsync(
        new ListDirectoryRequest { DirectoryId = directoryId.ToString(), Recursive = false });

    if (result.Success)
    {
        FilteredFiles.Clear();
        foreach (var item in result.Items)
        {
            FilteredFiles.Add(new FileItem
            {
                Name = item.FileName,
                Size = FormatSize(item.Size),
                Owner = "—", // нет данных владельца в ответе
                Modified = "—",
                Access = "—"
            });
        }
    }
}
```

#### 4.2. Добавить операции с файлами

```csharp
// Загрузка файла
[RelayCommand]
private async Task UploadFile(string filePath) { /* ... */ }

// Скачивание
[RelayCommand]
private async Task DownloadFile(FileItem file) { /* ... */ }

// Удаление
[RelayCommand]
private async Task DeleteFile(FileItem file) { /* ... */ }

// Переименование
[RelayCommand]
private async Task RenameFile(object _) => await ConfirmRename();

// Копирование
[RelayCommand]
private async Task CopyFile(FileItem file) { /* ... */ }

// Перемещение
[RelayCommand]
private async Task MoveFile(FileItem file) { /* ... */ }
```

#### 4.3. Генерация ссылки через сервер

```csharp
[RelayCommand]
private async Task GenerateLink()
{
    if (SelectedFile == null) return;
    var fileGuid = Guid.Parse(SelectedFile.Id); // нужно хранить FileId

    var result = await _storageClient.GenerateLink(
        new GenerateLinkRequest
        {
            FileId = fileGuid.ToString(),
            TtlSeconds = GetSelectedTtlSeconds(),
            // Groups = ..., Users = ...
        });

    if (result.Success)
    {
        GeneratedLink = result.Link;
        LinkGenerated = true;
    }
}
```

#### Шаги

| # | Действие | Файл |
|---|---|---|
| 4.1 | Удалить `LoadDemoData()` из конструктора | `FilesViewModel.cs` |
| 4.2 | Добавить `LoadFilesAsync(Guid directoryId)` | `FilesViewModel.cs` |
| 4.3 | Добавить `UploadFile`, `DownloadFile`, `DeleteFile` | `FilesViewModel.cs` |
| 4.4 | Заменить `StartRename/ConfirmRename` на gRPC | `FilesViewModel.cs` |
| 4.5 | Добавить `CopyFile`, `MoveFile` | `FilesViewModel.cs` |
| 4.6 | Заменить `GenerateLink()` на gRPC-вызов | `FilesViewModel.cs` |
| 4.7 | Добавить `NewDirectory`, `RenameDirectory`, `DeleteDirectory` | `FilesViewModel.cs` |
| 4.8 | Обновить `FileItem` — добавить `FileId` (Guid) | `Models/DataModels.cs` |
| 4.9 | Добавить обработку ошибок (RpcException) | `FilesViewModel.cs` |
| 4.10 | Добавить индикатор загрузки (`IsLoading`) | `FilesViewModel.cs` |

---

### Этап 5: Интеграция через Shared Project (альтернативный подход)

Вместо дублирования proto и моделей можно вынести общие типы в **Shared Project** или использовать **прямую ссылку на Contracts**:

#### 5.1. Вариант A: ProjectReference на Contracts (рекомендуемый)

```
Client.UI
  └── ProjectReference → Contracts
        └── Получает:
            • Все proto-контракты (Client + Server)
            • Сгенерированные gRPC клиенты
            • Модели данных (если добавить)
```

**Плюсы**: Не нужно копировать proto. Единый источник контрактов.
**Минусы**: UI получает доступ к server-side контрактам (не критично).

#### 5.2. Вариант B: Shared Contracts

Создать `DataGuard.Shared.Contracts` проект с общими DTO и интерфейсами.

#### Шаги

| # | Действие | Файл |
|---|---|---|
| 5.1 | Добавить `<ProjectReference Include="..\Contracts\Contracts.csproj" />` | `Client.UI.csproj` |
| 5.2 | Удалить дублирующие proto из `Client.UI/Protos/` | `Protos/auth.proto`, `Protos/company_manager.proto` |
| 5.3 | Добавить `Link` на Client proto из Contracts в `.csproj` | `Client.UI.csproj` |
| 5.4 | Обновить `using` в `ClientEngineGrpcClient.cs` на namespace из Contracts | `Services/ClientEngineGrpcClient.cs` |

---

### Этап 6: Дополнительные сервисы (Mессенджер, Аудит, Политики)

Эти разделы требуют реализации gRPC-сервисов в Client.Engine, которых **ещё нет**:

#### 6.1. Мессенджер

**Статус**: В Client.Engine **не реализован** сервис мессенджера.

**Что нужно:**
1. Создать `MessengerService` в Client.Engine с методами:
   - `GetThreads()` — получение списка тредов
   - `GetMessages(threadId)` — получение сообщений
   - `SendMessage(threadId, content)` — отправка сообщения
   - `ApproveAccess(requestId)` — одобрение доступа
   - `DenyAccess(requestId)` — отклонение доступа
2. Создать `Contracts/Protos/Client/messenger.proto`
3. Подключить UI MessengerViewModel

#### 6.2. Аудит

**Статус**: В Client.Engine **не реализован** сервис аудита.

**Что нужно:**
1. Создать `AuditService` в Client.Engine с методом `GetAuditEntries(filter)`
2. Создать `Contracts/Protos/Client/audit.proto`
3. Подключить UI AuditViewModel

#### 6.3. Политики

**Статус**: В Client.Engine **не реализован** сервис политик.

**Что нужно:**
1. Создать `PoliciesService` в Client.Engine с методами:
   - `GetGroups()`, `CreateGroup()`, `UpdateGroup()`, `DeleteGroup()`
   - `GetTemplates()`, `ApplyTemplate()`
   - `AddFileRestriction()`, `RemoveFileRestriction()`
2. Создать `Contracts/Protos/Client/policies.proto`
3. Подключить UI PoliciesViewModel

#### 6.4. Внешний доступ

Частично покрыт через `StorageClientService.GenerateLinkAsync()` и методы шаринга. Но не хватает:
- `RevokeLink()` — отзыв ссылки (нет в Server.Storage)
- `GetActiveLinks()` — список активных ссылок пользователя
- `GetJournal()` — журнал внешних действий

---

## 6. Полная матрица «кто что должен»

### Client.Engine должен предоставить:

| gRPC Сервис | Метод | Прото | Статус в Engine | Нужен в UI |
|---|---|---|---|---|
| `Authentication` | `Register` | `Client/auth.proto` | ✅ `AuthenticationService` | ✅ |
| `Authentication` | `Login` | `Client/auth.proto` | ✅ `AuthenticationService` | ✅ |
| `CompanyManager` | `CreateCompany` | `Client/company_manager.proto` | ✅ `CompanyManagerService` | ✅ |
| `StorageService` | `ListDirectory` | ❌ Нет клиентского proto | ⚠️ Есть `IStorageService`, нет gRPC | ✅ |
| `StorageService` | `UploadFile` | ❌ | ⚠️ | ✅ |
| `StorageService` | `GetFile` | ❌ | ⚠️ | ✅ |
| `StorageService` | `UpdateFile` | ❌ | ⚠️ | ✅ |
| `StorageService` | `DeleteFile` | ❌ | ⚠️ | ✅ |
| `StorageService` | `MoveFile` | ❌ | ⚠️ | ✅ |
| `StorageService` | `CopyFile` | ❌ | ⚠️ | ✅ |
| `StorageService` | `RenameFile` | ❌ | ⚠️ | ✅ |
| `StorageService` | `NewDirectory` | ❌ | ⚠️ | ✅ |
| `StorageService` | `RenameDirectory` | ❌ | ⚠️ | ✅ |
| `StorageService` | `DeleteDirectory` | ❌ | ⚠️ | ✅ |
| `StorageService` | `MoveDirectory` | ❌ | ⚠️ | ✅ |
| `StorageService` | `CopyDirectory` | ❌ | ⚠️ | ✅ |
| `StorageService` | `GetMetadata` | ❌ | ⚠️ | ✅ |
| `StorageService` | `UpdateMetadata` | ❌ | ⚠️ | ✅ |
| `StorageService` | `GenerateLink` | ❌ | ⚠️ | ✅ |
| `StorageService` | `GenerateDirectLink` | ❌ | ⚠️ | ✅ |
| `StorageService` | `DownloadFileViaLink` | ❌ | ⚠️ | ✅ |
| `StorageService` | `DownloadFileViaDirectLink` | ❌ | ⚠️ | ✅ |
| `Messenger` | `GetThreads` | ❌ Нет proto и сервиса | ❌ Не реализован | ✅ |
| `Messenger` | `SendMessage` | ❌ | ❌ | ✅ |
| `Messenger` | `ApproveAccess` | ❌ | ❌ | ✅ |
| `Audit` | `GetAuditEntries` | ❌ Нет proto и сервиса | ❌ Не реализован | ✅ |
| `Policies` | `GetGroups` | ❌ Нет proto и сервиса | ❌ Не реализован | ✅ |
| `Policies` | `ManageRestrictions` | ❌ | ❌ | ✅ |
| `Settings` | `GetAccount` | ❌ Нет сервиса | ❌ Не реализован | Настройки UI |
| `Settings` | `UpdatePassword` | ❌ | ❌ | Настройки UI |

### Легенда:

- ✅ — Реализовано и работает
- ⚠️ — Частично реализовано (есть бизнес-логика, нет gRPC-обёртки)
- ❌ — Не реализовано

---

## 7. Сводная таблица файлов для изменения

### Client.UI — что нужно изменить:

| Файл | Действие | Приоритет |
|---|---|---|
| `Client.UI.csproj` | Добавить `ProjectReference` на `Contracts`, обновить `Protobuf` references | Высокий |
| `Views/LoginView.axaml` | Добавить поле `AccountId` (или `UserId`) | Высокий |
| `ViewModels/AuthViewModels.cs` | Заменить `Login()`, `Register()`, `CreateCompany()` на gRPC-вызовы | Высокий |
| `ViewModels/FilesViewModel.cs` | Заменить `LoadDemoData()` на gRPC, добавить 21 метод хранилища | Высокий |
| `ViewModels/FeatureViewModels.cs` | Подключить Messenger, Audit, Policies к gRPC (после реализации в Engine) | Средний |
| `Services/GrpcClientService.cs` | Переписать на Named Pipe подключение к `DataGuardPipe` | Высокий |
| `Models/DataModels.cs` | Добавить `FileId` (Guid) в `FileItem`, добавить Storage-модели | Высокий |
| `Protos/` | Заменить server-side proto на client-side из `Contracts/Client/` | Высокий |

### Client.Engine — что нужно добавить:

| Файл | Действие | Приоритет |
|---|---|---|
| `Contracts/Protos/Client/storage.proto` | Создать клиентский proto для 21 метода хранилища | Высокий |
| `Client.Engine/Services/StorageGrpcService.cs` | Создать gRPC-обёртку над `IStorageService` | Высокий |
| `Client.Engine/Program.cs` | Зарегистрировать `StorageGrpcService` в DI и `MapGrpcService` | Высокий |
| `Client.Engine/Services/MessengerService.cs` | Создать сервис мессенджера (новый) | Средний |
| `Client.Engine/Services/AuditService.cs` | Создать сервис аудита (новый) | Средний |
| `Client.Engine/Services/PoliciesService.cs` | Создать сервис политик (новый) | Средний |

---

## 8. Дорожная карта (Roadmap)

### Фаза 1: Критический путь (1–2 дня)

1. ✅ Добавить `ProjectReference` на `Contracts` в `Client.UI.csproj`
2. ✅ Заменить proto-файлы в UI на клиентские из `Contracts/Protos/Client/`
3. ✅ Переписать `GrpcClientService` → `ClientEngineGrpcClient` с Named Pipe
4. ✅ Добавить `AccountId` в `LoginViewModel` и `LoginView`
5. ✅ Заменить `Login()` на gRPC-вызов к `AuthenticationService.Login`
6. ✅ Заменить `Register()` на gRPC-вызов
7. ✅ Заменить `CreateCompany()` на gRPC-вызов
8. ✅ Добавить `AccountId` сохранение в локальное хранилище UI

### Фаза 2: Хранилище (2–3 дня)

9. 🔨 Создать `Contracts/Protos/Client/storage.proto`
10. 🔨 Создать `StorageGrpcService` в Client.Engine
11. 🔨 Зарегистрировать в `Program.cs`
12. 🔨 Переписать `FilesViewModel`:
    - Удалить `LoadDemoData()`
    - Добавить `LoadFilesAsync()`
    - Подключить все операции с файлами
13. 🔨 Добавить `StorageModels` в UI
14. 🔨 Обновить `FileItem` с `FileId`

### Фаза 3: Мессенджер, Аудит, Политики (3–5 дней)

15. 🔨 Реализовать сервис мессенджера в Engine + proto + UI
16. 🔨 Реализовать сервис аудита в Engine + proto + UI
17. 🔨 Реализовать сервис политик в Engine + proto + UI

### Фаза 4: Тестирование и отладка (2–3 дня)

18. 🔨 Полный цикл: UI → Named Pipe → Engine → Server → ответ
19. 🔨 Обработка ошибок (RpcException, timeout, недоступность Engine)
20. 🔨 Индикаторы загрузки в UI при ожидании ответа

---

## 9. Итоговая диаграмма потоков

```
     ┌───────────────────────────────────────────────────────────────┐
     │                     Client.UI                              │
     │                                                               │
     │  LoginView ──────────┐                                        │
     │  RegisterView ───────┤   ClientEngineGrpcClient               │
     │  SetupCompanyView ───┤   (Named Pipe "DataGuardPipe")        │
     │  FilesView ──────────┤        │                               │
     │  MessengerView ──────┤        │  gRPC (HTTP/2)               │
     │  ExternalAccessView ─┤        │                               │
     │  AuditView ──────────┤        │                               │
     │  PoliciesView ───────┘        │                               │
     └───────────────────────────────┼───────────────────────────────┘
                                     │
                                     ▼ Named Pipe
     ┌───────────────────────────────────────────────────────────────┐
     │                     Client.Engine                             │
     │                                                               │
     │  ┌─────────────────┐  ┌──────────────────┐  ┌──────────────┐│
     │  │ Authentication  │  │ CompanyManager   │  │ StorageGrpc  ││
     │  │ Service         │  │ Service          │  │ Service      ││
     │  │ • Register()    │  │ • CreateCompany()│  │ • ListDir()  ││
     │  │ • Login()       │  │                  │  │ • Upload()   ││
     │  └────────┬────────┘  └────────┬─────────┘  │ • Download() ││
     │           │                    │             │ • CRUD...    ││
     │           │                    │             │ • Links()    ││
     │           ▼                    ▼             └──────┬───────┘│
     │  ┌──────────────────────────────────────────────────┤        │
     │  │               gRPC Clients                       │        │
     │  │  AuthClient │ CompanyClient │ StorageClient      │        │
     │  │  SecurityClient             │ HttpClient(REST)   │        │
     │  └──────┬──────────┬──────────────┬────────────────┘        │
     └─────────┼──────────┼──────────────┼──────────────────────────┘
               │          │              │
               ▼          ▼              ▼
     ┌──────────────┐ ┌──────────┐ ┌──────────────┐
     │ Server.Auth  │ │ Company  │ │Server.Storage│
     │ (localhost:  │ │ Manager  │ │(localhost:   │
     │  7203)       │ │ (7203)   │ │ 8081)        │
     └──────┬───────┘ └────┬─────┘ └──────┬───────┘
            │              │              │
            ▼              ▼              ▼
     ┌──────────────┐             ┌──────────────┐
     │ PostgreSQL   │             │  MinIO S3    │
     │ Redis        │             │  PostgreSQL  │
     └──────────────┘             │  Redis       │
                                  └──────────────┘
```
