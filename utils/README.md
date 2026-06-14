# DataGuard Utils

Утилиты для генерации конфигурации и работы с секретами проекта DataGuard.

## Содержание

| Файл | Назначение |
|---|---|
| `generate_appsettings.py` | Генератор `appsettings.Development.json` с случайными секретами |
| `hash_tool.py` | Генератор Argon2id-хэша из пароля и соли (Base64) |
| `settings.json` | Параметры Argon2id (DegreeOfParallelism, Iterations, MemorySize) |

---

## generate_appsettings.py

Генерирует `appsettings.Development.json` для проектов `Server.Auth`, `Client.Engine`, `Server.Storage` с заполненными секретами. Исходные `appsettings.json` **не перезаписываются**.

### Зависимости

```bash
pip install argon2-cffi
```

### Использование

```bash
python generate_appsettings.py --output-dir D:/DataGuard
```

По умолчанию `--output-dir` — текущая директория.

### Что генерируется

| Секрет | Проекты | Формат |
|---|---|---|
| `ConnectionStrings.PostgresConnection` | Server.Auth | Connection string с random password (hex 64) |
| `ConnectionStrings.RedisConnection` | Server.Auth | Connection string с random password (hex 64) |
| `Jwt.Key` | Server.Auth | Base64(32 random bytes), HMAC-SHA256 |
| `CompanyManager.MasterKeyHash` | Server.Auth | `Argon2id(random_32bytes, salt)` → Base64 |
| `CompanyManager.MasterKey` (raw) | *(вывод в консоль)* | Исходный ключ клиента, Base64 |
| `Security.NonceSecretKey` | Server.Auth | Base64(32 random bytes), HMAC-SHA256 для nonce |
| `Security.MasterKeySalt` | Server.Auth + Client.Engine | Base64(32 random bytes), **общий** для обоих |

### Вывод в консоль

После записи файлов скрипт выводит все секреты в формате:

```
======================================================================
СГЕНЕРИРОВАННЫЕ СЕКРЕТЫ
======================================================================
Security.MasterKeySalt = ...
CompanyManager.MasterKey (raw, Base64) = ...
CompanyManager.MasterKeyHash = ...
Jwt.Key = ...
Security.NonceSecretKey = ...
ConnectionStrings.PostgresPassword = ...
ConnectionStrings.RedisPassword = ...
```

---

## hash_tool.py

Скрипт для вычисления Argon2id-хэша из пароля и соли, передаваемых в Base64.

### Использование

```bash
python hash_tool.py -s <salt_base64>                        # пароль запросится интерактивно
python hash_tool.py -s <salt_base64> -p <password_base64>   # пароль в Base64
python hash_tool.py -s <salt_base64> -c settings.json       # путь к конфигурации Argon2
```

### Параметры

- `-s, --salt` — соль в Base64 (обязательный)
- `-p, --password` — пароль в Base64 (если не указан — безопасный ввод)
- `-c, --config` — путь к JSON-файлу с параметрами Argon2 (по умолчанию `settings.json`)

### Формат settings.json

```json
{
  "Security": {
    "Argon2": {
      "DegreeOfParallelism": 1,
      "Iterations": 3,
      "MemorySize": 19456
    }
  }
}
```

---

## Логика секретов DataGuard

```
┌─────────────────────────────────────────────────────────┐
│                    MasterKeySalt                        │
│                   (32 random bytes)                      │
│                   одинаковый для:                        │
│              Server.Auth + Client.Engine                │
└──────────┬──────────────────┬───────────────────────────┘
           │                  │
           ▼                  ▼
┌──────────────────┐  ┌─────────────────────────────────┐
│   Server.Auth    │  │  Client.Engine                  │
│                  │  │                                  │
│  MasterKeyHash = │  │  MasterKeyHash =                │
│  Argon2id(       │  │  Argon2id(client_master_key,    │
│   master_key,    │  │   MasterKeySalt)                │
│   salt)          │  │         │                        │
│                  │  │         │ отправляется на сервер   │
│  хранит hash,    │◄─┘        │ в CreateCompanyRequest    │
│  сравнивает     │           │                            │
│  FixedTimeEquals │           └───────────────────────────┘
└──────────────────┘
```

- `MasterKeySalt` — общий, т.к. мастер-ключ — случайный массив байтов (не подобрать радужными таблицами)
- `MasterKeyHash` на сервере = Argon2id от оригинального masterKey с этой солью
- Клиент вычисляет такой же хэш и отправляет в `CreateCompanyRequest.MasterKey`
- Сервер сравнивает через `CryptographicOperations.FixedTimeEquals`
