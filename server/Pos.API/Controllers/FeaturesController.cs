using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pos.Infrastructure.Etims;

namespace Pos.Api.Controllers;

/// <summary>Which optional integrations are switched on, so the UI can hide the rest.</summary>
[ApiController]
[Route("api/features")]
[Authorize]
public sealed class FeaturesController : ControllerBase
{
    private readonly EtimsOptions _etims;

    public FeaturesController(IOptions<EtimsOptions> etims) => _etims = etims.Value;

    [HttpGet]
    public IActionResult Get() => Ok(new { etimsEnabled = _etims.Enabled });
}