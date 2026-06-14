#!/usr/bin/env python3
"""
Генератор appsettings.Development.json с случайными секретами для проектов DataGuard.

Использование:
  python generate_appsettings.py [--output-dir D:/DataGuard]

Выводит в консоль сгенерированные секреты в формате "ключ = значение".
Создаёт/перезаписывает appsettings.Development.json, appsettings.json не  touch.
"""

import argparse
import base64
import json
import secrets
import sys
from pathlib import Path

try:
    from argon2.low_level import Type, hash_secret_raw
except ImportError:
    print("Ошибка: требуется 'argon2-cffi'. Установите: pip install argon2-cffi", file=sys.stderr)
    sys.exit(1)

# ─── Конфигурация ────────────────────────────────────────────────────────────

PROJECTS = {
    "Server.Auth": {
        "appsettings": {
            "Logging": {
                "LogLevel": {
                    "Default": "Information",
                    "Microsoft.AspNetCore": "Warning"
                }
            },
            "AllowedHosts": "*",
            "ConnectionStrings": {
                "PostgresConnection": None,
                "RedisConnection": None,
            },
            "Jwt": {
                "Key": None,
                "Issuer": "DataGuard.Server",
                "Audience": "DataGuard.Server",
                "AccessTokenExpiration": "00:30:00",
                "RefreshTokenExpiration": "1.00:00:00"
            },
            "CompanyManager": {
                "MasterKeyHash": None,
            },
            "Security": {
                "NonceSecretKey": None,
                "SaltLength": 32,
                "PasswordHashLength": 32,
                "EncryptedPasswordLength": 64,
                "EncryptedKeyLength": 32,
                "NonceLength": 12,
                "TagLength": 16,
                "MasterKeySalt": None,
                "Argon2": {
                    "DegreeOfParallelism": 1,
                    "Iterations": 3,
                    "MemorySize": 19456,
                    "HashLength": 32
                },
                "RsaKeySize": 4096
            }
        },
    },
    "Client.Engine": {
        "appsettings": {
            "Logging": {
                "LogLevel": {
                    "Default": "Information",
                    "Microsoft.Hosting.Lifetime": "Information"
                }
            },
            "Grpc": {
                "AuthUrl": "https://localhost:7203",
                "CompanyManagerUrl": "https://localhost:7203",
                "SecurityUrl": "https://localhost:7203"
            },
            "Security": {
                "SaltLength": 32,
                "HashLength": 32,
                "HashIterations": 600000,
                "NonceLength": 12,
                "TagLength": 16,
                "MasterKeySalt": None,
                "Argon2": {
                    "DegreeOfParallelism": 1,
                    "Iterations": 3,
                    "MemorySize": 19456,
                    "HashLength": 32
                },
                "Password": {
                    "MinimumLength": 8,
                    "MaximumLength": 21,
                    "EncryptedLength": 64
                },
                "RsaKeySize": 4096,
                "KeyLength": 32
            }
        },
    },
    "Server.Storage": {
        "appsettings": {
            "Logging": {
                "LogLevel": {
                    "Default": "Information",
                    "Microsoft.AspNetCore": "Warning"
                }
            },
            "AllowedHosts": "*"
        },
    },
}

# ─── Генерация секретов ─────────────────────────────────────────────────────

def b64(data: bytes) -> str:
    return base64.b64encode(data).decode("ascii")


def gen_bytes(length: int) -> bytes:
    return secrets.token_bytes(length)


def argon2id_hash(password: bytes, salt: bytes,
                  iterations: int = 3, memory: int = 19456,
                  parallelism: int = 1, hash_len: int = 32) -> bytes:
    return hash_secret_raw(
        secret=password,
        salt=salt,
        time_cost=iterations,
        memory_cost=memory,
        parallelism=parallelism,
        hash_len=hash_len,
        type=Type.ID,
    )


def build_secrets() -> list:
    """Возвращает список строк с секретами и заполняет PROJECTS."""
    secrets_log = []

    # 1. MasterKeySalt — общий для Server.Auth и Client.Engine
    master_key_salt = gen_bytes(32)
    salt_b64 = b64(master_key_salt)
    secrets_log.append(("Security.MasterKeySalt", salt_b64))

    PROJECTS["Server.Auth"]["appsettings"]["Security"]["MasterKeySalt"] = salt_b64
    PROJECTS["Client.Engine"]["appsettings"]["Security"]["MasterKeySalt"] = salt_b64

    # 2. MasterKeyHash — Argon2id от случайного masterKey + salt (хранится на сервере)
    master_key = gen_bytes(32)
    master_key_hash = argon2id_hash(master_key, master_key_salt)
    master_key_hash_b64 = b64(master_key_hash)
    secrets_log.append(("CompanyManager.MasterKey (raw, Base64)", b64(master_key)))
    secrets_log.append(("CompanyManager.MasterKeyHash", master_key_hash_b64))

    PROJECTS["Server.Auth"]["appsettings"]["CompanyManager"]["MasterKeyHash"] = master_key_hash_b64

    # 3. JWT Key — HMAC-SHA256, 32 байта
    jwt_key = gen_bytes(32)
    jwt_key_b64 = b64(jwt_key)
    secrets_log.append(("Jwt.Key", jwt_key_b64))
    PROJECTS["Server.Auth"]["appsettings"]["Jwt"]["Key"] = jwt_key_b64

    # 4. NonceSecretKey — HMAC-SHA256 для nonce, 32 байта
    nonce_key = gen_bytes(32)
    nonce_key_b64 = b64(nonce_key)
    secrets_log.append(("Security.NonceSecretKey", nonce_key_b64))
    PROJECTS["Server.Auth"]["appsettings"]["Security"]["NonceSecretKey"] = nonce_key_b64

    # 5. Postgres password
    pg_password = secrets.token_hex(32)
    pg_conn = f"Host=localhost;Port=5432;Database=dataguard;Username=dataguard;Password={pg_password};"
    secrets_log.append(("ConnectionStrings.PostgresPassword", pg_password))
    PROJECTS["Server.Auth"]["appsettings"]["ConnectionStrings"]["PostgresConnection"] = pg_conn

    # 6. Redis password
    redis_password = secrets.token_hex(32)
    redis_conn = f"localhost:6379,password={redis_password}"
    secrets_log.append(("ConnectionStrings.RedisPassword", redis_password))
    PROJECTS["Server.Auth"]["appsettings"]["ConnectionStrings"]["RedisConnection"] = redis_conn

    return secrets_log


def write_json(project_dir: Path, filename: str, data: dict) -> Path:
    """Записывает JSON-файл в указанную директорию проекта."""
    path = project_dir / filename
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")
    return path


def main():
    parser = argparse.ArgumentParser(
        description="Генератор appsettings.Development.json с секретами для DataGuard"
    )
    parser.add_argument(
        "--output-dir",
        default=".",
        help="Корневая директория проекта (по умолчанию: текущая)",
    )
    args = parser.parse_args()

    root = Path(args.output_dir).resolve()

    # Проверяем что существующие appsettings.json не содержат секретов
    for project_name in PROJECTS:
        existing = root / project_name / "appsettings.json"
        if existing.exists():
            with open(existing, "r", encoding="utf-8") as f:
                data = json.load(f)
            # Простая проверка: если в существующем файле уже есть секреты — предупреждаем
            has_secrets = (
                (data.get("ConnectionStrings", {}).get("PostgresConnection") or "").count(";Password=") > 0
                or (data.get("ConnectionStrings", {}).get("RedisConnection") or "").count("password=") > 0
            )
            if has_secrets:
                print(f"ПРЕДУПРЕЖДЕНИЕ: {existing} уже содержит секреты. Они будут сохранены.", file=sys.stderr)

    # Генерация секретов
    secrets_log = build_secrets()

    # Запись только в appsettings.Development.json
    written = []
    for project_name, project_data in PROJECTS.items():
        project_dir = root / project_name
        path = write_json(project_dir, "appsettings.Development.json", project_data["appsettings"])
        written.append(str(path))

    # Вывод секретов в консоль
    print("=" * 70)
    print("СГЕНЕРИРОВАННЫЕ СЕКРЕТЫ")
    print("=" * 70)
    for key, value in secrets_log:
        print(f"{key} = {value}")
    print()

    print("=" * 70)
    print("ЗАПИСАННЫЕ ФАЙЛЫ")
    print("=" * 70)
    for w in written:
        print(w)

    print()
    print("appsettings.json не изменён.")
    print("Сохраните эти секреты в надёжном месте. При повторном запуске они будут перегенерированы.")


if __name__ == "__main__":
    main()
