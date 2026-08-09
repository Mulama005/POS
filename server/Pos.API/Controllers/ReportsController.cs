using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Common.Interfaces;

namespace Pos.Api.Controllers;

// Illustrative — this is the shape that makes the "why this matters" note in Step 5 real:
// the file itself is never public, and this endpoint (not the bucket) is what decides who
// gets a link. [Authorize(Roles = ...)] becomes fully active once Phase 2 (auth/RBAC) is
// built — until then this compiles and returns 401/403 for anyone unauthenticated by default.

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IStorageService _storageService;

    public ReportsController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <summary>
    /// Returns a short-lived signed URL for a previously generated report. Only Manager and
    /// Admin roles can call this — a cashier hitting this endpoint gets a 403, not a file.
    /// </summary>
    [HttpGet("{reportId}/download-link")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<string>> GetDownloadLink(string reportId, CancellationToken cancellationToken)
    {
        // storagePath would normally come from a Report entity lookup (report.StoragePath),
        // not built from the ID directly — shown simplified here.
        var storagePath = $"reports/{reportId}";

        var signedUrl = await _storageService.GetSignedUrlAsync(storagePath, expiresInSeconds: 300, cancellationToken);
        return Ok(new { url = signedUrl, expiresInSeconds = 300 });
    }
}
