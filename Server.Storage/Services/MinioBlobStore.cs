using Minio;
using Minio.DataModel.Args;
using Server.Storage.Interfaces;

namespace Server.Storage.Services;

public class MinioBlobStore : IStorageBlobStore
{
    private readonly IMinioClient _minioClient;

    public MinioBlobStore(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    public async Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        var args = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(ms);
            });
        await _minioClient.GetObjectAsync(args, ct);
        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Потоковая загрузка объекта из хранилища напрямую в целевой поток без промежуточной буферизации в памяти.
    /// </summary>
    public async Task GetObjectToStreamAsync(string bucketName, string objectName, Stream targetStream, CancellationToken ct = default)
    {
        var args = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream => stream.CopyTo(targetStream));
        await _minioClient.GetObjectAsync(args, ct);
    }

    public async Task PutObjectAsync(string bucketName, string objectName, Stream content, long size, CancellationToken ct = default)
    {
        var args = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(content)
            .WithObjectSize(size);
        await _minioClient.PutObjectAsync(args, ct);
    }

    public async Task CopyObjectAsync(string sourceBucket, string sourceObject, string destBucket, string destObject, CancellationToken ct = default)
    {
        var copySourceArgs = new CopySourceObjectArgs()
            .WithBucket(sourceBucket)
            .WithObject(sourceObject);
        var args = new CopyObjectArgs()
            .WithBucket(destBucket)
            .WithObject(destObject)
            .WithCopyObjectSource(copySourceArgs);
        await _minioClient.CopyObjectAsync(args, ct);
    }

    public async Task RemoveObjectAsync(string bucketName, string objectName, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);
        await _minioClient.RemoveObjectAsync(args, ct);
    }

    public async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct = default)
    {
        var args = new BucketExistsArgs().WithBucket(bucketName);
        bool exists = await _minioClient.BucketExistsAsync(args, ct);
        if (!exists)
        {
            var makeArgs = new MakeBucketArgs().WithBucket(bucketName);
            await _minioClient.MakeBucketAsync(makeArgs, ct);
        }
    }

    public string GenerateStorageKey(string fileExtension)
    {
        string key = Guid.NewGuid().ToString("N");
        if (!string.IsNullOrEmpty(fileExtension))
        {
            string ext = fileExtension.StartsWith('.') ? fileExtension : "." + fileExtension;
            key += ext;
        }
        return key;
    }
}
