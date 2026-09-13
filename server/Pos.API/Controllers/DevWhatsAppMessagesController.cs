using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

/// <summary>Dev-only inspection of what MockWhatsAppService has "sent" — delete this
/// controller once a real Meta account/templates are wired up via WhatsAppCloudApiService.</summary>
[ApiController]
[Route("api/dev/whatsapp-messages")]
[Authorize]
public sealed class DevWhatsAppMessagesController : ControllerBase
{
    private readonly PosDbContext _db;
    private readonly IWebHostEnvironment _env;

    public DevWhatsAppMessagesController(PosDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var messages = await _db.SentWhatsAppMessages
            .OrderByDescending(m => m.SentAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Ok(messages);
    }
}
