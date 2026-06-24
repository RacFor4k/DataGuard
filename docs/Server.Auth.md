# Server.Auth — Сервис аутентификации

## Обзор

**Server.Auth** — ASP.NET Core gRPC-сервис (.NET 10), отвечающий за управление пользователями, компаниями, группами, аутентификацией и выдачу JWT-токенов. Предоставляет 3 gRPC-сервиса (9 RPC-методов).

**Технологии:**
- ASP.NET Core gRPC 2.80.0
- Entity Framework Core 10.0.9 + PostgreSQL 18 (Npgsql)
- StackExchange.Redis 3.0.0 (nonce, чёрный список JWT, данные регистрации)
- System.IdentityModel.Tokens.Jwt 8.2.1 (HMAC-SHA256)
- Konscious.Security.Cryptography.Argon2 1.3.1
- Nanoid 3.1.0 (генерация кодов регистрации)

**Порт:** `:7203` (HTTPS, контейнерный порт `8081`)

---

## Конфигурация

### appsettings.json

| Секция | Ключ | Тип | Значение по умолчанию | Описание |
|:---|:---|:---|:---|:---|
| `ConnectionStrings` | `PostgresConnection` | string | — | Строка подключения к PostgreSQL (база `dataguard`) |
| `ConnectionStrings` | `RedisConnection` | string | — | Строка подключения к Redis |
| `Jwt` | `Key` | byte[] (Base64) | — | Секретный ключ подписи JWT (env var `JWT_KEY`) |
| `Jwt` | `Issuer` | string | `DataGuard.Server` | Издатель токенов |
| `Jwt` | `Audience` | string | `DataGuard.Server` | Получатель токенов |
| `Jwt` | `AccessTokenExpiration` | TimeSpan | `00:30:00` | Срок действия access-токена |
| `Jwt` | `RefreshTokenExpiration` | TimeSpan | `1.00:00:00` (24 ч) | Срок действия refresh-токена |
| `CompanyManager` | `MasterKeyHash` | byte[] (Base64) | — | Хеш мастер-ключа для создания компаний (env var) |
| `Security` | `NonceSecretKey` | byte[] (Base64) | — | Ключ HMAC для nonce-токенов (env var) |
| `Security` | `SaltLength` | int | 32 | Длина соли (256 бит) |
| `Security` | `PasswordHashLength` | int | 32 | Длина хеша пароля Argon2id (256 бит) |
| `Security` | `EncryptedPasswordLength` | int | 64 | Длина шифрованного пароля (512 бит) |
| `Security` | `EncryptedKeyLength` | int | 32 | Длина шифрованного ключа (256 бит) |
| `Security` | `NonceLength` | int | 12 | Длина nonce AES-GCM (96 бит) |
| `Security` | `TagLength` | int | 16 | Длина тега аутентификации AES-GCM (128 бит) |
| `Security` | `MasterKeySalt` | byte[] (Base64) | — | Соль мастер-ключа компании (env var) |
| `Security` | `RsaKeySize` | int | 4096 | Размер RSA-ключа компании (бит) |
| `Security` | `Argon2:DegreeOfParallelism` | int | 1 | Параллелизм Argon2id |
| `Security` | `Argon2:Iterations` | int | 3 | Итерации Argon2id |
| `Security` | `Argon2:MemorySize` | int | 19456 | Объём памяти Argon2id (19 МБ) |

---

## Middleware

### JwtMiddleware

Пользовательский middleware (`Server.Auth/Middlewares/JwtMiddleware.cs`), выполняющий автоматическую валидацию JWT-токенов.

**Порядок обработки:**

1. Извлекает заголовок `Authorization` из HTTP-запроса
2. Проверяет наличие префикса `Bearer `
3. Передаёт токен в `IJwtService.VerifyTokenAsync()`
4. Результат валидации (`JwtSecurityToken` или `null`) записывается в scoped-сервис `UserAccessor`

**Жизненный цикл:** `UserAccessor` регистрируется как `Scoped`, поэтому каждая gRPC-вызов получает собственный экземпляр. Сервисы извлекают текущего пользователя через `_userAccessor.userJwt`.

**Внимание:** JwtMiddleware **не прерывает** запрос при невалидном токене — он лишь устанавливает `userJwt = null`. Решение об отказе принимает каждый сервисный метод самостоятельно.

---

## Сервисы

### 1. Authentication (gRPC)

**Контракт:** `Contracts/Protos/auth.proto`
**Реализация:** `Server.Auth/Services/AuthenticationService.cs`
**Зависимости:** `DataGuardDbContext`, `IConnectionMultiplexer` (Redis), `IJwtService`, `ISecurityService`, `UserAccessor`, `IOptions<SecurityOptions>`

#### 1.1. Register

Регистрация нового пользователя по коду регистрации.

**RPC:** `Register (RegisterRequest) returns (RegisterResponse)`

**Параметры запроса:**

| Поле | Тип | Формат | Описание |
|:---|:---|:---|:---|
| `registration_code` | string | 12 символов, `[A-Z0-9]` | Код регистрации, полученный через `CreateRegistrationCode` |
| `encrypted_password` | bytes | 92 байта | Пароль, зашифрованный AES-256-GCM: `nonce(12) + tag(16) + ciphertext(64)` |
| `encrypted_key` | bytes | 60 байт | Symmetric key, зашифрованный AES-256-GCM: `nonce(12) + tag(16) + ciphertext(32)` |
| `backup_encrypted_key` | bytes | 512 байт | Symmetric key, зашифрованный RSA-OAEP-SHA256 публичным ключом компании |
| `password_hash` | bytes | 64 байта | Argon2id-хеш пароля: `client_salt(32) + hash(32)` |
| `client_salt` | bytes | 32 байта | Соль для Argon2id, генерируемая на клиенте |

**Заголовки:**
- `User-Agent` — рекомендуется

**Параметры ответа:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `status` | int32 | HTTP-подобный код статуса |
| `message` | string | Текстовое описание |
| `user_id` | string | GUID зарегистрированного пользователя |
| `email` | string | Email пользователя |
| `company_public_key_pem` | string | RSA-4096 публичный ключ компании (PEM) |
| `jwt_access_token` | string | JWT access-токен (30 мин) |
| `jwt_refresh_token` | string | JWT refresh-токен (24 ч) |

**Коды ответов:**

| Статус | Сообщение | Условие |
|:---|:---|:---|
| 200 | OK | Успешная регистрация |
| 400 | Registration code is empty | Пустой код регистрации |
| 400 | Password is invalid | Некорректная длина `encrypted_password` |
| 400 | Key is invalid | Некорректная длина `encrypted_key` |
| 400 | Password hash is invalid | Некорректная длина `password_hash` |
| 400 | Client salt is invalid | Некорректная длина `client_salt` |
| 400 | Backup encrypted key is invalid | Некорректная длина `backup_encrypted_key` |
| 400 | Registration code is invalid | Код не найден в Redis или не десериализуется |
| 400 | Registration data is invalid | Ошибка при добавлении групп |
| 400 | Company is invalid | Компания не найдена или `public_key_pem` не установлен |
| 409 | User is already registered | Пользователь с таким email уже существует |

**Логика:**

1. Валидация формата всех полей (строгая проверка длин через `SecurityOptions`)
2. Получение данных регистрации из Redis по коду (JSON → `RegistrationData`)
3. Проверка уникальности email
4. Извлечение `Company` из БД
5. Разделение `password_hash` на соль и хеш (`Buffer.BlockCopy`)
6. Генерация серверной соли (`SecurityService.GenerateSalt`)
7. Создание `User` с полным набором зашифрованных данных
8. Создание записей `GroupMember` для каждой указанной группы
9. Сохранение в PostgreSQL (`SaveChangesAsync`)
10. Удаление кода регистрации из Redis (одноразовое использование)
11. Генерация JWT access + refresh токенов

---

#### 1.2. Login

Аутентификация пользователя.

**RPC:** `Login (LoginRequest) returns (LoginResponse)`

**Параметры запроса:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `user_id` | string | GUID пользователя |
| `password_hash` | bytes | Argon2id-хеш пароля: `client_salt(32) + hash(32)` |
| `nonce_token` | string | Nonce-токен, полученный через `Security.GetNonce()` |

**Заголовки:**
- `User-Agent` — рекомендуется

**Параметры ответа:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `status` | int32 | HTTP-подобный код статуса |
| `message` | string | Текстовое описание |
| `encrypted_key` | bytes | Symmetric key пользователя, зашифрованный AES-256-GCM |
| `jwt_access_token` | string | JWT access-токен (30 мин) |
| `jwt_refresh_token` | string | JWT refresh-токен (24 ч) |

**Коды ответов:**

| Статус | Сообщение | Условие |
|:---|:---|:---|
| 200 | OK | Успешная аутентификация |
| 400 | UserId is empty | Пустой идентификатор |
| 400 | Password is invalid | Некорректная длина `password_hash` |
| 400 | NonceToken is empty | Пустой nonce-токен |
| 400 | NonceToken is invalid | Nonce не прошёл верификацию или уже использован |
| 400 | UserId is invalid | Не удалось распарсить GUID |
| 401 | Password is invalid | Несовпадение хеша (сравнение через `FixedTimeEquals`) |
| 404 | User is not found | Пользователь не найден в БД |

**Логика:**

1. Валидация `user_id`, `password_hash`, `nonce_token`
2. Верификация nonce-токена через `ISecurityService.VerifyNonceToken()` (удаление из Redis после проверки)
3. Поиск пользователя по `user_id` в PostgreSQL
4. Извлечение хеша из `password_hash` (пропуск первых 32 байт соли)
5. Сравнение хешей через `CryptographicOperations.FixedTimeEquals`
6. Генерация новой пары JWT-токенов

---

#### 1.3. RefreshToken

Обновление access-токена.

**RPC:** `RefreshToken (RefreshTokenRequest) returns (RefreshTokenResponse)`

**Параметры запроса:** Отсутствуют (токен передаётся в заголовке).

**Заголовки:**
- `Authorization: Bearer {refresh_token}` — обязательно

**Параметры ответа:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `status` | int32 | HTTP-подобный код статуса |
| `message` | string | Текстовое описание |
| `jwt_access_token` | string | Новый JWT access-токен |
| `jwt_refresh_token` | string | Новый JWT refresh-токен |

**Коды ответов:**

| Статус | Сообщение | Условие |
|:---|:---|:---|
| 200 | OK | Токен обновлён |
| 400 | Token is invalid | Пользователь не найден или идентификатор не GUID |
| 401 | Token is invalid | Токен отсутствует, невалиден или является access-токеном (не refresh) |

**Логика:**

1. Извлечение JWT из `UserAccessor` (парсинг middleware)
2. Проверка, что это refresh-токен (claim `typ = "refresh"`)
3. Загрузка пользователя с группами из PostgreSQL
4. Генерация новой пары JWT-токенов

**Внимание:** Refresh-токен не ротируется (не выдаётся новый refresh-токен взамен старого) из-за ограничений протокола gRPC (невозможно вернуть новый refresh-токен в каждом ответе). Данный метод возвращает оба токена — и access, и refresh.

---

#### 1.4. CreateRegistrationCode

Создание кода регистрации для нового пользователя. Требует JWT-аутентификации.

**RPC:** `CreateRegistrationCode (CreateRegistrationCodeRequest) returns (CreateRegistrationCodeResponse)`

**Параметры запроса:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `name` | string | Имя нового пользователя |
| `surname` | string | Фамилия нового пользователя |
| `email` | string | Email нового пользователя |
| `groups` | repeated string | Список GUID групп (в формате строки) |
| `admin_groups` | repeated string | Список GUID групп, в которых пользователь будет администратором |

**Заголовки:**
- `Authorization: Bearer {access_token}` — обязательно

**Параметры ответа:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `status` | int32 | HTTP-подобный код статуса |
| `message` | string | Текстовое описание |
| `registration_code` | string | 12-символьный код (Nanoid, `[A-Z0-9]`) |

**Коды ответов:**

| Статус | Сообщение | Условие |
|:---|:---|:---|
| 200 | OK | Код создан |
| 400 | Name is empty / Surname is empty / Email is empty | Пустые обязательные поля |
| 400 | Groups is empty | Не указана ни одна группа |
| 400 | Registration data is invalid | Ошибка сериализации GUID групп |
| 401 | Token is invalid | Отсутствует или невалидный JWT |
| 507 | Registration code is invalid | Ошибка записи в Redis |

**Логика:**

1. Валидация обязательных полей
2. Проверка JWT (access-токен)
3. Определение `companyId` текущего пользователя
4. Сериализация `RegistrationData` в JSON
5. Генерация 12-символьного кода через Nanoid
6. Сохранение в Redis с TTL 30 дней
7. Возврат кода

---

### 2. CompanyManager (gRPC)

**Контракт:** `Contracts/Protos/company_manager.proto`
**Реализация:** `Server.Auth/Services/CompanyManagerService.cs`
**Зависимости:** `DataGuardDbContext`, `IConnectionMultiplexer` (Redis), `ISecurityService`, `IOptions<SecurityOptions>`, `IOptions<CompanyManagerOptions>`

#### 2.1. CreateCompany

Создание компании. Доступно без аутентификации.

**RPC:** `CreateCompany (CreateCompanyRequest) returns (CreateCompanyResponse)`

**Параметры запроса:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `nonce_token` | string | Nonce-токен (полученный через `Security.GetNonce()`) |
| `master_key` | bytes | Хеш мастер-ключа: `master_key_salt + hash`. Длина должна совпадать с конфигурацией |
| `company_name` | string | Название компании |
| `company_email` | string | Email компании |

**Параметры ответа:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `status` | int32 | HTTP-подобный код статуса |
| `message` | string | Текстовое описание |
| `registration_code` | string | Код регистрации для первого пользователя (владельца) |

**Коды ответов:**

| Статус | Сообщение | Условие |
|:---|:---|:---|
| 200 | OK | Компания создана |
| 400 | Nonce token is empty | Пустой nonce |
| 400 | Master key is empty | Пустой мастер-ключ |
| 400 | Company name is empty / Company email is empty | Пустые обязательные поля |
| 400 | Company email is invalid | Некорректный формат email |
| 400 | Nonce token is invalid | Nonce не прошёл верификацию |
| 400 | Master key is invalid | Несовпадение хеша (сравнение через `FixedTimeEquals`) |

**Логика:**

1. Валидация всех полей
2. Верификация nonce-токена
3. Извлечение хеша из `master_key` (пропуск префикса `MasterKeySalt`)
4. Сравнение с `CompanyManagerOptions.MasterKeyHash` через `FixedTimeEquals`
5. Создание `Company` с группой `system:owner`
6. Генерация кода регистрации для владельца
7. Сохранение данных регистрации в Redis (TTL 30 дней)

---

#### 2.2. GetCompanyPublicKey

Получение RSA-публичного ключа компании по коду регистрации.

**RPC:** `GetCompanyPublicKey (GetCompanyPublicKeyRequest) returns (GetCompanyPublicKeyResponse)`

**Параметры запроса:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `registration_code` | string | Код регистрации |

**Параметры ответа:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `status` | int32 | Код статуса |
| `message` | string | Текстовое описание |
| `company_public_key_pem` | string | RSA-4096 публичный ключ в PEM-формате |

**Коды ответов:**

| Статус | Сообщение | Условие |
|:---|:---|:---|
| 200 | OK | Ключ получен |
| 400 | Registration code is empty / Registration code is invalid | Код не найден или невалиден |
| 400 | Company is invalid | `public_key_pem` не установлен у компании |

---

#### 2.3. SetCompanyPublicKey

Установка RSA-публичного ключа компании. Вызывается первым регистрирующимся пользователем после генерации RSA-пары.

**RPC:** `SetCompanyPublicKey (SetCompanyPublicKeyRequest) returns (SetCompanyPublicKeyResponse)`

**Параметры запроса:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `registration_code` | string | Код регистрации |
| `company_public_key_pem` | string | RSA-4096 публичный ключ в PEM-формате |

**Параметры ответа:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `status` | int32 | Код статуса |
| `message` | string | Текстовое описание |

---

### 3. SecurityService (gRPC)

**Контракт:** `Contracts/Protos/security.proto`
**Реализация:** `Server.Auth/Services/SecurityRequestsService.cs`
**Зависимости:** `ISecurityService`, `DataGuardDbContext`

#### 3.1. GetNonce

Генерация nonce-токена для защиты от повторных атак.

**RPC:** `GetNonce (NonceRequest) returns (NonceResponse)`

**Параметры запроса:** Отсутствуют.

**Параметры ответа:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `status` | int32 | Код статуса |
| `message` | string | Текстовое описание |
| `nonce_token` | string | Токен формата `{unix_timestamp}.{guid}.{hmac_sha256_signature}` |

**Формат nonce-токена:**

```
{expiration}.{nonce_guid}.{base64_hmac_sha256_signature}
```

- `expiration` — Unix-время истечения (текущее + 5 минут)
- `nonce_guid` — уникальный идентификатор
- `signature` — HMAC-SHA256 от строки `{expiration}.{nonce_guid}` с ключом `SecurityOptions.NonceSecretKey`

Верификация: проверка подписи, проверка срока действия, удаление из Redis (одноразовое использование).

#### 3.2. GetSalt

Получение клиентской соли пользователя (необходима для хеширования пароля при входе).

**RPC:** `GetSalt (SaltRequest) returns (SaltResponse)`

**Параметры запроса:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `user_id` | string | GUID пользователя |

**Параметры ответа:**

| Поле | Тип | Описание |
|:---|:---|:---|
| `status` | int32 | Код статуса |
| `message` | string | Текстовое описание |
| `salt` | bytes | Соль клиента (32 байта) |

**Коды ответов:**

| Статус | Сообщение | Условие |
|:---|:---|:---|
| 200 | OK | Соль получена |
| 400 | UserId is empty | Пустой идентификатор |
| 400 | UserId is invalid | Не удалось распарсить GUID |
| 400 | Client salt is invalid | Пользователь не найден |

---

## JWT-токены

### Структура claims

| Claim | Тип | Описание |
|:---|:---|:---|
| `sub` | string | GUID пользователя |
| `name` | string | Полное имя (`{name} {surname}`) |
| `given_name` | string | Имя |
| `family_name` | string | Фамилия |
| `email` | string | Email |
| `jti` | string | Уникальный идентификатор токена (GUID) |
| `typ` | string | `"access"` или `"refresh"` |
| `role` | string (повторяющийся) | Названия групп пользователя |

### Механизм отзыва

| Тип токена | Механизм | Хранилище |
|:---|:---|:---|
| Access | Добавление `jti` в чёрный список | Redis, ключ `jwt:blacklist:{jti}`, TTL = оставшееся время жизни |
| Refresh | Удаление записи из БД | PostgreSQL, таблица `identity.UserJwtRefreshTokens` |

Проверка отзыва выполняется внутри `JwtService.VerifyTokenAsync()`.