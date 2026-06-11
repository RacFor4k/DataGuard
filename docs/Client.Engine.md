# Client.Engine API Documentation

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