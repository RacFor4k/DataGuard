# Server API Documentation

## Authentication Service

### Register

**Вызываемая функция:**
`Register`

**Параметры:**
- `registration_code` (string): Код регистрации
- `encrypted_password` (string): Зашифрованный пароль
- `encrypted_key` (string): Зашифрованный ключ
- `password_hash` (string): Хеш пароля
- `client_salt` (string): Соль клиента

**Необходимые HTTP заголовки:**
- `User-Agent`

**Параметры возврата:**
- `status` (int32): Статус ответа
- `message` (string): Сообщение статуса
- `public_master_key` (bytes): Публичный мастер-ключ
- `jwt_access_token` (string): Access JWT токен
- `jwt_refresh_token` (string): Refresh JWT токен

**Описание:**
Регистрация нового пользователя с предоставленным кодом регистрации, зашифрованным паролем, зашифрованным ключом и хешем пароля.

**Примеры возвращаемых значений:**
- Status: 400 Message: "Registration code is empty"
- Status: 400 Message: "Password is invalid"
- Status: 400 Message: "Key is invalid"
- Status: 400 Message: "Password hash is invalid"
- Status: 400 Message: "Registration code is invalid"
- Status: 400 Message: "Registration data is invalid"
- Status: 400, Message: "Client salt is invalid"
- Status: 409 Message: "User is already registered"
- Status: 400 Message: "Company is invalid"
- Status: 400 Message: "GroupMembers is invalid"
- Status: 200 Message: "OK" PublicMasterKey: <public_master_key> JwtAccessToken: <access_token> JwtRefreshToken: <refresh_token>

**Формат входящих данных:**
- `registration_code` должен быть непустым.
- `encrypted_password`, `encrypted_key` и `password_hash` должны содержать ровно 32 символа.

### SetMasterKey

**Вызываемая функция:**
`SetMasterKey`

**Параметры:**
- `master_key` (bytes): Мастер-ключ

**Необходимые HTTP заголовки:**
- `Authorization`: JWT токен

**Параметры возврата:**
- `status` (int32): Статус ответа
- `message` (string): Сообщение статуса
- `jwt_access_token` (string): Access JWT токен
- `jwt_refresh_token` (string): Refresh JWT токен

**Описание:**
Установка зашифрованного ключа пользователя мастер-ключом.

**Примеры возвращаемых значений:**
- Status: 401 Message: "Token is invalid"
- Status: 400 Message: "Master key is empty"
- Status: 403 Message: "Token is invalid"
- Status: 200 Message: "OK" JwtAccessToken: <access_token> JwtRefreshToken: <refresh_token>

**Формат входящих данных:**
- `master_key` должен содержать ровно 512 символа.

### Login

**Вызываемая функция:**
`Login`

**Параметры:**
- `user_id` (string): Идентификатор пользователя
- `password_hash` (bytes): Хеш пароля
- `nonce_token` (string): Nonce токен

**Необходимые HTTP заголовки:**
- `User-Agent`

**Параметры возврата:**
- `status` (int32): Статус ответа
- `message` (string): Сообщение статуса
- `encrypted_key` (bytes): Зашифрованный ключ
- `jwt_access_token` (string): Access JWT токен
- `jwt_refresh_token` (string): Refresh JWT токен

**Описание:**
Аутентификация пользователя с предоставленным email и паролем. Генерирует Access токен (30 мин) и Refresh токен (24 ч).

**Примеры возвращаемых значений:**
- Status: 400 Message: "UserId is empty"
- Status: 400 Message: "Pin is invalid"
- Status: 400 Message: "NonceToken is empty"
- Status: 400 Message: "NonceToken is invalid"
- Status: 404 Message: "User is not found"
- Status: 401 Message: "Pin is invalid"
- Status: 200 Message: "OK" EncryptedKey: <encrypted_key> JwtAccessToken: <access_token> JwtRefreshToken: <refresh_token>

**Формат входящих данных:**
- `user_id` должен быть непустым.
- `pin_hash` должен содержать ровно 32 символа.
- `nonce_token` должен быть непустым.

### RefreshToken

**Вызываемая функция:**
`RefreshToken`

**Параметры:**
Не требует параметров.

**Необходимые HTTP заголовки:**
- `Authorization`: JWT токен

**Параметры возврата:**
- `status` (int32): Статус ответа
- `message` (string): Сообщение статуса
- `jwt_access_token` (string): Access JWT токен

**Описание:**
Обновление токена пользователя с предоставленным токеном. Валидирует Refresh-токен и выдает новый Access токен. Refresh токен не rotates из-за ограничений протокола гRPC.

**Примеры возвращаемых значений:**
- Status: 401 Message: "Token is invalid"
- Status: 200 Message: "OK" JwtAccessToken: <access_token>

**Формат входящих данных:**
Не требует параметров.

### CreateRegistrationCode

**Вызываемая функция:**
`CreateRegistrationCode`

**Параметры:**
- `name` (string): Имя
- `surname` (string): Фамилия
- `email` (string): Email
- `groups` (list): Список групп
- `admin_groups` (list): Список групп администраторов

**Необходимые HTTP заголовки:**
- `Authorization`: JWT токен

**Параметры возврата:**
- `status` (int32): Статус ответа
- `message` (string): Сообщение статуса
- `registration_code` (string): Код регистрации

**Описание:**
Создание кода регистрации для нового пользователя.

**Примеры возвращаемых значений:**
- Status: 400 Message: "Name is empty"
- Status: 400 Message: "Surname is empty"
- Status: 400 Message: "Email is empty"
- Status: 400 Message: "Groups is empty"
- Status: 401 Message: "Token is invalid"
- Status: 507 Message: "Registration code is invalid"
- Status: 400 Message: "Registration data is invalid"
- Status: 200 Message: "OK" RegistrationCode: <registration_code>

**Формат входящих данных:**
- `name`, `surname` и `email` должны быть непустыми.
- `groups` и `admin_groups` должны содержать хотя бы одну группу.