using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using dotenv.net;

public static class EnvConfigurationHelper
{
    /// <summary>
    /// Загружает файл .env с автоматическим поиском вверх по дереву папок до корня решения.
    /// </summary>
    public static void LoadEnvFile()
    {
        DotEnv.Load(options: new DotEnvOptions(
            probeForEnv: true,         // Включает поиск .env в родительских папках
            probeLevelsToSearch: 6,     // Количество уровней для поиска вверх (6 достаточно для большинства структур)
            trimValues: true,           // Автоматически обрезает пробелы и лишние кавычки вокруг значений
            ignoreExceptions: false     // Игнорирует ошибки, если файл не найден (полезно для продакшена/Docker)
        ));
    }

    /// <summary>
    /// Проходит по конфигурации и заменяет ${VAR_NAME} на значения из переменных окружения.
    /// </summary>
    public static void ResolvePlaceholders(IConfiguration config)
    {
        // Преобразуем в список, чтобы избежать изменения коллекции во время итерации
        foreach (var kvp in config.AsEnumerable().ToList())
        {
            if (kvp.Value == null) continue;
            var resolvedValue = Regex.Replace(kvp.Value, @"\$\{([^}]+)\}", match =>
            {
                var envVarName = match.Groups[1].Value;
                var envValue = Environment.GetEnvironmentVariable(envVarName);
                // Если переменная не найдена в системе, оставляем плейсхолдер нетронутым
                return envValue ?? match.Value;
            });

            if (resolvedValue != kvp.Value)
            {
                config[kvp.Key] = resolvedValue;
            }
        }
    }
}