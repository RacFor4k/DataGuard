namespace Server.Storage.Interfaces;

public interface IStoragePathValidator
{
    bool TryNormalizePath(string input, out string normalizedPath, out string? error);
    bool IsPathInside(string parent, string child);
    string GetFileName(string path);
    string Combine(string parent, string child);
    string GetParentPath(string path);
}
