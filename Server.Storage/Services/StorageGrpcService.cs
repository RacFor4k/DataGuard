using System.Buffers;
using System.Security.Cryptography;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Server.Storage.Interfaces;
using Server.Storage.Models;

namespace Server.Storage.Services;

[Authorize]
public partial class StorageGrpcService : Contracts.Protos.Storage.StorageService.StorageServiceBase
{
    private readonly IStorageFileRepository _fileRepo;
    private readonly IStorageDirectoryRepository _dirRepo;
    private readonly IStorageBlobStore _blobStore;
    private readonly IStorageNonceService _nonceService;
    private readonly IStoragePathValidator _pathValidator;
    private readonly IStorageMetadataService _metadataService;
    private readonly IStorageLinkService _linkService;
    private readonly IOwnerIdentityProvider _ownerProvider;
    private readonly ILogger<StorageGrpcService> _logger;

    private const int MaxChunkSize = 1 * 1024 * 1024;
    private const long MaxFileSize = 5L * 1024 * 1024 * 1024;
    private const int ChunkSize = 256 * 1024;
    private const string DefaultBucket = "dataguard-storage";

    public StorageGrpcService(
        IStorageFileRepository fileRepo,
        IStorageDirectoryRepository dirRepo,
        IStorageBlobStore blobStore,
        IStorageNonceService nonceService,
        IStoragePathValidator pathValidator,
        IStorageMetadataService metadataService,
        IStorageLinkService linkService,
        IOwnerIdentityProvider ownerProvider,
        ILogger<StorageGrpcService> logger)
    {
        _fileRepo = fileRepo;
        _dirRepo = dirRepo;
        _blobStore = blobStore;
        _nonceService = nonceService;
        _pathValidator = pathValidator;
        _metadataService = metadataService;
        _linkService = linkService;
        _ownerProvider = ownerProvider;
        _logger = logger;
    }

    private Guid? GetOwnerId(ServerCallContext context)
    {
        return _ownerProvider.GetOwnerId(context.GetHttpContext());
    }

    public override async Task<Contracts.Protos.Storage.UploadFileResponse> UploadFile(
        IAsyncStreamReader<Contracts.Protos.Storage.UploadFileRequest> requestStream,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
        {
            return new Contracts.Protos.Storage.UploadFileResponse
            {
                Success = false,
                Message = "Не удалось идентифицировать пользователя."
            };
        }

        Contracts.Protos.Storage.FileMetadata? fileMetadata = null;

        if (!await requestStream.MoveNext(context.CancellationToken) || requestStream.Current.DataCase != Contracts.Protos.Storage.UploadFileRequest.DataOneofCase.Metadata)
        {
            return new Contracts.Protos.Storage.UploadFileResponse
            {
                Success = false,
                Message = "Первое сообщение должно содержать метаданные файла."
            };
        }

        fileMetadata = requestStream.Current.Metadata;

        if (string.IsNullOrWhiteSpace(fileMetadata.FileName))
        {
            return new Contracts.Protos.Storage.UploadFileResponse
            {
                Success = false,
                Message = "Имя файла не может быть пустым."
            };
        }

        if (fileMetadata.FileName.Contains('/') || fileMetadata.FileName.Contains('\\'))
        {
            return new Contracts.Protos.Storage.UploadFileResponse
            {
                Success = false,
                Message = "Имя файла не должно содержать разделителей пути."
            };
        }

        string filePath = string.IsNullOrEmpty(fileMetadata.FilePath) ? "/" : fileMetadata.FilePath;
        if (!_pathValidator.TryNormalizePath(filePath, out string normalizedPath, out string? pathError))
        {
            return new Contracts.Protos.Storage.UploadFileResponse
            {
                Success = false,
                Message = $"Некорректный путь: {pathError}"
            };
        }

        string fullPath = normalizedPath == "/" ? $"/{fileMetadata.FileName}" : $"{normalizedPath}/{fileMetadata.FileName}";

        string extension = Path.GetExtension(fileMetadata.FileName);
        string storageKey = _blobStore.GenerateStorageKey(extension);
        byte[] sha256Hash;
        long totalSize = 0;

        // Проверка существования родительской директории перед загрузкой
        Guid? parentDirId = null;
        if (!string.IsNullOrEmpty(fileMetadata.ParentDirectoryId) && Guid.TryParse(fileMetadata.ParentDirectoryId, out var parsedDirId))
        {
            parentDirId = parsedDirId;
            var parentDir = await _dirRepo.GetDirectoryAsync(parsedDirId, ownerId.Value, ct: context.CancellationToken);
            if (parentDir == null)
            {
                return new Contracts.Protos.Storage.UploadFileResponse
                {
                    Success = false,
                    Message = "Родительская директория не найдена."
                };
            }
        }

        // NOTE: Полный рефакторинг потоковой загрузки (gRPC → MinIO без буферизации в MemoryStream)
        // требует значительной переработки архитектуры. Текущая реализация буферизует чанки в памяти.
        using var ms = new MemoryStream();
        try
        {
            while (await requestStream.MoveNext(context.CancellationToken))
            {
                if (requestStream.Current.DataCase != Contracts.Protos.Storage.UploadFileRequest.DataOneofCase.Chunk)
                    break;

                ByteString chunk = requestStream.Current.Chunk;

                if (chunk.Length > MaxChunkSize)
                {
                    return new Contracts.Protos.Storage.UploadFileResponse
                    {
                        Success = false,
                        Message = "Размер чанка превышает допустимый лимит."
                    };
                }

                totalSize += chunk.Length;

                if (totalSize > MaxFileSize)
                {
                    return new Contracts.Protos.Storage.UploadFileResponse
                    {
                        Success = false,
                        Message = "Размер файла превышает допустимый лимит."
                    };
                }

                chunk.WriteTo(ms);
            }

            ms.Position = 0;
            sha256Hash = SHA256.HashData(ms);

            ms.Position = 0;
            await _blobStore.PutObjectAsync(DefaultBucket, storageKey, ms, totalSize, context.CancellationToken);

            var file = new StorageFile
            {
                FileId = Guid.NewGuid(),
                OwnerId = ownerId.Value,
                FileName = fileMetadata.FileName,
                NormalizedPath = fullPath,
                Size = totalSize,
                StorageKey = storageKey,
                BucketName = DefaultBucket,
                CreatedAtUtc = DateTime.UtcNow,
                ContentHash = sha256Hash
            };

            await _fileRepo.CreateFileAsync(file, context.CancellationToken);

            if (fileMetadata.Metadata.Count > 0)
            {
                var metaDict = new Dictionary<string, string>();
                foreach (var kvp in fileMetadata.Metadata)
                    metaDict[kvp.Key] = kvp.Value;
                await _metadataService.UpdateMetadataAsync(file.FileId, metaDict, context.CancellationToken);
            }

            return new Contracts.Protos.Storage.UploadFileResponse
            {
                Success = true,
                Message = "OK",
                FileId = file.FileId.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UploadFile failed for owner {OwnerId}", ownerId);
            return new Contracts.Protos.Storage.UploadFileResponse
            {
                Success = false,
                Message = "Внутренняя ошибка сервера."
            };
        }
    }

    public override async Task GetFile(
        Contracts.Protos.Storage.GetFileRequest request,
        IServerStreamWriter<Contracts.Protos.Storage.GetFileResponse> responseStream,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return;

        if (!Guid.TryParse(request.FileId, out Guid fileId))
            return;

        var file = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (file == null)
            return;

        var metadataResponse = new Contracts.Protos.Storage.GetFileResponse
        {
            Metadata = new Contracts.Protos.Storage.FileMetadata
            {
                FileId = file.FileId.ToString(),
                FileName = file.FileName,
                FilePath = _pathValidator.GetParentPath(file.NormalizedPath),
                Size = file.Size
            }
        };
        await responseStream.WriteAsync(metadataResponse, context.CancellationToken);

        try
        {
            using var blobStream = await _blobStore.GetObjectAsync(file.BucketName, file.StorageKey, context.CancellationToken);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
            try
            {
                int bytesRead;
                while ((bytesRead = await blobStream.ReadAsync(buffer.AsMemory(0, ChunkSize), context.CancellationToken)) > 0)
                {
                    await responseStream.WriteAsync(new Contracts.Protos.Storage.GetFileResponse
                    {
                        Chunk = ByteString.CopyFrom(buffer, 0, bytesRead)
                    }, context.CancellationToken);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFile failed for file {FileId}", fileId);
        }
    }

    public override async Task GetFileChanges(
        Contracts.Protos.Storage.GetFileRequest request,
        IServerStreamWriter<Contracts.Protos.Storage.GetFileResponse> responseStream,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null || !Guid.TryParse(request.FileId, out Guid fileId))
            return;

        var file = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (file == null)
            return;
    }

    public override async Task<Contracts.Protos.Storage.UpdateFileResponse> UpdateFile(
        Contracts.Protos.Storage.UpdateFileRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.UpdateFileResponse { Success = false, Message = "Ошибка аутентификации." };

        if (string.IsNullOrEmpty(request.FileId))
            return new Contracts.Protos.Storage.UpdateFileResponse { Success = false, Message = "Некорректный запрос." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.UpdateFileResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "UpdateFile", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.UpdateFileResponse { Success = false, Message = "Невалидный или повторный nonce." };

        if (!Guid.TryParse(request.FileId, out Guid fileId))
            return new Contracts.Protos.Storage.UpdateFileResponse { Success = false, Message = "Некорректный идентификатор файла." };

        var file = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (file == null)
            return new Contracts.Protos.Storage.UpdateFileResponse { Success = false, Message = "Файл не найден." };

        try
        {
            using var blobStream = await _blobStore.GetObjectAsync(file.BucketName, file.StorageKey, context.CancellationToken);
            using var ms = new MemoryStream();
            await blobStream.CopyToAsync(ms, context.CancellationToken);

            byte[] data = ms.ToArray();

            if (request.OperationCase == Contracts.Protos.Storage.UpdateFileRequest.OperationOneofCase.Update)
            {
                var update = request.Update;
                if (update.Offset < 0 || update.Offset + update.Data.Length > MaxFileSize)
                    return new Contracts.Protos.Storage.UpdateFileResponse { Success = false, Message = "Некорректный offset для обновления." };

                long requiredSize = update.Offset + update.Data.Length;
                if (requiredSize > data.Length)
                {
                    Array.Resize(ref data, (int)requiredSize);
                }

                update.Data.CopyTo(data, (int)update.Offset);
            }
            else if (request.OperationCase == Contracts.Protos.Storage.UpdateFileRequest.OperationOneofCase.Erase)
            {
                var erase = request.Erase;
                if (erase.Offset < 0 || erase.Offset >= data.Length)
                    return new Contracts.Protos.Storage.UpdateFileResponse { Success = false, Message = "Некорректный offset для стирания." };

                int eraseSize = (int)Math.Min(erase.Size, data.Length - erase.Offset);
                Array.Clear(data, (int)erase.Offset, eraseSize);
            }

            string newStorageKey = _blobStore.GenerateStorageKey(Path.GetExtension(file.FileName));
            using var newStream = new MemoryStream(data);

            await _blobStore.PutObjectAsync(DefaultBucket, newStorageKey, newStream, data.Length, context.CancellationToken);

            byte[] hash = SHA256.HashData(data);

            file.StorageKey = newStorageKey;
            file.Size = data.Length;
            file.ContentHash = hash;
            file.UpdatedAtUtc = DateTime.UtcNow;

            await _fileRepo.UpdateFileAsync(file, context.CancellationToken);

            return new Contracts.Protos.Storage.UpdateFileResponse { Success = true, Message = "OK" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateFile failed for file {FileId}", fileId);
            return new Contracts.Protos.Storage.UpdateFileResponse { Success = false, Message = "Внутренняя ошибка сервера." };
        }
    }

    public override async Task<Contracts.Protos.Storage.DeleteFileResponse> DeleteFile(
        Contracts.Protos.Storage.DeleteFileRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.DeleteFileResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.FileId, out Guid fileId))
            return new Contracts.Protos.Storage.DeleteFileResponse { Success = false, Message = "Некорректный идентификатор файла." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.DeleteFileResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "DeleteFile", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.DeleteFileResponse { Success = false, Message = "Невалидный или повторный nonce." };

        var file = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (file == null)
            return new Contracts.Protos.Storage.DeleteFileResponse { Success = false, Message = "Файл не найден." };

        bool deleted = await _fileRepo.DeleteFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (!deleted)
            return new Contracts.Protos.Storage.DeleteFileResponse { Success = false, Message = "Не удалось удалить файл." };

        return new Contracts.Protos.Storage.DeleteFileResponse { Success = true, Message = "OK" };
    }

    public override async Task<Contracts.Protos.Storage.MoveFileResponse> MoveFile(
        Contracts.Protos.Storage.MoveFileRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.MoveFileResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.FileId, out Guid fileId))
            return new Contracts.Protos.Storage.MoveFileResponse { Success = false, Message = "Некорректный идентификатор файла." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.MoveFileResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "MoveFile", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.MoveFileResponse { Success = false, Message = "Невалидный или повторный nonce." };

        if (!_pathValidator.TryNormalizePath(request.NewPath, out string normalizedPath, out string? pathError))
            return new Contracts.Protos.Storage.MoveFileResponse { Success = false, Message = $"Некорректный путь: {pathError}" };

        var file = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (file == null)
            return new Contracts.Protos.Storage.MoveFileResponse { Success = false, Message = "Файл не найден." };

        string newFullPath = normalizedPath == "/" ? $"/{file.FileName}" : $"{normalizedPath}/{file.FileName}";

        var existing = await _fileRepo.GetFileByPathAsync(ownerId.Value, newFullPath, context.CancellationToken);
        if (existing != null)
            return new Contracts.Protos.Storage.MoveFileResponse { Success = false, Message = "Файл с таким путём уже существует." };

        Guid? targetDirId = null;
        if (normalizedPath != "/")
        {
            var targetDir = await _dirRepo.GetDirectoryByPathAsync(ownerId.Value, normalizedPath, context.CancellationToken);
            if (targetDir == null)
                return new Contracts.Protos.Storage.MoveFileResponse { Success = false, Message = "Целевая директория не найдена." };
            targetDirId = targetDir.DirectoryId;
        }

        await _fileRepo.MoveFileAsync(fileId, ownerId.Value, targetDirId, newFullPath, context.CancellationToken);

        return new Contracts.Protos.Storage.MoveFileResponse { Success = true, Message = "OK" };
    }

    public override async Task<Contracts.Protos.Storage.CopyFileResponse> CopyFile(
        Contracts.Protos.Storage.CopyFileRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.CopyFileResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.FileId, out Guid fileId))
            return new Contracts.Protos.Storage.CopyFileResponse { Success = false, Message = "Некорректный идентификатор файла." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.CopyFileResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "CopyFile", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.CopyFileResponse { Success = false, Message = "Невалидный или повторный nonce." };

        if (!_pathValidator.TryNormalizePath(request.NewPath, out string normalizedPath, out string? pathError))
            return new Contracts.Protos.Storage.CopyFileResponse { Success = false, Message = $"Некорректный путь: {pathError}" };

        var sourceFile = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (sourceFile == null)
            return new Contracts.Protos.Storage.CopyFileResponse { Success = false, Message = "Файл не найден." };

        string newFullPath = normalizedPath == "/" ? $"/{sourceFile.FileName}" : $"{normalizedPath}/{sourceFile.FileName}";

        var existing = await _fileRepo.GetFileByPathAsync(ownerId.Value, newFullPath, context.CancellationToken);
        if (existing != null)
            return new Contracts.Protos.Storage.CopyFileResponse { Success = false, Message = "Файл с таким путём уже существует." };

        Guid? targetDirId = null;
        if (normalizedPath != "/")
        {
            var targetDir = await _dirRepo.GetDirectoryByPathAsync(ownerId.Value, normalizedPath, context.CancellationToken);
            if (targetDir == null)
                return new Contracts.Protos.Storage.CopyFileResponse { Success = false, Message = "Целевая директория не найдена." };
            targetDirId = targetDir.DirectoryId;
        }

        string newStorageKey = _blobStore.GenerateStorageKey(Path.GetExtension(sourceFile.FileName));
        await _blobStore.CopyObjectAsync(sourceFile.BucketName, sourceFile.StorageKey, DefaultBucket, newStorageKey, context.CancellationToken);

        var copy = await _fileRepo.CopyFileAsync(fileId, ownerId.Value, targetDirId, newFullPath, context.CancellationToken);
        if (copy == null)
            return new Contracts.Protos.Storage.CopyFileResponse { Success = false, Message = "Не удалось скопировать файл." };

        copy.StorageKey = newStorageKey;
        await _fileRepo.UpdateFileAsync(copy, context.CancellationToken);

        return new Contracts.Protos.Storage.CopyFileResponse
        {
            Success = true,
            Message = "OK",
            NewFileId = copy.FileId.ToString()
        };
    }

    public override async Task<Contracts.Protos.Storage.RenameFileResponse> RenameFile(
        Contracts.Protos.Storage.RenameFileRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.RenameFileResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.FileId, out Guid fileId))
            return new Contracts.Protos.Storage.RenameFileResponse { Success = false, Message = "Некорректный идентификатор файла." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.RenameFileResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "RenameFile", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.RenameFileResponse { Success = false, Message = "Невалидный или повторный nonce." };

        if (string.IsNullOrWhiteSpace(request.NewName))
            return new Contracts.Protos.Storage.RenameFileResponse { Success = false, Message = "Имя файла не может быть пустым." };

        if (request.NewName.Contains('/') || request.NewName.Contains('\\'))
            return new Contracts.Protos.Storage.RenameFileResponse { Success = false, Message = "Имя файла не должно содержать разделителей пути." };

        var file = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (file == null)
            return new Contracts.Protos.Storage.RenameFileResponse { Success = false, Message = "Файл не найден." };

        string parentPath = _pathValidator.GetParentPath(file.NormalizedPath);
        string newFullPath = parentPath == "/" ? $"/{request.NewName}" : $"{parentPath}/{request.NewName}";

        var existing = await _fileRepo.GetFileByPathAsync(ownerId.Value, newFullPath, context.CancellationToken);
        if (existing != null)
            return new Contracts.Protos.Storage.RenameFileResponse { Success = false, Message = "Файл с таким именем уже существует." };

        await _fileRepo.RenameFileAsync(fileId, ownerId.Value, request.NewName, context.CancellationToken);

        return new Contracts.Protos.Storage.RenameFileResponse { Success = true, Message = "OK" };
    }

    public override async Task<Contracts.Protos.Storage.NewDirectoryResponse> NewDirectory(
        Contracts.Protos.Storage.NewDirectoryRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.NewDirectoryResponse { Success = false, Message = "Ошибка аутентификации." };

        if (string.IsNullOrWhiteSpace(request.DirectoryPath))
            return new Contracts.Protos.Storage.NewDirectoryResponse { Success = false, Message = "Путь директории не может быть пустым." };

        if (!_pathValidator.TryNormalizePath(request.DirectoryPath, out string normalizedPath, out string? pathError))
            return new Contracts.Protos.Storage.NewDirectoryResponse { Success = false, Message = $"Некорректный путь: {pathError}" };

        var existing = await _dirRepo.GetDirectoryByPathAsync(ownerId.Value, normalizedPath, context.CancellationToken);
        if (existing != null)
            return new Contracts.Protos.Storage.NewDirectoryResponse { Success = false, Message = "Директория уже существует." };

        var existingFile = await _fileRepo.GetFileByPathAsync(ownerId.Value, normalizedPath, context.CancellationToken);
        if (existingFile != null)
            return new Contracts.Protos.Storage.NewDirectoryResponse { Success = false, Message = "Файл с таким путём уже существует." };

        Guid? parentId = null;
        if (normalizedPath != "/")
        {
            string parentPath = _pathValidator.GetParentPath(normalizedPath);
            if (parentPath != "/")
            {
                var parent = await _dirRepo.GetDirectoryByPathAsync(ownerId.Value, parentPath, context.CancellationToken);
                if (parent == null)
                    return new Contracts.Protos.Storage.NewDirectoryResponse { Success = false, Message = "Родительская директория не найдена." };
                parentId = parent.DirectoryId;
            }
        }

        string dirName = _pathValidator.GetFileName(normalizedPath);

        var dir = new StorageDirectory
        {
            DirectoryId = Guid.NewGuid(),
            OwnerId = ownerId.Value,
            ParentDirectoryId = parentId,
            DirectoryName = dirName,
            NormalizedName = dirName.ToLowerInvariant(),
            NormalizedPath = normalizedPath,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dirRepo.CreateDirectoryAsync(dir, context.CancellationToken);

        return new Contracts.Protos.Storage.NewDirectoryResponse
        {
            Success = true,
            Message = "OK",
            DirectoryId = dir.DirectoryId.ToString()
        };
    }

    public override async Task<Contracts.Protos.Storage.RenameDirectoryResponse> RenameDirectory(
        Contracts.Protos.Storage.RenameDirectoryRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.RenameDirectoryResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.DirectoryId, out Guid dirId))
            return new Contracts.Protos.Storage.RenameDirectoryResponse { Success = false, Message = "Некорректный идентификатор директории." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.RenameDirectoryResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "RenameDirectory", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.RenameDirectoryResponse { Success = false, Message = "Невалидный или повторный nonce." };

        if (string.IsNullOrWhiteSpace(request.NewName))
            return new Contracts.Protos.Storage.RenameDirectoryResponse { Success = false, Message = "Имя директории не может быть пустым." };

        if (request.NewName.Contains('/') || request.NewName.Contains('\\'))
            return new Contracts.Protos.Storage.RenameDirectoryResponse { Success = false, Message = "Имя директории не должно содержать разделителей пути." };

        var dir = await _dirRepo.GetDirectoryAsync(dirId, ownerId.Value, context.CancellationToken);
        if (dir == null)
            return new Contracts.Protos.Storage.RenameDirectoryResponse { Success = false, Message = "Директория не найдена." };

        string parentPath = _pathValidator.GetParentPath(dir.NormalizedPath);
        string newPath = parentPath == "/" ? $"/{request.NewName}" : $"{parentPath}/{request.NewName}";

        var existing = await _dirRepo.GetDirectoryByPathAsync(ownerId.Value, newPath, context.CancellationToken);
        if (existing != null)
            return new Contracts.Protos.Storage.RenameDirectoryResponse { Success = false, Message = "Директория с таким именем уже существует." };

        await _dirRepo.RenameDirectoryAsync(dirId, ownerId.Value, request.NewName, context.CancellationToken);

        return new Contracts.Protos.Storage.RenameDirectoryResponse { Success = true, Message = "OK" };
    }

    public override async Task<Contracts.Protos.Storage.DeleteDirectoryResponse> DeleteDirectory(
        Contracts.Protos.Storage.DeleteDirectoryRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.DeleteDirectoryResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.DirectoryId, out Guid dirId))
            return new Contracts.Protos.Storage.DeleteDirectoryResponse { Success = false, Message = "Некорректный идентификатор директории." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.DeleteDirectoryResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "DeleteDirectory", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.DeleteDirectoryResponse { Success = false, Message = "Невалидный или повторный nonce." };

        var dir = await _dirRepo.GetDirectoryAsync(dirId, ownerId.Value, context.CancellationToken);
        if (dir == null)
            return new Contracts.Protos.Storage.DeleteDirectoryResponse { Success = false, Message = "Директория не найдена." };

        bool deleted = await _dirRepo.DeleteDirectoryAsync(dirId, ownerId.Value, request.Recursive, context.CancellationToken);
        if (!deleted)
            return new Contracts.Protos.Storage.DeleteDirectoryResponse { Success = false, Message = "Директория не пуста. Используйте рекурсивное удаление." };

        return new Contracts.Protos.Storage.DeleteDirectoryResponse { Success = true, Message = "OK" };
    }

    public override async Task<Contracts.Protos.Storage.MoveDirectoryResponse> MoveDirectory(
        Contracts.Protos.Storage.MoveDirectoryRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.MoveDirectoryResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.DirectoryId, out Guid dirId))
            return new Contracts.Protos.Storage.MoveDirectoryResponse { Success = false, Message = "Некорректный идентификатор директории." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.MoveDirectoryResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "MoveDirectory", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.MoveDirectoryResponse { Success = false, Message = "Невалидный или повторный nonce." };

        if (!_pathValidator.TryNormalizePath(request.NewPath, out string normalizedPath, out string? pathError))
            return new Contracts.Protos.Storage.MoveDirectoryResponse { Success = false, Message = $"Некорректный путь: {pathError}" };

        var dir = await _dirRepo.GetDirectoryAsync(dirId, ownerId.Value, context.CancellationToken);
        if (dir == null)
            return new Contracts.Protos.Storage.MoveDirectoryResponse { Success = false, Message = "Директория не найдена." };

        if (_pathValidator.IsPathInside(dir.NormalizedPath, normalizedPath))
            return new Contracts.Protos.Storage.MoveDirectoryResponse { Success = false, Message = "Нельзя переместить директорию внутрь себя." };

        var existing = await _dirRepo.GetDirectoryByPathAsync(ownerId.Value, normalizedPath, context.CancellationToken);
        if (existing != null)
            return new Contracts.Protos.Storage.MoveDirectoryResponse { Success = false, Message = "Директория с таким путём уже существует." };

        Guid? targetParentId = null;
        if (normalizedPath != "/")
        {
            string targetParentPath = _pathValidator.GetParentPath(normalizedPath);
            if (targetParentPath != "/")
            {
                var targetParent = await _dirRepo.GetDirectoryByPathAsync(ownerId.Value, targetParentPath, context.CancellationToken);
                if (targetParent == null)
                    return new Contracts.Protos.Storage.MoveDirectoryResponse { Success = false, Message = "Целевая родительская директория не найдена." };
                targetParentId = targetParent.DirectoryId;
            }
        }

        await _dirRepo.MoveDirectoryAsync(dirId, ownerId.Value, targetParentId, normalizedPath, context.CancellationToken);

        return new Contracts.Protos.Storage.MoveDirectoryResponse { Success = true, Message = "OK" };
    }

    public override async Task<Contracts.Protos.Storage.CopyDirectoryResponse> CopyDirectory(
        Contracts.Protos.Storage.CopyDirectoryRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.CopyDirectoryResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.DirectoryId, out Guid dirId))
            return new Contracts.Protos.Storage.CopyDirectoryResponse { Success = false, Message = "Некорректный идентификатор директории." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.CopyDirectoryResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "CopyDirectory", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.CopyDirectoryResponse { Success = false, Message = "Невалидный или повторный nonce." };

        if (!_pathValidator.TryNormalizePath(request.NewPath, out string normalizedPath, out string? pathError))
            return new Contracts.Protos.Storage.CopyDirectoryResponse { Success = false, Message = $"Некорректный путь: {pathError}" };

        var sourceDir = await _dirRepo.GetDirectoryAsync(dirId, ownerId.Value, context.CancellationToken);
        if (sourceDir == null)
            return new Contracts.Protos.Storage.CopyDirectoryResponse { Success = false, Message = "Директория не найдена." };

        var existing = await _dirRepo.GetDirectoryByPathAsync(ownerId.Value, normalizedPath, context.CancellationToken);
        if (existing != null)
            return new Contracts.Protos.Storage.CopyDirectoryResponse { Success = false, Message = "Директория с таким путём уже существует." };

        Guid? targetParentId = null;
        if (normalizedPath != "/")
        {
            string targetParentPath = _pathValidator.GetParentPath(normalizedPath);
            if (targetParentPath != "/")
            {
                var targetParent = await _dirRepo.GetDirectoryByPathAsync(ownerId.Value, targetParentPath, context.CancellationToken);
                if (targetParent == null)
                    return new Contracts.Protos.Storage.CopyDirectoryResponse { Success = false, Message = "Целевая родительская директория не найдена." };
                targetParentId = targetParent.DirectoryId;
            }
        }

        string newDirName = _pathValidator.GetFileName(normalizedPath);

        var newDir = new StorageDirectory
        {
            DirectoryId = Guid.NewGuid(),
            OwnerId = ownerId.Value,
            ParentDirectoryId = targetParentId,
            DirectoryName = newDirName,
            NormalizedName = newDirName.ToLowerInvariant(),
            NormalizedPath = normalizedPath,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dirRepo.CreateDirectoryAsync(newDir, context.CancellationToken);

        if (request.Recursive)
        {
            await CopyDirectoryRecursiveAsync(sourceDir.DirectoryId, newDir.DirectoryId, ownerId.Value, sourceDir.NormalizedPath, normalizedPath, context.CancellationToken);
        }

        return new Contracts.Protos.Storage.CopyDirectoryResponse
        {
            Success = true,
            Message = "OK",
            NewDirectoryId = newDir.DirectoryId.ToString()
        };
    }

    private async Task CopyDirectoryRecursiveAsync(Guid sourceDirId, Guid destDirId, Guid ownerId, string sourcePrefix, string destPrefix, CancellationToken ct)
    {
        var files = await _fileRepo.ListFilesAsync(ownerId, sourceDirId, false, ct);
        foreach (var file in files)
        {
            string relativePath = file.NormalizedPath[sourcePrefix.Length..];
            string newPath = destPrefix == "/" ? $"/{relativePath.TrimStart('/')}" : $"{destPrefix}/{relativePath.TrimStart('/')}";

            string newStorageKey = _blobStore.GenerateStorageKey(Path.GetExtension(file.FileName));
            await _blobStore.CopyObjectAsync(file.BucketName, file.StorageKey, DefaultBucket, newStorageKey, ct);

            var copy = new StorageFile
            {
                FileId = Guid.NewGuid(),
                OwnerId = ownerId,
                ParentDirectoryId = destDirId,
                FileName = file.FileName,
                NormalizedPath = newPath,
                Size = file.Size,
                StorageKey = newStorageKey,
                BucketName = DefaultBucket,
                CreatedAtUtc = DateTime.UtcNow,
                ContentHash = file.ContentHash
            };

            await _fileRepo.CreateFileAsync(copy, ct);
        }

        var subdirs = await _dirRepo.GetSubdirectoriesAsync(ownerId, sourceDirId, ct);
        foreach (var sub in subdirs)
        {
            string relativePath = sub.NormalizedPath[sourcePrefix.Length..];
            string newSubPath = destPrefix == "/" ? $"/{relativePath.TrimStart('/')}" : $"{destPrefix}/{relativePath.TrimStart('/')}";

            var newSubDir = new StorageDirectory
            {
                DirectoryId = Guid.NewGuid(),
                OwnerId = ownerId,
                ParentDirectoryId = destDirId,
                DirectoryName = sub.DirectoryName,
                NormalizedName = sub.DirectoryName.ToLowerInvariant(),
                NormalizedPath = newSubPath,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _dirRepo.CreateDirectoryAsync(newSubDir, ct);
            await CopyDirectoryRecursiveAsync(sub.DirectoryId, newSubDir.DirectoryId, ownerId, sub.NormalizedPath, newSubPath, ct);
        }
    }

    public override async Task<Contracts.Protos.Storage.GetMetadataResponse> GetMetadata(
        Contracts.Protos.Storage.GetMetadataRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.GetMetadataResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.FileId, out Guid fileId))
            return new Contracts.Protos.Storage.GetMetadataResponse { Success = false, Message = "Некорректный идентификатор файла." };

        var file = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (file == null)
            return new Contracts.Protos.Storage.GetMetadataResponse { Success = false, Message = "Файл не найден." };

        var metadata = await _metadataService.GetMetadataAsync(fileId, context.CancellationToken);

        var response = new Contracts.Protos.Storage.GetMetadataResponse
        {
            Success = true,
            Message = "OK",
            Metadata = new Contracts.Protos.Storage.FileMetadata
            {
                FileId = file.FileId.ToString(),
                FileName = file.FileName,
                FilePath = _pathValidator.GetParentPath(file.NormalizedPath),
                Size = file.Size
            }
        };

        foreach (var kvp in metadata)
            response.Metadata.Metadata[kvp.Key] = kvp.Value;

        return response;
    }

    public override async Task<Contracts.Protos.Storage.UpdateMetadataResponse> UpdateMetadata(
        Contracts.Protos.Storage.UpdateMetadataRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.UpdateMetadataResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.FileId, out Guid fileId))
            return new Contracts.Protos.Storage.UpdateMetadataResponse { Success = false, Message = "Некорректный идентификатор файла." };

        var file = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (file == null)
            return new Contracts.Protos.Storage.UpdateMetadataResponse { Success = false, Message = "Файл не найден." };

        try
        {
            var metaDict = new Dictionary<string, string>();
            foreach (var kvp in request.Metadata)
                metaDict[kvp.Key] = kvp.Value;

            await _metadataService.UpdateMetadataAsync(fileId, metaDict, context.CancellationToken);

            return new Contracts.Protos.Storage.UpdateMetadataResponse { Success = true, Message = "OK" };
        }
        catch (ArgumentException ex)
        {
            return new Contracts.Protos.Storage.UpdateMetadataResponse { Success = false, Message = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateMetadata failed for file {FileId}", fileId);
            return new Contracts.Protos.Storage.UpdateMetadataResponse { Success = false, Message = "Внутренняя ошибка сервера." };
        }
    }

    public override async Task<Contracts.Protos.Storage.ListDirectoryResponse> ListDirectory(
        Contracts.Protos.Storage.ListDirectoryRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.ListDirectoryResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.DirectoryId, out Guid dirId))
            return new Contracts.Protos.Storage.ListDirectoryResponse { Success = false, Message = "Некорректный идентификатор директории." };

        var dir = await _dirRepo.GetDirectoryAsync(dirId, ownerId.Value, context.CancellationToken);
        if (dir == null)
            return new Contracts.Protos.Storage.ListDirectoryResponse { Success = false, Message = "Директория не найдена." };

        var contents = await _dirRepo.ListDirectoryContentsAsync(ownerId.Value, dirId, request.Recursive, context.CancellationToken);

        var response = new Contracts.Protos.Storage.ListDirectoryResponse
        {
            Success = true,
            Message = "OK"
        };

        foreach (var item in contents)
        {
            if (item is StorageFile file)
            {
                response.Items.Add(new Contracts.Protos.Storage.FileMetadata
                {
                    FileId = file.FileId.ToString(),
                    FileName = file.FileName,
                    FilePath = _pathValidator.GetParentPath(file.NormalizedPath),
                    Size = file.Size
                });
            }
        }

        return response;
    }

    public override async Task<Contracts.Protos.Storage.GenerateLinkResponse> GenerateLink(
        Contracts.Protos.Storage.GenerateLinkRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.FileId, out Guid fileId))
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Некорректный идентификатор файла." };

        if (request.TtlSeconds <= 0)
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "TTL должен быть положительным." };

        if (request.TtlSeconds > 30 * 24 * 3600)
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "TTL не может превышать 30 дней." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "GenerateLink", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Невалидный или повторный nonce." };

        var file = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (file == null)
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Файл не найден." };

        try
        {
            var users = request.Users.ToList();
            var groups = request.Groups.ToList();
            var ttl = TimeSpan.FromSeconds(request.TtlSeconds);

            var link = await _linkService.GenerateLinkAsync(fileId, ownerId.Value, ttl, false, users, groups, context.CancellationToken);

            return new Contracts.Protos.Storage.GenerateLinkResponse
            {
                Success = true,
                Message = "OK",
                Link = $"/storage/links/{link.Token}"
            };
        }
        catch (ArgumentException ex)
        {
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateLink failed for file {FileId}", fileId);
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Внутренняя ошибка сервера." };
        }
    }

    public override async Task<Contracts.Protos.Storage.GenerateLinkResponse> GenerateDirectLink(
        Contracts.Protos.Storage.GenerateLinkRequest request,
        ServerCallContext context)
    {
        var ownerId = GetOwnerId(context);
        if (ownerId == null)
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Ошибка аутентификации." };

        if (!Guid.TryParse(request.FileId, out Guid fileId))
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Некорректный идентификатор файла." };

        if (request.TtlSeconds <= 0)
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "TTL должен быть положительным." };

        if (request.TtlSeconds > 30 * 24 * 3600)
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "TTL не может превышать 30 дней." };

        if (string.IsNullOrEmpty(request.NonceToken))
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Требуется nonce_token." };

        bool nonceValid = await _nonceService.TryConsumeNonceAsync(ownerId.Value, "GenerateDirectLink", request.NonceToken, TimeSpan.FromMinutes(5), context.CancellationToken);
        if (!nonceValid)
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Невалидный или повторный nonce." };

        var file = await _fileRepo.GetFileAsync(fileId, ownerId.Value, context.CancellationToken);
        if (file == null)
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Файл не найден." };

        try
        {
            var users = request.Users.ToList();
            var groups = request.Groups.ToList();
            var ttl = TimeSpan.FromSeconds(request.TtlSeconds);

            var link = await _linkService.GenerateLinkAsync(fileId, ownerId.Value, ttl, true, users, groups, context.CancellationToken);

            return new Contracts.Protos.Storage.GenerateLinkResponse
            {
                Success = true,
                Message = "OK",
                Link = $"/storage/direct/{link.Token}"
            };
        }
        catch (ArgumentException ex)
        {
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateDirectLink failed for file {FileId}", fileId);
            return new Contracts.Protos.Storage.GenerateLinkResponse { Success = false, Message = "Внутренняя ошибка сервера." };
        }
    }
}
