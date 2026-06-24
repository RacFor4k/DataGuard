namespace Client.Engine.Models
{
    public class StorageOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class StorageUploadResult : StorageOperationResult
    {
        public string? FileId { get; set; }
    }

    public class StorageDownloadResult : StorageOperationResult
    {
        public Stream? Content { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public string? LocalPath { get; set; }
        public long Size { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    public class StorageCopyResult : StorageOperationResult
    {
        public string? NewFileId { get; set; }
    }

    public class StorageDirectoryResult : StorageOperationResult
    {
        public string? DirectoryId { get; set; }
    }

    public class StorageDirectoryCopyResult : StorageOperationResult
    {
        public string? NewDirectoryId { get; set; }
    }

    public class StorageMetadataResult : StorageOperationResult
    {
        public string? FileId { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public long Size { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    public class StorageListResult : StorageOperationResult
    {
        public List<StorageFileItem> Items { get; set; } = new();
    }

    public class StorageFileItem
    {
        public string? FileId { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public long Size { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    public class StorageLinkResult : StorageOperationResult
    {
        public string? Link { get; set; }
    }
}
