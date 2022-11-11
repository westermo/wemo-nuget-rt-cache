using Microsoft.AspNetCore.Mvc;

namespace Westermo.Nuget.ReadThroughCache.Controllers.V3;

[ApiController]
[Route("/v3/package-metadata")]
public class PackageMetaDataController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return NoContent();
    }
}