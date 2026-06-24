using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Client.Engine.Models;

namespace Client.Engine.Interfaces
{
    public interface IStorageService
    {
        Task<StorageUploadResult> UploadFileAsync(Stream fileStream, string fileName, string filePath, Dictionary<string, string>? metadata = null);
        Task<StorageDownloadResult> GetFileAsync(Guid fileId, string localFilePath, CancellationToken ct = default);
        Task<StorageOperationResult> UpdateFileAsync(Guid fileId, long offset, byte[]? data = null, long? eraseSize = null);
        Task<StorageOperationResult> DeleteFileAsync(Guid fileId);
        Task<StorageOperationResult> MoveFileAsync(Guid fileId, string newPath);
        Task<StorageCopyResult> CopyFileAsync(Guid fileId, string newPath);
        Task<StorageOperationResult> RenameFileAsync(Guid fileId, string newName);
        Task<StorageDirectoryResult> NewDirectoryAsync(string directoryPath);
        Task<StorageOperationResult> RenameDirectoryAsync(Guid directoryId, string newName);
        Task<StorageOperationResult> DeleteDirectoryAsync(Guid directoryId, bool recursive = false);
        Task<StorageOperationResult> MoveDirectoryAsync(Guid directoryId, string newPath);
        Task<StorageDirectoryCopyResult> CopyDirectoryAsync(Guid directoryId, string newPath, bool recursive = false);
        Task<StorageMetadataResult> GetMetadataAsync(Guid fileId);
        Task<StorageOperationResult> UpdateMetadataAsync(Guid fileId, Dictionary<string, string> metadata);
        Task<StorageListResult> ListDirectoryAsync(Guid directoryId, bool recursive = false);
        Task<StorageLinkResult> GenerateLinkAsync(Guid fileId, string[]? groups = null, string[]? users = null, int ttlSeconds = 86400);
        Task<StorageLinkResult> GenerateDirectLinkAsync(Guid fileId, string[]? groups = null, string[]? users = null, int ttlSeconds = 86400);
        Task<StorageDownloadResult> DownloadFileViaLinkAsync(string token);
        Task<StorageDownloadResult> DownloadFileViaDirectLinkAsync(string token);
    }
}
