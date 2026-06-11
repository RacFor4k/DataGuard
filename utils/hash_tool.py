import argparse
import base64
import getpass
import json
import os
import sys

try:
    from argon2.low_level import Type, hash_secret_raw
except ImportError:
    print("Ошибка: требуется библиотека 'argon2-cffi'. Установите её: pip install argon2-cffi", file=sys.stderr)
    sys.exit(1)

HASH_LENGTH = 32  # Длина результирующего хэша в байтах

def load_settings(config_path):
    """Загружает параметры Argon2id из JSON-файла."""
    if not os.path.exists(config_path):
        print(f"Ошибка: Файл конфигурации '{config_path}' не найден.", file=sys.stderr)
        sys.exit(1)
        
    try:
        with open(config_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
            
        security = data["Security"]
        argon2_settings = security["Argon2"]
        
        return {
            "parallelism": int(argon2_settings["DegreeOfParallelism"]),
            "iterations": int(argon2_settings["Iterations"]),
            "memory_size": int(argon2_settings["MemorySize"])
        }
    except KeyError as e:
        print(f"Ошибка: В конфигурационном файле отсутствует обязательное поле: {e}", file=sys.stderr)
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"Ошибка чтения JSON в '{config_path}': {e}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Непредвиденная ошибка при загрузке конфигурации: {e}", file=sys.stderr)
        sys.exit(1)

def main():
    parser = argparse.ArgumentParser(
        description="Скрипт для генерации хэша Argon2id (пароль и соль передаются в Base64)."
    )
    parser.add_argument(
        "-p", "--password", 
        help="Пароль в формате Base64 (если не указан, будет запрошен безопасный ввод)"
    )
    parser.add_argument(
        "-s", "--salt",
        required=True,
        help="Соль для хэширования в формате Base64 (обязательный параметр)"
    )
    parser.add_argument(
        "-c", "--config",
        default="settings.json",
        help="Путь к файлу конфигурации JSON (по умолчанию: settings.json)"
    )
    
    args = parser.parse_args()

    # Загрузка настроек из JSON
    settings = load_settings(args.config)

    # Получение пароля (ожидается строка Base64)
    if args.password:
        password_b64 = args.password
    else:
        password_b64 = getpass.getpass("Введите пароль (в формате Base64): ")

    # Декодирование пароля из Base64 в байты
    try:
        password_bytes = base64.b64decode(password_b64)
    except Exception as e:
        print(f"Ошибка декодирования пароля из Base64: {e}", file=sys.stderr)
        sys.exit(1)

    # Декодирование соли из Base64 в байты
    try:
        salt_bytes = base64.b64decode(args.salt)
    except Exception as e:
        print(f"Ошибка декодирования соли из Base64: {e}", file=sys.stderr)
        sys.exit(1)

    # Вычисление хэша Argon2id
    try:
        raw_hash = hash_secret_raw(
            secret=password_bytes,  # Передаем декодированные байты
            salt=salt_bytes,
            time_cost=settings["iterations"],
            memory_cost=settings["memory_size"],
            parallelism=settings["parallelism"],
            hash_len=HASH_LENGTH,
            type=Type.ID
        )
    except Exception as e:
        print(f"Ошибка при вычислении хэша Argon2id: {e}", file=sys.stderr)
        sys.exit(1)

    # Кодирование полученного хэша в Base64
    b64_hash = base64.b64encode(raw_hash).decode("utf-8")
    print(b64_hash)

if __name__ == "__main__":
    main()