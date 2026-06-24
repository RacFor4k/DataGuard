namespace Server.Storage.Interfaces;

public interface IStorageBlobStore
{
    Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken ct = default);
    /// <summary>
    /// Потоковая загрузка объекта из хранилища напрямую в целевой поток без промежуточной буферизации.
    /// </summary>
    Task GetObjectToStreamAsync(string bucketName, string objectName, Stream targetStream, CancellationToken ct = default);
    Task PutObjectAsync(string bucketName, string objectName, Stream content, long size, CancellationToken ct = default);
    Task CopyObjectAsync(string sourceBucket, string sourceObject, string destBucket, string destObject, CancellationToken ct = default);
    Task RemoveObjectAsync(string bucketName, string objectName, CancellationToken ct = default);
    Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct = default);
    string GenerateStorageKey(string fileExtension);
}
