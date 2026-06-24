using Server.Storage.Interfaces;

namespace Server.Storage.Services;

public class StoragePathValidator : IStoragePathValidator
{
    private const int MaxPathLength = 4096;
    private const int MaxDepth = 64;

    public bool TryNormalizePath(string input, out string normalizedPath, out string? error)
    {
        normalizedPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Путь не может быть пустым.";
            return false;
        }

        if (input.Length > MaxPathLength)
        {
            error = "Превышена максимальная длина пути.";
            return false;
        }

        if (Path.IsPathRooted(input) && input.Contains(':'))
        {
            error = "Абсолютные пути не допускаются.";
            return false;
        }

        string normalized = input.Replace('\\', '/');

        while (normalized.Contains("//"))
            normalized = normalized.Replace("//", "/");

        if (normalized.StartsWith('/'))
            normalized = normalized.TrimStart('/');

        if (normalized.EndsWith('/'))
            normalized = normalized.TrimEnd('/');

        if (string.IsNullOrEmpty(normalized))
        {
            error = "Путь не может быть пустым после нормализации.";
            return false;
        }

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length > MaxDepth)
        {
            error = "Превышена максимальная глубина вложенности.";
            return false;
        }

        foreach (string part in parts)
        {
            if (part == "." || part == "..")
            {
                error = "Path traversal не допускается.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(part))
            {
                error = "Компонент пути не может быть пустым.";
                return false;
            }
        }

        normalizedPath = "/" + string.Join("/", parts);
        return true;
    }

    public bool IsPathInside(string parent, string child)
    {
        string normalizedParent = parent.TrimEnd('/') + "/";
        string normalizedChild = child.TrimEnd('/') + "/";
        return normalizedChild.StartsWith(normalizedParent, StringComparison.Ordinal);
    }

    public string GetFileName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        int lastSlash = path.LastIndexOf('/');
        return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
    }

    public string Combine(string parent, string child)
    {
        string p = parent.TrimEnd('/');
        string c = child.TrimStart('/');
        return $"{p}/{c}";
    }

    public string GetParentPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";

        string trimmed = path.TrimEnd('/');
        int lastSlash = trimmed.LastIndexOf('/');
        return lastSlash > 0 ? trimmed[..lastSlash] : "/";
    }
}
