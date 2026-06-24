# Server.Storage API Documentation

## Обзор

**Server.Storage** — сервис файлового хранилища DataGuard, предоставляющий gRPC и REST API для управления файлами, директориями, метаданными и ссылками. Использует MinIO S3 для blob-хранилища, PostgreSQL для метаданных, Redis для nonce-защиты, JWT Bearer для аутентификации.

**Технологии:**
- ASP.NET Core gRPC (21 метод)
- ASP.NET Core REST (2 метода)
- MinIO SDK 7.0.0 (blob-хранилище)
- Entity Framework Core 10.0.9 + PostgreSQL 18
- StackExchange.Redis 3.0.0 (nonce)
- JWT Bearer 8.2.1 (аутентификация)

---

## Конфигурация

### appsettings.json

| Секция | Ключ | Значение | Описание |
|---|---|---|---|
| ConnectionStrings | PostgresConnection | `Host=localhost;Port=5432;Database=dataguard_storage;Username=${DB_USER};Password=${DB_PASSWORD}` | Строка подключения к PostgreSQL |
| ConnectionStrings | RedisConnection | `localhost:6379,password=${REDIS_PASSWORD}` | Строка подключения к Redis |
| Minio | Endpoint | `localhost:9000` | Адрес MinIO S3 API |
| Minio | AccessKey | `${MINIO_ROOT_USER}` | Ключ доступа MinIO (env var) |
| Minio | SecretKey | `${MINIO_ROOT_PASSWORD}` | Секретный ключ MinIO (env var) |
| Jwt | Secret | `${JWT_SECRET}` | Секрет подписи JWT (env var) |
| Jwt | Issuer | `DataGuard` | Издатель JWT |
| Jwt | Audience | `DataGuard.Storage` | Получатель JWT |

**Требуемые переменные окружения:** `DB_USER`, `DB_PASSWORD`, `REDIS_PASSWORD`, `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `JWT_SECRET`

### Ограничения

| Параметр | Значение |
|---|---|
| Максимальный размер чанка | 1 МБ |
| Максимальный размер файла | 5 ГБ |
| Размер чанка при стриминге | 256 КБ |
| TTL nonce | 5 минут |
| Максимальный TTL ссылки | 30 дней |
| Максимальная длина пути | 4096 символов |
| Максимальная глубина вложенности | 64 уровня |
| Максимум ключей метаданных на файл | 64 |
| Максимальная длина ключа метаданных | 256 символов |
| Максимальная длина значения метаданных | 4096 символов |
| Бакет по умолчанию | `dataguard-storage` |

---

## gRPC Сервис: StorageService

**Прото-контракт:** `Contracts/Protos/storage.proto`
**Пространство имён:** `Contracts.Protos.Storage`
**Реализация:** `Server.Storage.Services.StorageGrpcService`

Все методы извлекают `OwnerId` из JWT токена (клейм `sub`). Все изменяющие операции требуют валидный `nonce_token` (защита от повторных атак через Redis).

### Операции с файлами

#### UploadFile

**Вызываемая функция:** `UploadFile`
**Тип:** Client-streaming gRPC

**Параметры запроса (поток):**
- Первое сообщение — `FileMetadata`:
  - `file_name` (string): Имя файла (без `/` или `\`)
  - `file_path` (string): Путь директории (например, `/docs/projects`)
  - `Metadata` (map<string,string>): Опциональные метаданные
- Последующие сообщения — `bytes chunk` (до 1 МБ каждый)

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса
- `file_id` (string): GUID созданного файла

**Описание:** Загрузка файла в хранилище. Вычисляет SHA-256 хеш, генерирует уникальный storage key (GUID + расширение), сохраняет blob в MinIO, создаёт запись в БД, сохраняет метаданные.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK", FileId: "a1b2c3d4-..."
- Success: false, Message: "Первое сообщение должно содержать метаданные файла."
- Success: false, Message: "Имя файла не может быть пустым."
- Success: false, Message: "Имя файла не должно содержать разделителей пути."
- Success: false, Message: "Некорректный путь: Path traversal не допускается."
- Success: false, Message: "Размер чанка превышает допустимый лимит."
- Success: false, Message: "Размер файла превышает допустимый лимит."
- Success: false, Message: "Не удалось идентифицировать пользователя."
- Success: false, Message: "Внутренняя ошибка сервера."

**Формат входящих данных:**
- `file_name` — непустая строка без символов `/` и `\`, макс. 1024 символа
- `file_path` — относительный путь (например, `docs/projects`), макс. 4096 символов
- Каждый чанк — до 1 МБ
- Общий размер файла — до 5 ГБ

---

#### GetFile

**Вызываемая функция:** `GetFile`
**Тип:** Server-streaming gRPC

**Параметры запроса:**
- `file_id` (string): GUID файла

**Параметры возврата (поток):**
- Первое сообщение — `FileMetadata`:
  - `file_id` (string): GUID файла
  - `file_name` (string): Имя файла
  - `file_path` (string): Путь к директории
  - `size` (int64): Размер файла в байтах
- Последующие сообщения — `bytes chunk` (по 256 КБ)

**Описация:** Скачивание файла из хранилища. Потоковая передача blob из MinIO чанками по 256 КБ.

**Примеры возвращаемых значений:**
- Первое сообщение: Metadata { FileId: "a1b2c3d4-...", FileName: "report.txt", FilePath: "/docs", Size: 1024 }
- Последующие: Chunk { <bytes> }
- Пустой поток (file_id не найден или не принадлежат владельцу)

**Формат входящих данных:**
- `file_id` — валидный GUID

---

#### GetFileChanges

**Вызываемая функция:** `GetFileChanges`
**Тип:** Server-streaming gRPC

**Параметры запроса:**
- `file_id` (string): GUID файла

**Параметры возврата (поток):**
- `update` (UpdateOperation): Операция обновления
  - `offset` (int64): Смещение в файле
  - `data` (bytes): Данные для записи
- `erase` (ErraseOperation): Операция стирания
  - `offset` (int64): Смещение в файле
  - `size` (int64): Размер стираемого блока

**Описание:** Получение журнала изменений файла. **Внимание: метод является заглушкой** — файл загружается из БД, но изменения не стримятся.

---

#### UpdateFile

**Вызываемая функция:** `UpdateFile`
**Тип:** Unary gRPC

**Параметры запроса:**
- `file_id` (int64, oneof): Идентификатор файла
- `update` (UpdateOperation, oneof): Операция обновления
  - `offset` (int64): Смещение в файле (≥ 0)
  - `data` (bytes): Данные для записи
- `erase` (ErraseOperation, oneof): Операция стирания
  - `offset` (int64): Смещение в файле (≥ 0)
  - `size` (int64): Размер стираемого блока
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса

**Описация:** Частичное обновление файла. Поддерживает две операции: `update` — запись данных по смещению, `erase` — запись нулей по смещению. Создаётся новый blob в MinIO с новым storage key.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK"
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор файла."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Файл не найден."
- Success: false, Message: "Некорректный offset для обновления."
- Success: false, Message: "Некорректный offset для стирания."
- Success: false, Message: "Внутренняя ошибка сервера."

**Формат входящих данных:**
- `file_id` — int64, обязательный (один из oneof)
- `update` или `erase` — один из oneof
- `nonce_token` — непустая строка

---

#### DeleteFile

**Вызываемая функция:** `DeleteFile`
**Тип:** Unary gRPC

**Параметры запроса:**
- `file_id` (string): GUID файла
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса

**Описация:** Мягкое удаление файла (soft delete). Устанавливает `DeletedAtUtc` в БД. Blob в MinIO не удаляется.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK"
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор файла."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Файл не найден."
- Success: false, Message: "Не удалось удалить файл."

**Формат входящих данных:**
- `file_id` — валидный GUID
- `nonce_token` — непустая строка

---

#### MoveFile

**Вызываемая функция:** `MoveFile`
**Тип:** Unary gRPC

**Параметры запроса:**
- `file_id` (string): GUID файла
- `new_path` (string): Новый путь директории (например, `/archive`)
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса

**Описание:** Перемещение файла в другую директорию. Blob в MinIO не перемещается — обновляется только путь в БД. Целевая директория должна существовать.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK"
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор файла."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Некорректный путь: Path traversal не допускается."
- Success: false, Message: "Файл не найден."
- Success: false, Message: "Целевая директория не найдена."
- Success: false, Message: "Файл с таким путём уже существует."

**Формат входящих данных:**
- `file_id` — валидный GUID
- `new_path` — относительный путь без path traversal
- `nonce_token` — непустая строка

---

#### CopyFile

**Вызываемая функция:** `CopyFile`
**Тип:** Unary gRPC

**Параметры запроса:**
- `file_id` (string): GUID исходного файла
- `new_path` (string): Путь директории назначения
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса
- `new_file_id` (string): GUID нового файла

**Описание:** Копирование файла. Создаёт новый blob в MinIO (server-side copy), создаёт новую запись в БД с новым GUID.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK", NewFileId: "e5f6a7b8-..."
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор файла."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Некорректный путь: Path traversal не допускается."
- Success: false, Message: "Файл не найден."
- Success: false, Message: "Целевая директория не найдена."
- Success: false, Message: "Файл с таким путём уже существует."
- Success: false, Message: "Не удалось скопировать файл."

**Формат входящих данных:**
- `file_id` — валидный GUID
- `new_path` — относительный путь без path traversal
- `nonce_token` — непустая строка

---

#### RenameFile

**Вызываемая функция:** `RenameFile`
**Тип:** Unary gRPC

**Параметры запроса:**
- `file_id` (string): GUID файла
- `new_name` (string): Новое имя файла (без `/` или `\`)
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса

**Описание:** Переименование файла. Blob в MinIO не изменяется. Обновляется `FileName` и `NormalizedPath` в БД.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK"
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор файла."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Имя файла не может быть пустым."
- Success: false, Message: "Имя файла не должно содержать разделителей пути."
- Success: false, Message: "Файл не найден."
- Success: false, Message: "Файл с таким именем уже существует."

**Формат входящих данных:**
- `file_id` — валидный GUID
- `new_name` — непустая строка без `/` и `\`, макс. 1024 символа
- `nonce_token` — непустая строка

---

### Операции с директориями

#### NewDirectory

**Вызываемая функция:** `NewDirectory`
**Тип:** Unary gRPC

**Параметры запроса:**
- `directory_path` (string): Путь новой директории (например, `/docs/projects`)

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса
- `directory_id` (string): GUID созданной директории

**Описание:** Создание новой директории. Родительская директория должна существовать.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK", DirectoryId: "c3d4e5f6-..."
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Путь директории не может быть пустым."
- Success: false, Message: "Некорректный путь: Path traversal не допускается."
- Success: false, Message: "Директория уже существует."
- Success: false, Message: "Файл с таким путём уже существует."
- Success: false, Message: "Родительская директория не найдена."

**Формат входящих данных:**
- `directory_path` — относительный путь без path traversal, макс. 4096 символов

---

#### RenameDirectory

**Вызываемая функция:** `RenameDirectory`
**Тип:** Unary gRPC

**Параметры запроса:**
- `directory_id` (string): GUID директории
- `new_name` (string): Новое имя директории (без `/` или `\`)
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса

**Описание:** Переименование директории. Рекурсивно обновляет `NormalizedPath` всех вложенных объектов (поддиректорий и файлов).

**Примеры возвращаемых значений:**
- Success: true, Message: "OK"
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор директории."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Имя директории не может быть пустым."
- Success: false, Message: "Имя директории не должно содержать разделителей пути."
- Success: false, Message: "Директория не найдена."
- Success: false, Message: "Директория с таким именем уже существует."

**Формат входящих данных:**
- `directory_id` — валидный GUID
- `new_name` — непустая строка без `/` и `\`
- `nonce_token` — непустая строка

---

#### DeleteDirectory

**Вызываемая функция:** `DeleteDirectory`
**Тип:** Unary gRPC

**Параметры запроса:**
- `directory_id` (string): GUID директории
- `recursive` (bool): Удалять ли вложенные объекты
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса

**Описание:** Мягкое удаление директории. При `recursive=false` — только пустая директория. При `recursive=true` — рекурсивное soft-delete всех вложенных объектов.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK"
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор директории."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Директория не найдена."
- Success: false, Message: "Директория не пуста. Используйте рекурсивное удаление."

**Формат входящих данных:**
- `directory_id` — валидный GUID
- `recursive` — bool
- `nonce_token` — непустая строка

---

#### MoveDirectory

**Вызываемая функция:** `MoveDirectory`
**Тип:** Unary gRPC

**Параметры запроса:**
- `directory_id` (string): GUID директории
- `new_path` (string): Новый путь (например, `/archive`)
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса

**Описание:** Перемещение директории. Запрещает перемещение внутрь себя. Рекурсивно обновляет пути вложенных объектов.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK"
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор директории."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Некорректный путь: Path traversal не допускается."
- Success: false, Message: "Директория не найдена."
- Success: false, Message: "Нельзя переместить директорию внутрь себя."
- Success: false, Message: "Директория с таким путём уже существует."
- Success: false, Message: "Целевая родительская директория не найдена."

**Формат входящих данных:**
- `directory_id` — валидный GUID
- `new_path` — относительный путь без path traversal
- `nonce_token` — непустая строка

---

#### CopyDirectory

**Вызываемая функция:** `CopyDirectory`
**Тип:** Unary gRPC

**Параметры запроса:**
- `directory_id` (string): GUID исходной директории
- `new_path` (string): Путь директории назначения
- `recursive` (bool): Копировать ли вложенные объекты
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса
- `new_directory_id` (string): GUID новой директории

**Описание:** Копирование директории. При `recursive=true` — рекурсивно копирует все вложенные файлы (с копированием blob в MinIO) и поддиректории.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK", NewDirectoryId: "g7h8i9j0-..."
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор директории."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Некорректный путь: Path traversal не допускается."
- Success: false, Message: "Директория не найдена."
- Success: false, Message: "Директория с таким путём уже существует."
- Success: false, Message: "Целевая родительская директория не найдена."

**Формат входящих данных:**
- `directory_id` — валидный GUID
- `new_path` — относительный путь без path traversal
- `recursive` — bool
- `nonce_token` — непустая строка

---

### Операции с атрибутами

#### GetMetadata

**Вызываемая функция:** `GetMetadata`
**Тип:** Unary gRPC

**Параметры запроса:**
- `file_id` (string): GUID файла

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса
- `metadata` (FileMetadata):
  - `file_id` (string): GUID файла
  - `file_name` (string): Имя файла
  - `file_path` (string): Путь к директории
  - `size` (int64): Размер файла
  - `Metadata` (map<string,string>): Пользовательские метаданные

**Описание:** Получение метаданных файла. Не возвращает `storageKey`, `bucketName`, `OwnerId`.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK", Metadata { FileId: "...", FileName: "report.txt", FilePath: "/docs", Size: 1024, Metadata: { "author": "user1" } }
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор файла."
- Success: false, Message: "Файл не найден."

**Формат входящих данных:**
- `file_id` — валидный GUID

---

#### UpdateMetadata

**Вызываемая функция:** `UpdateMetadata`
**Тип:** Unary gRPC

**Параметры запроса:**
- `file_id` (string): GUID файла
- `metadata` (map<string,string>): Новые метаданные (полная замена)

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса

**Описание:** Полная замена метаданных файла. Все существующие ключи удаляются, записываются новые.

**Примеры возвращаемых значений:**
- Success: true, Message: "OK"
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор файла."
- Success: false, Message: "Файл не найден."
- Success: false, Message: "Ключ метаданных не может быть пустым."
- Success: false, Message: "Превышена максимальная длина ключа метаданных."
- Success: false, Message: "Превышена максимальная длина значения метаданных."
- Success: false, Message: "Ключ метаданных 'storageKey' зарезервирован."
- Success: false, Message: "Превышено максимальное количество ключей метаданных."
- Success: false, Message: "Внутренняя ошибка сервера."

**Формат входящих данных:**
- `file_id` — валидный GUID
- `metadata` — map<string,string>, макс. 64 ключа, ключ макс. 256 символов, значение макс. 4096 символов
- Запрещённые ключи: `storageKey`, `ownerId`, `physicalPath`, `bucketName`, все начинающиеся с `__`

---

### Операции со списками

#### ListDirectory

**Вызываемая функция:** `ListDirectory`
**Тип:** Unary gRPC

**Параметры запроса:**
- `directory_id` (string): GUID директории
- `recursive` (bool): Включать ли вложенные объекты

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса
- `items` (repeated FileMetadata): Список файлов

**Описание:** Получение списка файлов в директории. При `recursive=true` включает все вложенные файлы. Поддиректории в ответ не включаются (только файлы).

**Примеры возвращаемых значений:**
- Success: true, Message: "OK", Items: [ { FileId: "...", FileName: "a.txt", FilePath: "/docs", Size: 100 }, { FileId: "...", FileName: "b.txt", FilePath: "/docs", Size: 200 } ]
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор директории."
- Success: false, Message: "Директория не найдена."

**Формат входящих данных:**
- `directory_id` — валидный GUID
- `recursive` — bool

---

### Операции со ссылками

#### GenerateLink

**Вызываемая функция:** `GenerateLink`
**Тип:** Unary gRPC

**Параметры запроса:**
- `file_id` (string): GUID файла
- `groups` (repeated string): Список групп с доступом
- `users` (repeated string): Список пользователей с доступом
- `ttl_seconds` (int32): Время жизни ссылки в секундах (1 — 2 592 000)
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса
- `link` (string): Ссылка вида `/storage/links/{token}`

**Описание:** Генерация безопасной ссылки на файл. Токен генерируется криптографически стойким генератором (32 байта → Base64 URL-safe).

**Примеры возвращаемых значений:**
- Success: true, Message: "OK", Link: "/storage/links/aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789"
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор файла."
- Success: false, Message: "TTL должен быть положительным."
- Success: false, Message: "TTL не может превышать 30 дней."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Файл не найден."
- Success: false, Message: "Внутренняя ошибка сервера."

**Формат входящих данных:**
- `file_id` — валидный GUID
- `ttl_seconds` — int32, от 1 до 2 592 000 (30 дней)
- `nonce_token` — непустая строка

---

#### GenerateDirectLink

**Вызываемая функция:** `GenerateDirectLink`
**Тип:** Unary gRPC

**Параметры запроса:**
- `file_id` (string): GUID файла
- `groups` (repeated string): Список групп с доступом
- `users` (repeated string): Список пользователей с доступом
- `ttl_seconds` (int32): Время жизни ссылки в секундах (1 — 2 592 000)
- `nonce_token` (string): Токен защиты от повторов

**Параметры возврата:**
- `success` (bool): Успешность операции
- `message` (string): Сообщение статуса
- `link` (string): Ссылка вида `/storage/direct/{token}`

**Описание:** Генерация прямой ссылки на скачивание файла. Отличается от `GenerateLink` маршрутом (`/storage/direct/` вместо `/storage/links/`).

**Примеры возвращаемых значений:**
- Success: true, Message: "OK", Link: "/storage/direct/aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789"
- Success: false, Message: "Ошибка аутентификации."
- Success: false, Message: "Некорректный идентификатор файла."
- Success: false, Message: "TTL должен быть положительным."
- Success: false, Message: "TTL не может превышать 30 дней."
- Success: false, Message: "Требуется nonce_token."
- Success: false, Message: "Невалидный или повторный nonce."
- Success: false, Message: "Файл не найден."
- Success: false, Message: "Внутренняя ошибка сервера."

**Формат входящих данных:**
- `file_id` — валидный GUID
- `ttl_seconds` — int32, от 1 до 2 592 000 (30 дней)
- `nonce_token` — непустая строка

---

## REST API

**Контроллер:** `StorageLinksController`
**Префикс маршрута:** `/storage`

### GET /storage/links/{token}

**Параметры:**
- `token` (string, path): Токен ссылки

**Параметры возврата:**
- `302 Redirect` → `/storage/direct/{token}` (если ссылка валидна)
- `410 Gone` — "Ссылка истекла." (если `ExpiresAtUtc < DateTime.UtcNow`)
- `404 Not Found` (если ссылка не найдена)

**Описание:** Редирект на прямую ссылку. Проверяет срок действия ссылки.

---

### GET /storage/direct/{token}

**Параметры:**
- `token` (string, path): Токен ссылки

**Параметры возврата:**
- `200 OK` — файл с `Content-Type: application/octet-stream` и `Content-Disposition` с оригинальным именем
- `410 Gone` — "Ссылка истекла." (если `ExpiresAtUtc < DateTime.UtcNow`)
- `404 Not Found` (если ссылка или файл не найден)
- `500 Internal Server Error` — "Ошибка при загрузке файла." (ошибка MinIO)

**Описание:** Скачивание файла по прямой ссылке. Стримит blob из MinIO.

---

## Модели данных

### StorageFile (таблица: `storage_files`)

| Свойство | Тип | Ограничения | Описание |
|---|---|---|---|
| FileId | Guid | PK | Уникальный идентификатор |
| OwnerId | Guid | NOT NULL | Владелец |
| ParentDirectoryId | Guid? | NULL | Родительская директория |
| FileName | string | NOT NULL, max 1024 | Имя файла |
| NormalizedPath | string | NOT NULL, max 4096 | Нормализованный путь |
| Size | long | NOT NULL | Размер в байтах |
| StorageKey | string | NOT NULL, max 1024 | Ключ в MinIO (GUID + расширение) |
| BucketName | string | NOT NULL, max 256 | Бакет MinIO |
| CreatedAtUtc | DateTime | NOT NULL | Дата создания |
| UpdatedAtUtc | DateTime? | NULL | Дата обновления |
| DeletedAtUtc | DateTime? | NULL | Дата удаления (soft delete) |
| ContentHash | byte[] | bytea | SHA-256 хеш |

---

### StorageDirectory (таблица: `storage_directories`)

| Свойство | Тип | Ограничения | Описание |
|---|---|---|---|
| DirectoryId | Guid | PK | Уникальный идентификатор |
| OwnerId | Guid | NOT NULL | Владелец |
| ParentDirectoryId | Guid? | NULL | Родительская директория |
| DirectoryName | string | NOT NULL, max 1024 | Имя директории |
| NormalizedPath | string | NOT NULL, max 4096 | Нормализованный путь |
| CreatedAtUtc | DateTime | NOT NULL | Дата создания |
| UpdatedAtUtc | DateTime? | NULL | Дата обновления |
| DeletedAtUtc | DateTime? | NULL | Дата удаления (soft delete) |

---

### StorageSharedLink (таблица: `storage_shared_links`)

| Свойство | Тип | Ограничения | Описание |
|---|---|---|---|
| Id | Guid | PK | Уникальный идентификатор |
| FileId | Guid | FK → StorageFile | Файл |
| OwnerId | Guid | NOT NULL | Владелец |
| Token | string | NOT NULL, max 512, UNIQUE | Токен ссылки |
| ExpiresAtUtc | DateTime | NOT NULL | Срок действия |
| IsDirect | bool | NOT NULL | Прямая ссылка |
| CreatedAtUtc | DateTime | NOT NULL | Дата создания |

---

### FileMetadataEntry (таблица: `file_metadata_entries`)

| Свойство | Тип | Ограничения | Описание |
|---|---|---|---|
| Id | Guid | PK | Уникальный идентификатор |
| FileId | Guid | FK → StorageFile | Файл |
| Key | string | NOT NULL, max 256 | Ключ метаданных |
| Value | string | NOT NULL, max 4096 | Значение метаданных |

---

### StorageFileAccess (таблица: `storage_file_access`)

| Свойство | Тип | Ограничения | Описание |
|---|---|---|---|
| Id | Guid | PK | Уникальный идентификатор |
| FileId | Guid | FK → StorageFile | Файл |
| UserId | string? | max 256 | Пользователь |
| GroupId | string? | max 256 | Группа |
| AccessLevel | StorageAccessLevel | NOT NULL | Уровень доступа |

**StorageAccessLevel (enum):** `Read` (0), `Write` (1), `Owner` (2)

---

### StorageNonce (таблица: `storage_nonces`)

| Свойство | Тип | Ограничения | Описание |
|---|---|---|---|
| Id | Guid | PK | Уникальный идентификатор |
| OwnerId | Guid | NOT NULL | Владелец |
| OperationName | string | NOT NULL, max 256 | Имя операции |
| Token | string | NOT NULL, max 512 | Токен nonce |
| ExpiresAtUtc | DateTime | NOT NULL | Срок действия |
| Consumed | bool | NOT NULL | Потреблён |

---

## Аутентификация

Все gRPC методы требуют JWT Bearer токен в заголовке `Authorization`. Токен должен содержать клейм `sub` (GUID владельца).

**Алгоритм:** HMAC-SHA256
**Issuer:** `DataGuard`
**Audience:** `DataGuard.Storage`
**ClockSkew:** 0 (строгая проверка срока)

REST эндпоинты (`/storage/links/`, `/storage/direct/`) не требуют JWT — доступ контролируется через токен ссылки.

---

## Защита от повторных атак (Nonce)

Все изменяющие операции требуют `nonce_token`. Nonce потребляется атомарно через Redis с TTL 5 минут:

- **Ключ Redis:** `nonce:{ownerId}:{operationName}:{token}`
- **Механизм:** `StringSet` с флагом `When.NotExists`
- **Повторное использование:** отклоняется

Операции, требующие nonce: `UpdateFile`, `DeleteFile`, `MoveFile`, `CopyFile`, `RenameFile`, `RenameDirectory`, `DeleteDirectory`, `MoveDirectory`, `CopyDirectory`, `GenerateLink`, `GenerateDirectLink`.

Операции без nonce: `UploadFile`, `GetFile`, `GetFileChanges`, `GetMetadata`, `ListDirectory`, `NewDirectory`, `UpdateMetadata`.

---

## Безопасность

| Аспект | Реализация |
|---|---|
| Path traversal | Запрещён: `..`, абсолютные пути, `C:` drive letters |
| Storage key | Генерируется как `Guid.NewGuid() + расширение`, не зависит от имени файла |
| Публичные токены | Криптографически стойкие (32 байта, `RandomNumberGenerator`), не `Guid.NewGuid()` |
| Soft delete | Файлы и директории помечаются `DeletedAtUtc`, не удаляются физически |
| Ошибки | Не раскрывают внутренние детали (storage key, bucket name, stack trace) |
| Владелец | Проверяется на уровне repository для всех операций |
| Метаданные | Запрещённые ключи: `storageKey`, `ownerId`, `physicalPath`, `bucketName`, `__*` |

---

## Известные ограничения

1. **GetFileChanges** — заглушка, не возвращает данные изменений
2. **DeleteFile** — не удаляет blob из MinIO (orphaned blob)
3. **ListDirectory** — возвращает только файлы, без поддиректорий
4. **UpdateFileRequest.file_id** — в proto определён как `int64`, не `string` (несоответствие с другими методами)
5. **REST эндпоинты** — не имеют rate limiting
