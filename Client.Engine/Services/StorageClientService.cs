using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Client.Engine.Helpers;
using Client.Engine.Interfaces;
using Client.Engine.Models;
using Contracts.Protos.Security;
using Contracts.Protos.Storage;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StorageGrpc = Contracts.Protos.Storage.StorageService;

namespace Client.Engine.Services
{
    public class StorageClientService : IStorageService
    {
        private const int MaxChunkSize = 1048576;
        private const long MaxFileSize = 5368709120L;

        private readonly ILogger<StorageClientService> _logger;
        private readonly StorageGrpc.StorageServiceClient _storageClient;
        private readonly Contracts.Protos.Security.SecurityService.SecurityServiceClient _securityClient;
        private readonly IJwtTokenProvider _jwtTokenProvider;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public StorageClientService(
            ILogger<StorageClientService> logger,
            StorageGrpc.StorageServiceClient storageClient,
            Contracts.Protos.Security.SecurityService.SecurityServiceClient securityClient,
            IJwtTokenProvider jwtTokenProvider,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _storageClient = storageClient;
            _securityClient = securityClient;
            _jwtTokenProvider = jwtTokenProvider;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        private async Task<string> GetNonceAsync()
        {
            _logger.LogTrace("Getting nonce for storage operation");
            var response = await _securityClient.GetNonceAsync(new NonceRequest());
            if (response.Status != 200)
            {
                _logger.LogError("Failed to get nonce: {Message}", response.Message);
                throw new InvalidOperationException($"Не удалось получить nonce: {response.Message}");
            }
            return response.NonceToken;
        }

        private async Task<Metadata> GetAuthHeadersAsync()
        {
            var token = await _jwtTokenProvider.GetOrRefreshTokenAsync();
            return new Metadata { { "Authorization", $"Bearer {token}" } };
        }

        public async Task<StorageUploadResult> UploadFileAsync(Stream fileStream, string fileName, string filePath, Dictionary<string, string>? metadata = null)
        {
            try
            {
                StorageValidationHelper.ValidateFileName(fileName);
                StorageValidationHelper.ValidatePath(filePath);

                _logger.LogInformation("Uploading file {FileName} to {FilePath}", fileName, filePath);

                var headers = await GetAuthHeadersAsync();
                using var call = _storageClient.UploadFile(headers);

                var fileMetadata = new FileMetadata
                {
                    FileName = fileName,
                    FilePath = filePath
                };

                if (metadata != null)
                {
                    foreach (var kvp in metadata)
                    {
                        fileMetadata.Metadata[kvp.Key] = kvp.Value;
                    }
                }

                var firstMessage = new UploadFileRequest { Metadata = fileMetadata };
                await call.RequestStream.WriteAsync(firstMessage);

                var buffer = new byte[MaxChunkSize];
                long totalBytes = 0;
                int bytesRead;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > MaxFileSize)
                    {
                        throw new ArgumentException("Размер файла превышает допустимый лимит (5 ГБ).");
                    }

                    var chunk = new UploadFileRequest
                    {
                        Chunk = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead)
                    };
                    await call.RequestStream.WriteAsync(chunk);
                }

                await call.RequestStream.CompleteAsync();
                var response = await call.ResponseAsync;

                return new StorageUploadResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    FileId = response.FileId
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during file upload");
                return new StorageUploadResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during file upload");
                return new StorageUploadResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file upload");
                return new StorageUploadResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageDownloadResult> GetFileAsync(Guid fileId)
        {
            try
            {
                _logger.LogInformation("Downloading file {FileId}", fileId);

                var headers = await GetAuthHeadersAsync();
                var request = new GetFileRequest { FileId = fileId.ToString() };
                using var call = _storageClient.GetFile(request, headers);

                var memoryStream = new MemoryStream();
                string? fileName = null;
                string? filePath = null;
                long size = 0;
                var metadata = new Dictionary<string, string>();

                await foreach (var response in call.ResponseStream.ReadAllAsync())
                {
                    switch (response.DataCase)
                    {
                        case GetFileResponse.DataOneofCase.Metadata:
                            fileName = response.Metadata.FileName;
                            filePath = response.Metadata.FilePath;
                            size = response.Metadata.Size;
                            foreach (var kvp in response.Metadata.Metadata)
                            {
                                metadata[kvp.Key] = kvp.Value;
                            }
                            break;
                        case GetFileResponse.DataOneofCase.Chunk:
                            await memoryStream.WriteAsync(response.Chunk.Memory);
                            break;
                    }
                }

                memoryStream.Position = 0;
                return new StorageDownloadResult
                {
                    Success = true,
                    Message = "OK",
                    Content = memoryStream,
                    FileName = fileName,
                    FilePath = filePath,
                    Size = size,
                    Metadata = metadata
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during file download");
                return new StorageDownloadResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file download");
                return new StorageDownloadResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> UpdateFileAsync(Guid fileId, long offset, byte[]? data = null, long? eraseSize = null)
        {
            try
            {
                if (offset < 0)
                    throw new ArgumentException("Некорректный offset для обновления.");

                if (data == null && eraseSize == null)
                    throw new ArgumentException("Необходимо указать данные для записи или размер стирания.");

                if (eraseSize.HasValue && eraseSize.Value < 0)
                    throw new ArgumentException("Некорректный offset для стирания.");

                _logger.LogInformation("Updating file {FileId} at offset {Offset}", fileId, offset);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new UpdateFileRequest { NonceToken = nonce };

                if (data != null)
                {
                    request.Update = new UpdateOperation
                    {
                        Offset = offset,
                        Data = Google.Protobuf.ByteString.CopyFrom(data)
                    };
                }
                else if (eraseSize.HasValue)
                {
                    request.Erase = new ErraseOperation
                    {
                        Offset = offset,
                        Size = eraseSize.Value
                    };
                }

                var response = await _storageClient.UpdateFileAsync(request, headers);

                return new StorageOperationResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during file update");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during file update");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file update. Exception type: {ExceptionType}", ex.GetType().FullName);
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> DeleteFileAsync(Guid fileId)
        {
            try
            {
                _logger.LogInformation("Deleting file {FileId}", fileId);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new DeleteFileRequest
                {
                    FileId = fileId.ToString(),
                    NonceToken = nonce
                };

                var response = await _storageClient.DeleteFileAsync(request, headers);

                return new StorageOperationResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during file delete");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file delete");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> MoveFileAsync(Guid fileId, string newPath)
        {
            try
            {
                StorageValidationHelper.ValidatePath(newPath);

                _logger.LogInformation("Moving file {FileId} to {NewPath}", fileId, newPath);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new MoveFileRequest
                {
                    FileId = fileId.ToString(),
                    NewPath = newPath,
                    NonceToken = nonce
                };

                var response = await _storageClient.MoveFileAsync(request, headers);

                return new StorageOperationResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during file move");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during file move");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file move");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageCopyResult> CopyFileAsync(Guid fileId, string newPath)
        {
            try
            {
                StorageValidationHelper.ValidatePath(newPath);

                _logger.LogInformation("Copying file {FileId} to {NewPath}", fileId, newPath);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new CopyFileRequest
                {
                    FileId = fileId.ToString(),
                    NewPath = newPath,
                    NonceToken = nonce
                };

                var response = await _storageClient.CopyFileAsync(request, headers);

                return new StorageCopyResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    NewFileId = response.NewFileId
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during file copy");
                return new StorageCopyResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during file copy");
                return new StorageCopyResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file copy");
                return new StorageCopyResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> RenameFileAsync(Guid fileId, string newName)
        {
            try
            {
                StorageValidationHelper.ValidateFileName(newName);

                _logger.LogInformation("Renaming file {FileId} to {NewName}", fileId, newName);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new RenameFileRequest
                {
                    FileId = fileId.ToString(),
                    NewName = newName,
                    NonceToken = nonce
                };

                var response = await _storageClient.RenameFileAsync(request, headers);

                return new StorageOperationResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during file rename");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during file rename");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file rename");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageDirectoryResult> NewDirectoryAsync(string directoryPath)
        {
            try
            {
                StorageValidationHelper.ValidateDirectoryPath(directoryPath);

                _logger.LogInformation("Creating directory {DirectoryPath}", directoryPath);

                var headers = await GetAuthHeadersAsync();

                var request = new NewDirectoryRequest
                {
                    DirectoryPath = directoryPath
                };

                var response = await _storageClient.NewDirectoryAsync(request, headers);

                return new StorageDirectoryResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    DirectoryId = response.DirectoryId
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during directory creation");
                return new StorageDirectoryResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during directory creation");
                return new StorageDirectoryResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during directory creation");
                return new StorageDirectoryResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> RenameDirectoryAsync(Guid directoryId, string newName)
        {
            try
            {
                StorageValidationHelper.ValidateDirectoryName(newName);

                _logger.LogInformation("Renaming directory {DirectoryId} to {NewName}", directoryId, newName);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new RenameDirectoryRequest
                {
                    DirectoryId = directoryId.ToString(),
                    NewName = newName,
                    NonceToken = nonce
                };

                var response = await _storageClient.RenameDirectoryAsync(request, headers);

                return new StorageOperationResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during directory rename");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during directory rename");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during directory rename");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> DeleteDirectoryAsync(Guid directoryId, bool recursive = false)
        {
            try
            {
                _logger.LogInformation("Deleting directory {DirectoryId}, recursive={Recursive}", directoryId, recursive);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new DeleteDirectoryRequest
                {
                    DirectoryId = directoryId.ToString(),
                    Recursive = recursive,
                    NonceToken = nonce
                };

                var response = await _storageClient.DeleteDirectoryAsync(request, headers);

                return new StorageOperationResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during directory delete");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during directory delete");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> MoveDirectoryAsync(Guid directoryId, string newPath)
        {
            try
            {
                StorageValidationHelper.ValidatePath(newPath);

                _logger.LogInformation("Moving directory {DirectoryId} to {NewPath}", directoryId, newPath);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new MoveDirectoryRequest
                {
                    DirectoryId = directoryId.ToString(),
                    NewPath = newPath,
                    NonceToken = nonce
                };

                var response = await _storageClient.MoveDirectoryAsync(request, headers);

                return new StorageOperationResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during directory move");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during directory move");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during directory move");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageDirectoryCopyResult> CopyDirectoryAsync(Guid directoryId, string newPath, bool recursive = false)
        {
            try
            {
                StorageValidationHelper.ValidatePath(newPath);

                _logger.LogInformation("Copying directory {DirectoryId} to {NewPath}, recursive={Recursive}", directoryId, newPath, recursive);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new CopyDirectoryRequest
                {
                    DirectoryId = directoryId.ToString(),
                    NewPath = newPath,
                    Recursive = recursive,
                    NonceToken = nonce
                };

                var response = await _storageClient.CopyDirectoryAsync(request, headers);

                return new StorageDirectoryCopyResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    NewDirectoryId = response.NewDirectoryId
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during directory copy");
                return new StorageDirectoryCopyResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during directory copy");
                return new StorageDirectoryCopyResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during directory copy");
                return new StorageDirectoryCopyResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageMetadataResult> GetMetadataAsync(Guid fileId)
        {
            try
            {
                _logger.LogInformation("Getting metadata for file {FileId}", fileId);

                var headers = await GetAuthHeadersAsync();

                var request = new GetMetadataRequest
                {
                    FileId = fileId.ToString()
                };

                var response = await _storageClient.GetMetadataAsync(request, headers);

                var metadata = new Dictionary<string, string>();
                if (response.Metadata != null)
                {
                    foreach (var kvp in response.Metadata.Metadata)
                    {
                        metadata[kvp.Key] = kvp.Value;
                    }
                }

                return new StorageMetadataResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    FileId = response.Metadata?.FileId,
                    FileName = response.Metadata?.FileName,
                    FilePath = response.Metadata?.FilePath,
                    Size = response.Metadata?.Size ?? 0,
                    Metadata = metadata
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during metadata get");
                return new StorageMetadataResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during metadata get");
                return new StorageMetadataResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> UpdateMetadataAsync(Guid fileId, Dictionary<string, string> metadata)
        {
            try
            {
                StorageValidationHelper.ValidateMetadata(metadata);

                _logger.LogInformation("Updating metadata for file {FileId}", fileId);

                var headers = await GetAuthHeadersAsync();

                var request = new UpdateMetadataRequest
                {
                    FileId = fileId.ToString()
                };

                foreach (var kvp in metadata)
                {
                    request.Metadata[kvp.Key] = kvp.Value;
                }

                var response = await _storageClient.UpdateMetadataAsync(request, headers);

                return new StorageOperationResult
                {
                    Success = response.Success,
                    Message = response.Message
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during metadata update");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during metadata update");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during metadata update");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageListResult> ListDirectoryAsync(Guid directoryId, bool recursive = false)
        {
            try
            {
                _logger.LogInformation("Listing directory {DirectoryId}, recursive={Recursive}", directoryId, recursive);

                var headers = await GetAuthHeadersAsync();

                var request = new ListDirectoryRequest
                {
                    DirectoryId = directoryId.ToString(),
                    Recursive = recursive
                };

                var response = await _storageClient.ListDirectoryAsync(request, headers);

                var items = new List<StorageFileItem>();
                foreach (var item in response.Items)
                {
                    var metadata = new Dictionary<string, string>();
                    foreach (var kvp in item.Metadata)
                    {
                        metadata[kvp.Key] = kvp.Value;
                    }

                    items.Add(new StorageFileItem
                    {
                        FileId = item.FileId,
                        FileName = item.FileName,
                        FilePath = item.FilePath,
                        Size = item.Size,
                        Metadata = metadata
                    });
                }

                return new StorageListResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    Items = items
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during directory listing");
                return new StorageListResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during directory listing");
                return new StorageListResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageLinkResult> GenerateLinkAsync(Guid fileId, string[]? groups = null, string[]? users = null, int ttlSeconds = 86400)
        {
            try
            {
                StorageValidationHelper.ValidateTtl(ttlSeconds);

                _logger.LogInformation("Generating link for file {FileId}", fileId);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new GenerateLinkRequest
                {
                    FileId = fileId.ToString(),
                    TtlSeconds = ttlSeconds,
                    NonceToken = nonce
                };

                if (groups != null)
                {
                    request.Groups.AddRange(groups);
                }

                if (users != null)
                {
                    request.Users.AddRange(users);
                }

                var response = await _storageClient.GenerateLinkAsync(request, headers);

                return new StorageLinkResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    Link = response.Link
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during link generation");
                return new StorageLinkResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during link generation");
                return new StorageLinkResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during link generation");
                return new StorageLinkResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageLinkResult> GenerateDirectLinkAsync(Guid fileId, string[]? groups = null, string[]? users = null, int ttlSeconds = 86400)
        {
            try
            {
                StorageValidationHelper.ValidateTtl(ttlSeconds);

                _logger.LogInformation("Generating direct link for file {FileId}", fileId);

                var nonce = await GetNonceAsync();
                var headers = await GetAuthHeadersAsync();

                var request = new GenerateLinkRequest
                {
                    FileId = fileId.ToString(),
                    TtlSeconds = ttlSeconds,
                    NonceToken = nonce
                };

                if (groups != null)
                {
                    request.Groups.AddRange(groups);
                }

                if (users != null)
                {
                    request.Users.AddRange(users);
                }

                var response = await _storageClient.GenerateDirectLinkAsync(request, headers);

                return new StorageLinkResult
                {
                    Success = response.Success,
                    Message = response.Message,
                    Link = response.Link
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error during direct link generation");
                return new StorageLinkResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error during direct link generation");
                return new StorageLinkResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during direct link generation");
                return new StorageLinkResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageDownloadResult> DownloadFileViaLinkAsync(string token)
        {
            try
            {
                _logger.LogInformation("Downloading file via link token");

                var storageUrl = _configuration["Grpc:StorageUrl"] ?? throw new InvalidOperationException("Storage URL not configured");
                var baseUrl = storageUrl.Replace("https://", "http://");

                var response = await _httpClient.GetAsync($"{baseUrl}/storage/links/{token}", HttpCompletionOption.ResponseHeadersRead);

                if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    return new StorageDownloadResult { Success = false, Message = "Ссылка истекла." };
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new StorageDownloadResult { Success = false, Message = "Ссылка не найдена." };
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStreamAsync();
                var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');

                return new StorageDownloadResult
                {
                    Success = true,
                    Message = "OK",
                    Content = content,
                    FileName = fileName,
                    Size = response.Content.Headers.ContentLength ?? 0
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during file download via link");
                return new StorageDownloadResult { Success = false, Message = $"Ошибка сервера: {ex.Message}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file download via link");
                return new StorageDownloadResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageDownloadResult> DownloadFileViaDirectLinkAsync(string token)
        {
            try
            {
                _logger.LogInformation("Downloading file via direct link token");

                var storageUrl = _configuration["Grpc:StorageUrl"] ?? throw new InvalidOperationException("Storage URL not configured");
                var baseUrl = storageUrl.Replace("https://", "http://");

                var response = await _httpClient.GetAsync($"{baseUrl}/storage/direct/{token}", HttpCompletionOption.ResponseHeadersRead);

                if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    return new StorageDownloadResult { Success = false, Message = "Ссылка истекла." };
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new StorageDownloadResult { Success = false, Message = "Ссылка не найдена." };
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStreamAsync();
                var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');

                return new StorageDownloadResult
                {
                    Success = true,
                    Message = "OK",
                    Content = content,
                    FileName = fileName,
                    Size = response.Content.Headers.ContentLength ?? 0
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during file download via direct link");
                return new StorageDownloadResult { Success = false, Message = $"Ошибка сервера: {ex.Message}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file download via direct link");
                return new StorageDownloadResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }
    }
}
