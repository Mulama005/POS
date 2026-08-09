using Microsoft.AspNetCore.Mvc;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Constants;

namespace Pos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StorageSmokeTestController : ControllerBase
{
    private readonly IStorageService _storageService;

    public StorageSmokeTestController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <summary>
    /// Upload a file to the `products` folder and get a short-lived signed URL.
    /// Use this to verify uploads and signed URLs from a browser (link expires by default in 5 minutes).
    /// </summary>
    [HttpPost("upload-test")]
    public async Task<IActionResult> UploadTest([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        await using var stream = file.OpenReadStream();
        var path = await _storageService.UploadAsync(stream, file.FileName, file.ContentType ?? "application/octet-stream", StorageFolders.Products);
        var url = await _storageService.GetSignedUrlAsync(path);

        return Ok(new { path, url });
    }

    /// <summary>
    /// Delete a previously uploaded file by storage path.
    /// </summary>
    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return BadRequest("storagePath is required as a query parameter.");

        await _storageService.DeleteAsync(storagePath);
        return NoContent();
    }
}
