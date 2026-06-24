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

        /// <summary>
        /// Проверяет, что URL использует HTTPS. HTTP запрещён.
        /// </summary>
        private static void ValidateHttpsUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("Некорректный формат URL.");
            if (uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Использование HTTP-протокола запрещено. Только HTTPS.");
        }

        private async Task<string> GetNonceAsync()
        {
            _logger.LogTrace("Получение nonce для операции с хранилищем");
            var response = await _securityClient.GetNonceAsync(new NonceRequest());
            if (response.Status != 200)
            {
                _logger.LogError("Не удалось получить nonce: {Message}", response.Message);
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

                _logger.LogInformation("Загрузка файла {FileName} в {FilePath}", fileName, filePath);

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
                _logger.LogError(ex, "gRPC ошибка при загрузке файла");
                return new StorageUploadResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при загрузке файла");
                return new StorageUploadResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при загрузке файла");
                return new StorageUploadResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageDownloadResult> GetFileAsync(Guid fileId, string localFilePath, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Загрузка файла {FileId} в {LocalPath}", fileId, localFilePath);

                var headers = await GetAuthHeadersAsync();
                var request = new GetFileRequest { FileId = fileId.ToString() };
                var call = _storageClient.GetFile(request, headers: headers, cancellationToken: ct);

                // Пишем напрямую во временный файл
                var tempPath = localFilePath + ".tmp";
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

                string? fileName = null;
                string? filePath = null;
                long size = 0;
                var metadata = new Dictionary<string, string>();

                await foreach (var response in call.ResponseStream.ReadAllAsync(ct))
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
                            await fileStream.WriteAsync(response.Chunk.ToByteArray(), ct);
                            break;
                    }
                }

                await fileStream.FlushAsync(ct);

                // Атомарная замена временного файла на целевой
                File.Move(tempPath, localFilePath, overwrite: true);

                return new StorageDownloadResult
                {
                    Success = true,
                    Message = "OK",
                    LocalPath = localFilePath,
                    FileName = fileName,
                    FilePath = filePath,
                    Size = size,
                    Metadata = metadata
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC ошибка при загрузке файла {FileId}", fileId);
                return new StorageDownloadResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при загрузке файла {FileId}", fileId);
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

                _logger.LogInformation("Обновление файла {FileId} по смещению {Offset}", fileId, offset);

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
                    request.Erase = new EraseOperation
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
                _logger.LogWarning(ex, "Ошибка валидации при обновлении файла");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC ошибка при обновлении файла");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при обновлении файла. Тип: {ExceptionType}", ex.GetType().FullName);
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> DeleteFileAsync(Guid fileId)
        {
            try
            {
                _logger.LogInformation("Удаление файла {FileId}", fileId);

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
                _logger.LogError(ex, "gRPC ошибка при удалении файла");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при удалении файла");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> MoveFileAsync(Guid fileId, string newPath)
        {
            try
            {
                StorageValidationHelper.ValidatePath(newPath);

                _logger.LogInformation("Перемещение файла {FileId} в {NewPath}", fileId, newPath);

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
                _logger.LogError(ex, "gRPC ошибка при перемещении файла");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при перемещении файла");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при перемещении файла");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageCopyResult> CopyFileAsync(Guid fileId, string newPath)
        {
            try
            {
                StorageValidationHelper.ValidatePath(newPath);

                _logger.LogInformation("Копирование файла {FileId} в {NewPath}", fileId, newPath);

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
                _logger.LogError(ex, "gRPC ошибка при копировании файла");
                return new StorageCopyResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при копировании файла");
                return new StorageCopyResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при копировании файла");
                return new StorageCopyResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> RenameFileAsync(Guid fileId, string newName)
        {
            try
            {
                StorageValidationHelper.ValidateFileName(newName);

                _logger.LogInformation("Переименование файла {FileId} в {NewName}", fileId, newName);

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
                _logger.LogError(ex, "gRPC ошибка при переименовании файла");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при переименовании файла");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при переименовании файла");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageDirectoryResult> NewDirectoryAsync(string directoryPath)
        {
            try
            {
                StorageValidationHelper.ValidateDirectoryPath(directoryPath);

                _logger.LogInformation("Создание директории {DirectoryPath}", directoryPath);

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
                _logger.LogError(ex, "gRPC ошибка при создании директории");
                return new StorageDirectoryResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при создании директории");
                return new StorageDirectoryResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при создании директории");
                return new StorageDirectoryResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> RenameDirectoryAsync(Guid directoryId, string newName)
        {
            try
            {
                StorageValidationHelper.ValidateDirectoryName(newName);

                _logger.LogInformation("Переименование директории {DirectoryId} в {NewName}", directoryId, newName);

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
                _logger.LogError(ex, "gRPC ошибка при переименовании директории");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при переименовании директории");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при переименовании директории");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> DeleteDirectoryAsync(Guid directoryId, bool recursive = false)
        {
            try
            {
                _logger.LogInformation("Удаление директории {DirectoryId}, recursive={Recursive}", directoryId, recursive);

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
                _logger.LogError(ex, "gRPC ошибка при удалении директории");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при удалении директории");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> MoveDirectoryAsync(Guid directoryId, string newPath)
        {
            try
            {
                StorageValidationHelper.ValidatePath(newPath);

                _logger.LogInformation("Перемещение директории {DirectoryId} в {NewPath}", directoryId, newPath);

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
                _logger.LogError(ex, "gRPC ошибка при перемещении директории");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при перемещении директории");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при перемещении директории");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageDirectoryCopyResult> CopyDirectoryAsync(Guid directoryId, string newPath, bool recursive = false)
        {
            try
            {
                StorageValidationHelper.ValidatePath(newPath);

                _logger.LogInformation("Копирование директории {DirectoryId} в {NewPath}, recursive={Recursive}", directoryId, newPath, recursive);

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
                _logger.LogError(ex, "gRPC ошибка при копировании директории");
                return new StorageDirectoryCopyResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при копировании директории");
                return new StorageDirectoryCopyResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при копировании директории");
                return new StorageDirectoryCopyResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageMetadataResult> GetMetadataAsync(Guid fileId)
        {
            try
            {
                _logger.LogInformation("Получение метаданных файла {FileId}", fileId);

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
                _logger.LogError(ex, "gRPC ошибка при получении метаданных");
                return new StorageMetadataResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при получении метаданных");
                return new StorageMetadataResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageOperationResult> UpdateMetadataAsync(Guid fileId, Dictionary<string, string> metadata)
        {
            try
            {
                StorageValidationHelper.ValidateMetadata(metadata);

                _logger.LogInformation("Обновление метаданных файла {FileId}", fileId);

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
                _logger.LogError(ex, "gRPC ошибка при обновлении метаданных");
                return new StorageOperationResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при обновлении метаданных");
                return new StorageOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при обновлении метаданных");
                return new StorageOperationResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageListResult> ListDirectoryAsync(Guid directoryId, bool recursive = false)
        {
            try
            {
                _logger.LogInformation("Список файлов директории {DirectoryId}, recursive={Recursive}", directoryId, recursive);

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
                _logger.LogError(ex, "gRPC ошибка при получении списка файлов");
                return new StorageListResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при получении списка файлов");
                return new StorageListResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageLinkResult> GenerateLinkAsync(Guid fileId, string[]? groups = null, string[]? users = null, int ttlSeconds = 86400)
        {
            try
            {
                StorageValidationHelper.ValidateTtl(ttlSeconds);

                _logger.LogInformation("Генерация ссылки для файла {FileId}", fileId);

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
                _logger.LogError(ex, "gRPC ошибка при генерации ссылки");
                return new StorageLinkResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при генерации ссылки");
                return new StorageLinkResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при генерации ссылки");
                return new StorageLinkResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageLinkResult> GenerateDirectLinkAsync(Guid fileId, string[]? groups = null, string[]? users = null, int ttlSeconds = 86400)
        {
            try
            {
                StorageValidationHelper.ValidateTtl(ttlSeconds);

                _logger.LogInformation("Генерация прямой ссылки для файла {FileId}", fileId);

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
                _logger.LogError(ex, "gRPC ошибка при генерации прямой ссылки");
                return new StorageLinkResult { Success = false, Message = $"Ошибка сервера: {ex.Status.Detail}" };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при генерации прямой ссылки");
                return new StorageLinkResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при генерации прямой ссылки");
                return new StorageLinkResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageDownloadResult> DownloadFileViaLinkAsync(string token)
        {
            try
            {
                _logger.LogInformation("Загрузка файла по ссылке");

                var storageUrl = _configuration["Grpc:StorageUrl"] ?? throw new InvalidOperationException("Storage URL not configured");
                ValidateHttpsUrl(storageUrl);

                var response = await _httpClient.GetAsync($"{storageUrl}/storage/links/{token}", HttpCompletionOption.ResponseHeadersRead);

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
                _logger.LogError(ex, "HTTP ошибка при загрузке файла по ссылке");
                return new StorageDownloadResult { Success = false, Message = $"Ошибка сервера: {ex.Message}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при загрузке файла по ссылке");
                return new StorageDownloadResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }

        public async Task<StorageDownloadResult> DownloadFileViaDirectLinkAsync(string token)
        {
            try
            {
                _logger.LogInformation("Загрузка файла по прямой ссылке");

                var storageUrl = _configuration["Grpc:StorageUrl"] ?? throw new InvalidOperationException("Storage URL not configured");
                ValidateHttpsUrl(storageUrl);

                var response = await _httpClient.GetAsync($"{storageUrl}/storage/direct/{token}", HttpCompletionOption.ResponseHeadersRead);

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
                _logger.LogError(ex, "HTTP ошибка при загрузке файла по прямой ссылке");
                return new StorageDownloadResult { Success = false, Message = $"Ошибка сервера: {ex.Message}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при загрузке файла по прямой ссылке");
                return new StorageDownloadResult { Success = false, Message = "Внутренняя ошибка сервера." };
            }
        }
    }
}
