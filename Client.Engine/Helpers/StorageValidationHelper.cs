using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Engine.Helpers
{
    public static class StorageValidationHelper
    {
        private static readonly HashSet<string> ReservedMetadataKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "storageKey", "ownerId", "physicalPath", "bucketName"
        };

        public static void ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Имя файла не может быть пустым.", nameof(fileName));

            if (fileName.Length > 1024)
                throw new ArgumentException("Имя файла не может превышать 1024 символа.", nameof(fileName));

            if (fileName.Contains('/') || fileName.Contains('\\'))
                throw new ArgumentException("Имя файла не должно содержать разделителей пути.", nameof(fileName));
        }

        public static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь не может быть пустым.", nameof(path));

            if (path.Length > 4096)
                throw new ArgumentException("Путь не может превышать 4096 символов.", nameof(path));

            if (path.Contains(".."))
                throw new ArgumentException("Некорректный путь: Path traversal не допускается.", nameof(path));

            if (path.StartsWith('/'))
                throw new ArgumentException("Путь должен быть относительным.", nameof(path));

            if (path.Contains(':'))
                throw new ArgumentException("Некорректный путь: буквы дисков не допускаются.", nameof(path));
        }

        public static void ValidateMetadata(Dictionary<string, string> metadata)
        {
            if (metadata.Count > 64)
                throw new ArgumentException("Превышено максимальное количество ключей метаданных (64).");

            foreach (var kvp in metadata)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    throw new ArgumentException("Ключ метаданных не может быть пустым.");

                if (kvp.Key.Length > 256)
                    throw new ArgumentException($"Превышена максимальная длина ключа метаданных (256): {kvp.Key.Substring(0, 20)}...");

                if (kvp.Value.Length > 4096)
                    throw new ArgumentException($"Превышена максимальная длина значения метаданных (4096) для ключа '{kvp.Key}'.");

                if (ReservedMetadataKeys.Contains(kvp.Key))
                    throw new ArgumentException($"Ключ метаданных '{kvp.Key}' зарезервирован.");

                if (kvp.Key.StartsWith("__"))
                    throw new ArgumentException($"Ключ метаданных '{kvp.Key}' зарезервирован (префикс '__').");
            }
        }

        public static void ValidateTtl(int ttlSeconds)
        {
            if (ttlSeconds < 1)
                throw new ArgumentException("TTL должен быть положительным.", nameof(ttlSeconds));

            const int maxTtl = 2592000;
            if (ttlSeconds > maxTtl)
                throw new ArgumentException($"TTL не может превышать 30 дней ({maxTtl} секунд).", nameof(ttlSeconds));
        }

        public static void ValidateDirectoryPath(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Путь директории не может быть пустым.", nameof(directoryPath));

            if (directoryPath.Length > 4096)
                throw new ArgumentException("Путь директории не может превышать 4096 символов.", nameof(directoryPath));

            if (directoryPath.Contains(".."))
                throw new ArgumentException("Некорректный путь: Path traversal не допускается.", nameof(directoryPath));

            if (directoryPath.StartsWith('/'))
                throw new ArgumentException("Путь должен быть относительным.", nameof(directoryPath));

            if (directoryPath.Contains(':'))
                throw new ArgumentException("Некорректный путь: буквы дисков не допускаются.", nameof(directoryPath));
        }

        public static void ValidateDirectoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя директории не может быть пустым.", nameof(name));

            if (name.Contains('/') || name.Contains('\\'))
                throw new ArgumentException("Имя директории не должно содержать разделителей пути.", nameof(name));
        }
    }
}
