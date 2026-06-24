using Microsoft.AspNetCore.Mvc;
using Server.Storage.Interfaces;

namespace Server.Storage.Controllers;

[ApiController]
[Route("storage")]
public class StorageLinksController : ControllerBase
{
    private readonly IStorageLinkService _linkService;
    private readonly IStorageBlobStore _blobStore;
    private readonly IStorageFileRepository _fileRepo;
    private readonly ILogger<StorageLinksController> _logger;

    public StorageLinksController(
        IStorageLinkService linkService,
        IStorageBlobStore blobStore,
        IStorageFileRepository fileRepo,
        ILogger<StorageLinksController> logger)
    {
        _linkService = linkService;
        _blobStore = blobStore;
        _fileRepo = fileRepo;
        _logger = logger;
    }

    [HttpGet("links/{token}")]
    public async Task<IActionResult> GetLink(string token, CancellationToken ct)
    {
        var link = await _linkService.GetLinkAsync(token, ct);
        if (link == null)
            return NotFound();

        if (link.ExpiresAtUtc < DateTime.UtcNow)
            return StatusCode(410, "Ссылка истекла.");

        return Redirect($"/storage/direct/{token}");
    }

    [HttpGet("direct/{token}")]
    public async Task<IActionResult> GetDirectLink(string token, CancellationToken ct)
    {
        var link = await _linkService.GetLinkAsync(token, ct);
        if (link == null)
            return NotFound();

        if (link.ExpiresAtUtc < DateTime.UtcNow)
            return StatusCode(410, "Ссылка истекла.");

        var file = await _fileRepo.GetFileAsync(link.FileId, link.OwnerId, ct);
        if (file == null)
            return NotFound();

        try
        {
            var stream = await _blobStore.GetObjectAsync(file.BucketName, file.StorageKey, ct);
            return File(stream, "application/octet-stream", file.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file {FileId} via direct link", file.FileId);
            return StatusCode(500, "Ошибка при загрузке файла.");
        }
    }
}
