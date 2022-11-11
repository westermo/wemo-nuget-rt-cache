using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Westermo.Nuget.ReadThroughCache.Services.V2;

namespace Westermo.Nuget.ReadThroughCache.Controllers.V2;

[ApiController]
[Route("/v2/package-content/{packageId}")]
public class PackageContentController : ControllerBase
{
    private readonly IPackageCache m_packageCache;

    public PackageContentController(IPackageCache packageCache)
    {
        m_packageCache = packageCache ?? throw new ArgumentNullException(nameof(packageCache));
    }
    
    [HttpGet("{version}/{fileName}.nupkg")]
    public async Task<IActionResult> GetPackageContents([FromRoute(Name = "packageId")] string packageId,
                                                        [FromRoute(Name = "version")] string version,
                                                        [FromRoute(Name = "fileName")] string fileName,
                                                        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId) ||
            string.IsNullOrWhiteSpace(version) ||
            string.IsNullOrWhiteSpace(fileName))
            return BadRequest();

        packageId = packageId.ToLowerInvariant();
        version = version.ToLowerInvariant();
        fileName = fileName.ToLowerInvariant();

        if (fileName != $"{packageId}.{version}")
            return BadRequest();

        var contents = await m_packageCache.GetPackageContent(packageId, version, cancellationToken);
        if (contents == null)
            return NotFound();

        return File(contents, "application/zip");
    }
}