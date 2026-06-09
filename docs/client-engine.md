# Клиент.Engine

## Назначение

Консольный рабочий процесс для выполнения фоновых задач и обработки очереди сообщений.

## Компоненты

- `Program.cs` — точка входа, настройка DI, запуск хост-сервиса.
- `QueueProcessorWorker` — фоновый сервис для обработки очереди сообщений.
- `JwtToken` — модель для хранения JWT-кредиentials.
- `JwtHelper` — общий вспомогательный класс для операций с JWT (из `Common`).

## Конфигурация

Настраивается через `appsettings.json` и `appsettings.Development.json`.

Ключевые настройки:
- Строка подключения к очереди сообщений
- Эндпоинт API
- JWT-кредиentials для аутентификации сервиса

## Запуск

```bash
dotnet run --project Client.Engine
```

As Windows service (production):
```bash
sc create DataGuardClientEngine binPath= "C:\path\to\Client.Engine.exe"