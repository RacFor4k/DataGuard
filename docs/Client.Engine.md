# Client.Engine API Documentation

## Обзор

**Client.Engine** — мост между GUI и серверами DataGuard. Предоставляет gRPC API для GUI, делегируя всю обработку логики серверам. Поддерживает операции аутентификации, управления компаниями и файлового хранилища.

**Технологии:**
- ASP.NET Core gRPC
- Entity Framework Core + SQLite (локальное хранение)
- JWT Bearer (аутентификация)
- MinIO S3 (blob-хранилище через Server.Storage)

---

## Authentication Service

### Register

**Вызываемая функция:**
`Register`

**Параметры:**
- `registration_code` (string): Код регистрации
- `password` (string): Пароль

**Необходимые HTTP заголовки:**
- `User-Agent`

**Параметры возврата:**
- `status` (int32): Статус ответа
- `message` (string): Сообщение статуса

**Описание:**
Регистрация нового пользователя с предоставленным кодом регистрации и паролем.

**Примеры возвращаемых значений:**
- Status: 400 Message: "Registration code is required."
- Status: 400 Message: "Password too short"
- Status: 400 Message: "Password too long"
- Status: 400 Message: "Password cannot contain whitespace"
- Status: 400 Message: "Password must contain at least one uppercase letter"
- Status: 400 Message: "Password must contain at least one lowercase letter"
- Status: 400 Message: "Password must contain at least one digit"
- Status: 400 Message: "Password must contain at least one special character"

**Формат входящих данных:**
- `registration_code` должен содержать ровно 12 символов и состоять только из букв и цифр.
- `password` должен содержать от 8 до 32 символов, включая хотя бы одну заглавную букву, одну строчную букву, одну цифру и один специальный символ.

---

## Storage Service

### Обзор

**StorageService** — сервис для работы с файловым хранилищем Server.Storage. Предоставляет 21 gRPC + 2 REST метода для управления файлами, директориями, метаданными и ссылками.

**Особенности:**
- Автоматическое получение nonce для всех изменяющих операций
- Автоматическая подстановка JWT токена в заголовки
- Валидация входных данных на клиенте
- Обработка gRPC-ошибок без проброса исключений на GUI

### Операции с файлами

#### UploadFileAsync

**Вызываемая функция:** `UploadFileAsync`

**Параметры:**
- `fileStream` (Stream): Поток данных файла
- `fileName` (string): Имя файла (без `/` или `\`)
- `filePath` (string): Путь директории (например, `docs/projects`)
- `metadata` (Dictionary<string, string>?): Опциональные метаданные

**Параметры возврата:** `StorageUploadResult`
- `Success` (bool): Успешность операции
- `Message` (string): Сообщение статуса
- `FileId` (string?): GUID созданного файла

**Описание:** Загрузка файла в хранилище. Файл разбивается на чанки по 1 МБ и стримится на сервер.

**Ограничения:**
- Максимальный размер чанка: 1 МБ
- Максимальный размер файла: 5 ГБ

---

#### GetFileAsync

**Вызываемая функция:** `GetFileAsync`

**Параметры:**
- `fileId` (Guid): GUID файла

**Параметры возврата:** `StorageDownloadResult`
- `Success` (bool): Успешность операции
- `Message` (string): Сообщение статуса
- `Content` (Stream?): Поток данных файла
- `FileName` (string?): Имя файла
- `FilePath` (string?): Путь к директории
- `Size` (long): Размер файла в байтах
- `Metadata` (Dictionary<string, string>?): Метаданные файла

**Описание:** Скачивание файла из хранилища. Файл стримится чанками по 256 КБ.

---

#### UpdateFileAsync

**Вызываемая функция:** `UpdateFileAsync`

**Параметры:**
- `fileId` (Guid): GUID файла
- `offset` (long): Смещение в файле (≥ 0)
- `data` (byte[]?): Данные для записи (для операции update)
- `eraseSize` (long?): Размер стираемого блока (для операции erase)

**Параметры возврата:** `StorageOperationResult`
- `Success` (bool): Успешность операции
- `Message` (string): Сообщение статуса

**Описание:** Частичное обновление файла. Поддерживает две операции:
- `update` — запись данных по смещению
- `erase` — запись нулей по смещению

**Требует nonce:** Да

---

#### DeleteFileAsync

**Вызываемая функция:** `DeleteFileAsync`

**Параметры:**
- `fileId` (Guid): GUID файла

**Параметры возврата:** `StorageOperationResult`

**Описание:** Мягкое удаление файла (soft delete). Blob в MinIO не удаляется.

**Требует nonce:** Да

---

#### MoveFileAsync

**Вызываемая функция:** `MoveFileAsync`

**Параметры:**
- `fileId` (Guid): GUID файла
- `newPath` (string): Новый путь директории

**Параметры возврата:** `StorageOperationResult`

**Описание:** Перемещение файла в другую директорию. Blob в MinIO не перемещается.

**Требует nonce:** Да

---

#### CopyFileAsync

**Вызываемая функция:** `CopyFileAsync`

**Параметры:**
- `fileId` (Guid): GUID исходного файла
- `newPath` (string): Путь директории назначения

**Параметры возврата:** `StorageCopyResult`
- `Success` (bool): Успешность операции
- `Message` (string): Сообщение статуса
- `NewFileId` (string?): GUID нового файла

**Описание:** Копирование файла. Создаёт новый blob в MinIO.

**Требует nonce:** Да

---

#### RenameFileAsync

**Вызываемая функция:** `RenameFileAsync`

**Параметры:**
- `fileId` (Guid): GUID файла
- `newName` (string): Новое имя файла (без `/` или `\`)

**Параметры возврата:** `StorageOperationResult`

**Описание:** Переименование файла. Blob в MinIO не изменяется.

**Требует nonce:** Да

---

### Операции с директориями

#### NewDirectoryAsync

**Вызываемая функция:** `NewDirectoryAsync`

**Параметры:**
- `directoryPath` (string): Путь новой директории

**Параметры возврата:** `StorageDirectoryResult`
- `Success` (bool): Успешность операции
- `Message` (string): Сообщение статуса
- `DirectoryId` (string?): GUID созданной директории

**Описание:** Создание новой директории. Родительская директория должна существовать.

**Требует nonce:** Нет

---

#### RenameDirectoryAsync

**Вызываемая функция:** `RenameDirectoryAsync`

**Параметры:**
- `directoryId` (Guid): GUID директории
- `newName` (string): Новое имя директории

**Параметры возврата:** `StorageOperationResult`

**Описание:** Переименование директории. Рекурсивно обновляет пути вложенных объектов.

**Требует nonce:** Да

---

#### DeleteDirectoryAsync

**Вызываемая функция:** `DeleteDirectoryAsync`

**Параметры:**
- `directoryId` (Guid): GUID директории
- `recursive` (bool): Удалять ли вложенные объекты

**Параметры возврата:** `StorageOperationResult`

**Описание:** Мягкое удаление директории. При recursive=false — только пустая директория.

**Требует nonce:** Да

---

#### MoveDirectoryAsync

**Вызываемая функция:** `MoveDirectoryAsync`

**Параметры:**
- `directoryId` (Guid): GUID директории
- `newPath` (string): Новый путь

**Параметры возврата:** `StorageOperationResult`

**Описание:** Перемещение директории. Запрещает перемещение внутрь себя.

**Требует nonce:** Да

---

#### CopyDirectoryAsync

**Вызываемая функция:** `CopyDirectoryAsync`

**Параметры:**
- `directoryId` (Guid): GUID исходной директории
- `newPath` (string): Путь директории назначения
- `recursive` (bool): Копировать ли вложенные объекты

**Параметры возврата:** `StorageDirectoryCopyResult`
- `Success` (bool): Успешность операции
- `Message` (string): Сообщение статуса
- `NewDirectoryId` (string?): GUID новой директории

**Описание:** Копирование директории. При recursive=true — рекурсивно копирует все вложенные файлы.

**Требует nonce:** Да

---

### Операции с атрибутами

#### GetMetadataAsync

**Вызываемая функция:** `GetMetadataAsync`

**Параметры:**
- `fileId` (Guid): GUID файла

**Параметры возврата:** `StorageMetadataResult`
- `Success` (bool): Успешность операции
- `Message` (string): Сообщение статуса
- `FileId` (string?): GUID файла
- `FileName` (string?): Имя файла
- `FilePath` (string?): Путь к директории
- `Size` (long): Размер файла
- `Metadata` (Dictionary<string, string>?): Пользовательские метаданные

**Описание:** Получение метаданных файла. Не возвращает storageKey, bucketName, OwnerId.

**Требует nonce:** Нет

---

#### UpdateMetadataAsync

**Вызываемая функция:** `UpdateMetadataAsync`

**Параметры:**
- `fileId` (Guid): GUID файла
- `metadata` (Dictionary<string, string>): Новые метаданные (полная замена)

**Параметры возврата:** `StorageOperationResult`

**Описание:** Полная замена метаданных файла. Все существующие ключи удаляются.

**Ограничения:**
- Максимум 64 ключа
- Максимальная длина ключа: 256 символов
- Максимальная длина значения: 4096 символов
- Запрещённые ключи: storageKey, ownerId, physicalPath, bucketName, __*

**Требует nonce:** Нет

---

### Операции со списками

#### ListDirectoryAsync

**Вызываемая функция:** `ListDirectoryAsync`

**Параметры:**
- `directoryId` (Guid): GUID директории
- `recursive` (bool): Включать ли вложенные объекты

**Параметры возврата:** `StorageListResult`
- `Success` (bool): Успешность операции
- `Message` (string): Сообщение статуса
- `Items` (List<StorageFileItem>): Список файлов

**Описание:** Получение списка файлов в директории. Поддиректории в ответ не включаются.

**Требует nonce:** Нет

---

### Операции со ссылками

#### GenerateLinkAsync

**Вызываемая функция:** `GenerateLinkAsync`

**Параметры:**
- `fileId` (Guid): GUID файла
- `groups` (string[]?): Список групп с доступом
- `users` (string[]?): Список пользователей с доступом
- `ttlSeconds` (int): Время жизни ссылки в секундах (1 — 2 592 000)

**Параметры возврата:** `StorageLinkResult`
- `Success` (bool): Успешность операции
- `Message` (string): Сообщение статуса
- `Link` (string?): Ссылка вида `/storage/links/{token}`

**Описание:** Генерация безопасной ссылки на файл.

**Требует nonce:** Да

---

#### GenerateDirectLinkAsync

**Вызываемая функция:** `GenerateDirectLinkAsync`

**Параметры:** Аналогично GenerateLinkAsync

**Параметры возврата:** `StorageLinkResult`
- `Link` (string?): Ссылка вида `/storage/direct/{token}`

**Описание:** Генерация прямой ссылки на скачивание файла.

**Требует nonce:** Да

---

#### DownloadFileViaLinkAsync

**Вызываемая функция:** `DownloadFileViaLinkAsync`

**Параметры:**
- `token` (string): Токен ссылки

**Параметры возврата:** `StorageDownloadResult`

**Описание:** Скачивание файла по ссылке (REST API). Не требует JWT.

---

#### DownloadFileViaDirectLinkAsync

**Вызываемая функция:** `DownloadFileViaDirectLinkAsync`

**Параметры:**
- `token` (string): Токен ссылки

**Параметры возврата:** `StorageDownloadResult`

**Описание:** Скачивание файла по прямой ссылке (REST API). Не требует JWT.

---

## Модели данных

### StorageOperationResult
```csharp
public class StorageOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
```

### StorageUploadResult
```csharp
public class StorageUploadResult : StorageOperationResult
{
    public string? FileId { get; set; }
}
```

### StorageDownloadResult
```csharp
public class StorageDownloadResult : StorageOperationResult
{
    public Stream? Content { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long Size { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
```

### StorageCopyResult
```csharp
public class StorageCopyResult : StorageOperationResult
{
    public string? NewFileId { get; set; }
}
```

### StorageDirectoryResult
```csharp
public class StorageDirectoryResult : StorageOperationResult
{
    public string? DirectoryId { get; set; }
}
```

### StorageDirectoryCopyResult
```csharp
public class StorageDirectoryCopyResult : StorageOperationResult
{
    public string? NewDirectoryId { get; set; }
}
```

### StorageMetadataResult
```csharp
public class StorageMetadataResult : StorageOperationResult
{
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long Size { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
```

### StorageListResult
```csharp
public class StorageListResult : StorageOperationResult
{
    public List<StorageFileItem> Items { get; set; } = new();
}
```

### StorageFileItem
```csharp
public class StorageFileItem
{
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long Size { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
```

### StorageLinkResult
```csharp
public class StorageLinkResult : StorageOperationResult
{
    public string? Link { get; set; }
}
```

---

## Консольные команды для отладки

| Команда | Описание |
|---|---|
| `storage_upload <file_path> <storage_path> <file_name>` | Загрузить файл |
| `storage_download <file_id> <output_path>` | Скачать файл |
| `storage_delete <file_id>` | Удалить файл |
| `storage_move <file_id> <new_path>` | Переместить файл |
| `storage_copy <file_id> <new_path>` | Скопировать файл |
| `storage_rename <file_id> <new_name>` | Переименовать файл |
| `storage_get_metadata <file_id>` | Получить метаданные |
| `storage_update_metadata <file_id> <key=value,...>` | Обновить метаданные |
| `storage_new_dir <directory_path>` | Создать директорию |
| `storage_rename_dir <directory_id> <new_name>` | Переименовать директорию |
| `storage_delete_dir <directory_id> <recursive>` | Удалить директорию |
| `storage_move_dir <directory_id> <new_path>` | Переместить директорию |
| `storage_copy_dir <directory_id> <new_path> <recursive>` | Скопировать директорию |
| `storage_list <directory_id> <recursive>` | Список файлов |
| `storage_generate_link <file_id> <ttl_seconds>` | Создать ссылку |
| `storage_generate_direct_link <file_id> <ttl_seconds>` | Создать прямую ссылку |