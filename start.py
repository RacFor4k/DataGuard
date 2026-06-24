#!/usr/bin/env python3
import os
import sys
import time
import socket
import secrets
import signal
import threading
import subprocess
import base64
import importlib
from datetime import datetime

# ─── Конфигурация ────────────────────────────────────────────────────────────────

SERVICES = {
    "Auth": {
        "name": "Server.Auth",
        "project": "Server.Auth/Server.Auth.csproj",
        "port": 7203,
        "profile": "https",
    },
    "Storage": {
        "name": "Server.Storage",
        "project": "Server.Storage/Server.Storage.csproj",
        "port": 7122,
        "profile": "https",
    },
    "Client": {
        "name": "Client.Engine",
        "project": "Client.Engine/Client.Engine.csproj",
        "port": 0,
        "profile": None,
    },
    "UI": {
        "name": "Client.UI",
        "project": "Client.UI/Client.UI.csproj",
        "port": 0,
        "profile": None,
    },
}

RUNNING_PROCESSES = []
EVENT_SHUTDOWN = threading.Event()

# Цвета для вывода в консоль
ANSI_CYAN = "\033[96m"
ANSI_GREEN = "\033[92m"
ANSI_YELLOW = "\033[93m"
ANSI_RED = "\033[91m"
ANSI_GRAY = "\033[90m"
ANSI_RESET = "\033[0m"

# Включение поддержки ANSI цветов в Windows 10+
if sys.platform == "win32":
    os.system("")

# ─── Логирование ─────────────────────────────────────────────────────────────────


def print_log(message, level="INFO"):
    timestamp = datetime.now().strftime("%H:%M:%S")
    color = ANSI_RESET
    if level == "INFO":
        color = ANSI_CYAN
    elif level == "OK":
        color = ANSI_GREEN
    elif level == "WARN":
        color = ANSI_YELLOW
    elif level == "ERROR":
        color = ANSI_RED

    print(f"[{timestamp}] {color}{level:5}{ANSI_RESET} {message}", flush=True)


def print_step(message, step):
    print("\n" + "═" * 60)
    print(f"  Step {step} │ {message}")
    print("═" * 60 + "\n", flush=True)


def wait_for_port(port, timeout_seconds=60):
    start_time = time.time()
    while time.time() - start_time < timeout_seconds:
        try:
            with socket.create_connection(("localhost", port), timeout=2):
                return True
        except (socket.timeout, ConnectionRefusedError, OSError):
            pass
        time.sleep(0.5)
    return False


# ─── Остановка процессов ─────────────────────────────────────────────────────────


def clean_up():
    if not RUNNING_PROCESSES:
        return
    print_log("Stopping all background processes...", "WARN")

    for name, proc in RUNNING_PROCESSES:
        if proc.poll() is None:  # Процесс всё ещё запущен
            try:
                if sys.platform == "win32":
                    # Рекурсивно убиваем всё дерево дочерних dotnet-процессов на Windows
                    subprocess.run(
                        ["taskkill", "/F", "/T", "/PID", str(proc.pid)],
                        stdout=subprocess.DEVNULL,
                        stderr=subprocess.DEVNULL,
                    )
                else:
                    # Убиваем группу процессов на Linux/macOS
                    os.killpg(os.getpgid(proc.pid), signal.SIGKILL)
            except Exception as e:
                try:
                    proc.kill()
                except Exception:
                    pass
    RUNNING_PROCESSES.clear()
    print_log("All processes stopped", "OK")


def handle_sigint(signum, frame):
    EVENT_SHUTDOWN.set()


# Регистрируем обработку Ctrl+C
signal.signal(signal.SIGINT, handle_sigint)

# ─── Первоначальная настройка ────────────────────────────────────────────────────


def initialize_project():
    print_step("Initial project setup", 0)

    # Проверка .NET SDK
    try:
        dotnet_version = subprocess.check_output(
            ["dotnet", "--version"], text=True, stderr=subprocess.DEVNULL
        ).strip()
        print_log(f".NET SDK {dotnet_version} — OK", "OK")
    except Exception:
        print_log(".NET SDK not found. Please install .NET SDK.", "ERROR")
        sys.exit(1)

    # Проверка Docker
    try:
        subprocess.check_output(
            ["docker", "--version"], text=True, stderr=subprocess.DEVNULL
        )
        print_log("Docker — OK", "OK")
    except Exception:
        print_log("Docker not found. Please install Docker Desktop.", "ERROR")
        sys.exit(1)

    # Проверка и установка argon2-cffi
    python_available = True
    try:
        import argon2  # noqa: F401

        print_log("argon2-cffi — OK", "OK")
    except ImportError:
        print_log(
            "argon2-cffi is not installed. Attempting automatic installation...", "WARN"
        )
        try:
            subprocess.run(
                [sys.executable, "-m", "pip", "install", "argon2-cffi"], check=True
            )
            print_log("argon2-cffi installed — OK", "OK")
        except Exception:
            print_log("Failed to install argon2-cffi.", "WARN")
            python_available = False

    # Генерация .env
    env_path = os.path.join(os.getcwd(), ".env")
    if not os.path.exists(env_path):
        print_log("Generating .env for Docker Compose and AppSettings...", "INFO")
        pg_pass = secrets.token_hex(32)
        rd_pass = secrets.token_hex(32)
        minio_us = "dataguardadmin"
        minio_pw = secrets.token_urlsafe(24)

        # Раздельные JWT ключи для Auth и Storage (512 бит)
        jwt_secret = secrets.token_hex(64)
        jwt_key = secrets.token_hex(64)

        # Мастер-ключ, соль и nonce-ключ в Base64 для криптографии .NET
        master_key = secrets.token_urlsafe(32)
        nonce_key = base64.b64encode(secrets.token_bytes(32)).decode("utf-8")
        master_key_salt = base64.b64encode(secrets.token_bytes(16)).decode("utf-8")

        # Динамическая генерация Argon2id хэша для мастер-ключа
        master_key_hash = ""
        try:
            argon2_mod = importlib.import_module("argon2")
            # Настройки совпадают с вашими параметрами в appsettings: m=19456, t=3, p=1
            ph = argon2_mod.PasswordHasher(
                time_cost=3, memory_cost=19456, parallelism=1, hash_len=32
            )
            master_key_hash = ph.hash(master_key)
        except Exception:
            # Дефолтный корректный хэш на случай отсутствия установленного модуля в рантайме
            master_key_hash = "$argon2id$v=19$m=19456,t=3,p=1$c2FsdHNhbHRzYWx0c2FsdA$VGVtcG9yYXJ5SGFzaFZhbHVlRm9yRGV2ZWxvcG1lbnQ"

        try:
            with open(env_path, "w", encoding="utf-8") as f:
                f.write(f"DB_USER=dataguard\n")
                f.write(f"DB_PASSWORD={pg_pass}\n")
                f.write(f"DB_NAME=dataguard\n")
                f.write(f"REDIS_PASSWORD={rd_pass}\n")
                f.write(f"MINIO_ROOT_USER={minio_us}\n")
                f.write(f"MINIO_ROOT_PASSWORD={minio_pw}\n")
                f.write(f"JWT_SECRET={jwt_secret}\n")
                f.write(f"JWT_KEY={jwt_key}\n")
                f.write(f"COMPANY_MANAGER_MASTER_KEY={master_key}\n")
                # Одинарные кавычки предотвращают интерполяцию знаков $ в Docker Compose
                f.write(f"COMPANY_MANAGER_MASTER_KEY_HASH='{master_key_hash}'\n")
                f.write(f"SECURITY_NONCE_SECRET_KEY={nonce_key}\n")
                f.write(f"SECURITY_MASTER_KEY_SALT={master_key_salt}\n")
            print_log(f".env created ({env_path})", "OK")
        except Exception as e:
            print_log(f"Failed to create .env: {e}", "ERROR")
            sys.exit(1)
    else:
        print_log(".env already exists — skipping", "WARN")

    print_log("Setup complete. Now run: python start.py", "OK")
    sys.exit(0)


# ─── Запуск инфраструктуры ───────────────────────────────────────────────────────


def start_infrastructure():
    print_step("Starting infrastructure (Docker Compose)", 1)
    try:
        print_log("docker compose up -d...", "INFO")
        subprocess.run(["docker", "compose", "up", "-d"], check=True)
    except Exception as e:
        print_log(f"Docker Compose up failed: {e}", "ERROR")
        sys.exit(1)

    print_log("Waiting for PostgreSQL (5432)...", "INFO")
    if not wait_for_port(5432):
        print_log("PostgreSQL did not respond in time", "ERROR")
        sys.exit(1)
    print_log("PostgreSQL — ready", "OK")

    print_log("Waiting for Redis (6379)...", "INFO")
    if not wait_for_port(6379):
        print_log("Redis did not respond in time", "ERROR")
        sys.exit(1)
    print_log("Redis — ready", "OK")

    print_log("Waiting for MinIO (9000)...", "INFO")
    if not wait_for_port(9000):
        print_log("MinIO did not respond in time", "ERROR")
        sys.exit(1)
    print_log("MinIO — ready", "OK")


# ─── Сборка проектов ─────────────────────────────────────────────────────────────


def build_projects():
    print_step("Building projects (dotnet build)", "B")
    print_log("Running dotnet build for the solution...", "INFO")
    try:
        subprocess.run(["dotnet", "build"], check=True)
        print_log("Build completed successfully", "OK")
    except Exception as e:
        print_log(f"Build failed: {e}", "ERROR")
        sys.exit(1)


# ─── Применение миграций EF Core ─────────────────────────────────────────────────


def invoke_migrations():
    print_step("Applying EF Core migrations", 2)
    migrations = [
        {"project": SERVICES["Auth"]["project"], "name": SERVICES["Auth"]["name"]},
        {
            "project": SERVICES["Storage"]["project"],
            "name": SERVICES["Storage"]["name"],
        },
    ]

    for m in migrations:
        print_log(f"Migrations: {m['name']}...", "INFO")
        try:
            subprocess.run(
                ["dotnet", "ef", "database", "update", "--project", m["project"]],
                check=True,
            )
            print_log(f"{m['name']} — migrations applied", "OK")
        except Exception as e:
            print_log(f"Migrations for {m['name']} failed: {e}", "ERROR")
            sys.exit(1)


# ─── Асинхронное чтение логов в реальном времени ──────────────────────────────────


def log_reader(stream, prefix, color_ansi):
    try:
        for line in iter(stream.readline, ""):
            if line:
                cleaned_line = line.rstrip()
                if cleaned_line:
                    # Вывод вида: [Server.Auth] Информация из лога
                    print(
                        f"[{color_ansi}{prefix}{ANSI_RESET}] {cleaned_line}", flush=True
                    )
    except Exception:
        pass
    finally:
        stream.close()


# ─── Запуск одного .NET сервиса ──────────────────────────────────────────────────


def start_dotnet_service(service_cfg):
    name = service_cfg["name"]
    project = service_cfg["project"]
    profile = service_cfg["profile"]
    port = service_cfg["port"]

    print_log(f"Starting {name}...", "INFO")

    args = ["dotnet", "run", "--project", project]
    if profile:
        args += ["--launch-profile", profile]
    args += ["--no-build"]

    env = os.environ.copy()
    env["DOTNET_ENVIRONMENT"] = "Development"

    try:
        # setsid обеспечивает новую группу процессов в UNIX, упрощая терминирование дерева потомков
        preexec = os.setsid if sys.platform != "win32" else None
        proc = subprocess.Popen(
            args,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            encoding="utf-8",  # Явно указываем UTF-8 для декодирования вывода .NET
            errors="replace",  # Заменяем некорректные байты, чтобы скрипт не падал
            env=env,
            bufsize=1,  # Построчный буфер
            preexec_fn=preexec,
        )
    except Exception as e:
        print_log(f"Failed to start process {name}: {e}", "ERROR")
        sys.exit(1)

    RUNNING_PROCESSES.append((name, proc))

    # Запускаем фоновые потоки чтения stdout и stderr, чтобы буферы dotnet не переполнялись
    t_out = threading.Thread(
        target=log_reader, args=(proc.stdout, name, ANSI_GRAY), daemon=True
    )
    t_err = threading.Thread(
        target=log_reader, args=(proc.stderr, f"{name}_ERR", ANSI_RED), daemon=True
    )
    t_out.start()
    t_err.start()

    if port > 0:
        if wait_for_port(port, timeout_seconds=60):
            print_log(f"{name} — ready (:{port})", "OK")
        else:
            print_log(f"{name} did not respond on port {port} in time", "WARN")
    else:
        time.sleep(3)
        if proc.poll() is None:
            print_log(f"{name} — running (PID {proc.pid})", "OK")
        else:
            print_log(f"{name} crashed during startup", "ERROR")
            sys.exit(1)


# ─── Точка входа ─────────────────────────────────────────────────────────────────


def main():
    # Парсим аргументы (поддерживает и -style, и --style)
    args = [a.lower() for a in sys.argv[1:]]
    setup_flag = "--setup" in args or "-setup" in args
    stop_flag = "--stop" in args or "-stop" in args
    ui_flag = "--ui" in args or "-ui" in args
    migrate_flag = "--migrate" in args or "-migrate" in args
    build_flag = "--build" in args or "-build" in args
    skip_infra = (
        "--skipinfra" in args
        or "-skipinfra" in args
        or "--skip-infra" in args
        or "-skip-infra" in args
    )
    skip_migrations = (
        "--skipmigrations" in args
        or "-skipmigrations" in args
        or "--skip-migrations" in args
        or "-skip-migrations" in args
    )

    if stop_flag:
        print_step("Stopping all components", 0)
        try:
            print_log("Stopping Docker Compose...", "INFO")
            subprocess.run(["docker", "compose", "down"], check=True)
        except Exception as e:
            print_log(f"Docker Compose down failed: {e}", "WARN")
        print_log("All components stopped", "OK")
        sys.exit(0)

    if setup_flag:
        initialize_project()

    # Проверка .env перед запуском
    if not os.path.exists(os.path.join(os.getcwd(), ".env")):
        print_log(".env not found. Run setup first: python start.py -Setup", "ERROR")
        sys.exit(1)

    # Обработка команды генерации и применения миграций
    if migrate_flag:
        # Определение имени миграции
        migration_name = None
        for idx, arg in enumerate(sys.argv):
            if arg.lower() in ("-migrate", "--migrate"):
                if idx + 1 < len(sys.argv) and not sys.argv[idx + 1].startswith("-"):
                    migration_name = sys.argv[idx + 1]
                break

        if not migration_name:
            migration_name = f"Auto_{datetime.now().strftime('%Y%m%d_%H%M%S')}"

        # 1. Проверяем, запущена ли БД (порт 5432)
        db_running = False
        try:
            with socket.create_connection(("localhost", 5432), timeout=1):
                db_running = True
        except OSError:
            pass

        started_temp_infra = False
        if not db_running:
            print_log(
                "Database is not running. Starting temporary infrastructure...", "WARN"
            )
            start_infrastructure()
            started_temp_infra = True
        else:
            print_log("Database is already running. Using existing instance.", "OK")

        # 2. Генерируем новые миграции для проектов Auth и Storage
        print_step(f"Generating migrations: {migration_name}", 1)
        projects_to_migrate = [
            {"project": SERVICES["Auth"]["project"], "name": SERVICES["Auth"]["name"]},
            {
                "project": SERVICES["Storage"]["project"],
                "name": SERVICES["Storage"]["name"],
            },
        ]

        for m in projects_to_migrate:
            print_log(f"Generating migration for {m['name']}...", "INFO")
            try:
                subprocess.run(
                    [
                        "dotnet",
                        "ef",
                        "migrations",
                        "add",
                        migration_name,
                        "--project",
                        m["project"],
                    ],
                    check=True,
                )
                print_log(
                    f"Migration '{migration_name}' successfully created for {m['name']}",
                    "OK",
                )
            except subprocess.CalledProcessError:
                print_log(
                    f"Could not generate migration for {m['name']} (it may already exist or contain no changes)",
                    "WARN",
                )
            except Exception as e:
                print_log(
                    f"Unexpected error during migration generation for {m['name']}: {e}",
                    "ERROR",
                )

        # 3. Применяем миграции
        invoke_migrations()

        # 4. Если инфраструктура запускалась временно — выключаем её
        if started_temp_infra:
            print_step("Stopping temporary infrastructure", 3)
            try:
                subprocess.run(["docker", "compose", "down"], check=True)
                print_log("Temporary infrastructure stopped successfully", "OK")
            except Exception as e:
                print_log(f"Failed to stop temporary infrastructure: {e}", "WARN")

        print_log("Migration process completed successfully!", "OK")
        sys.exit(0)

    try:
        # Шаг 1 — Инфраструктура
        if not skip_infra:
            start_infrastructure()
        else:
            print_log("Infrastructure skipped (-SkipInfra)", "WARN")

        # Шаг 1.5 — Сборка проектов (если передан флаг -build)
        if build_flag:
            build_projects()

        # Шаг 2 — Миграции
        if not skip_migrations:
            invoke_migrations()
        else:
            print_log("Migrations skipped (-SkipMigrations)", "WARN")

        # Шаг 3 — Server.Auth
        print_step("Starting Server.Auth (gRPC :7203)", 3)
        start_dotnet_service(SERVICES["Auth"])

        # Шаг 4 — Server.Storage
        print_step("Starting Server.Storage (gRPC+REST :7122)", 4)
        start_dotnet_service(SERVICES["Storage"])

        # Шаг 5 — Client.Engine
        print_step("Starting Client.Engine (Named Pipe)", 5)
        start_dotnet_service(SERVICES["Client"])

        # Шаг 6 — Client.UI (Опционально)
        if ui_flag:
            print_step("Starting Client.UI (Avalonia 11)", 6)
            start_dotnet_service(SERVICES["UI"])

        # ─── Итог ────────────────────────────────────────────────────────────────────
        print("")
        print("═" * 60)
        print(f"  {ANSI_GREEN}DataGuard is running{ANSI_RESET}")
        print("═" * 60)
        print("")
        print("  Services:")
        print("    Server.Auth     → https://localhost:7203  (gRPC)")
        print("    Server.Storage  → https://localhost:7122  (gRPC + REST)")
        print("    Client.Engine   → Named Pipe: DataGuardPipe")
        if ui_flag:
            print("    Client.UI       → Desktop (Avalonia 11)")
        print("")
        print("  Infrastructure:")
        print("    PostgreSQL      → localhost:5432")
        print("    Redis           → localhost:6379")
        print("    MinIO API       → localhost:9000")
        print("    MinIO Console   → http://localhost:9001")
        print("")
        print(
            f"  {ANSI_GRAY}To stop, press Ctrl+C or run: python start.py -Stop{ANSI_RESET}"
        )
        print("")

        # Мониторинг процессов в основном потоке
        while not EVENT_SHUTDOWN.is_set():
            # Проверяем, не упал ли какой-то из процессов
            for name, proc in RUNNING_PROCESSES:
                exit_code = proc.poll()
                if exit_code is not None:
                    print_log(
                        f"Process {name} terminated unexpectedly with code {exit_code}",
                        "ERROR",
                    )
                    EVENT_SHUTDOWN.set()
                    break
            time.sleep(0.5)

    finally:
        # Вызывается при падении процесса, закрытии окна или нажатии Ctrl+C
        clean_up()


if __name__ == "__main__":
    main()
