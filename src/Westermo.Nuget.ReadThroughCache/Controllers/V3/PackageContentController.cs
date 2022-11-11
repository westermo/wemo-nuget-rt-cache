using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Westermo.Nuget.ReadThroughCache.Services.V3;

namespace Westermo.Nuget.ReadThroughCache.Controllers.V3;

[ApiController]
[Route("/v3/package-content/{packageId}")]
public class PackageContentController : ControllerBase
{
    private readonly IPackageCache m_packageCache;

    public PackageContentController(IPackageCache packageCache)
    {
        m_packageCache = packageCache ?? throw new ArgumentNullException(nameof(packageCache));
    }

    [HttpGet("index.json")]
    public async Task<IActionResult> GetPackageVersions([FromRoute(Name = "packageId")] string packageId,
                                                        CancellationToken cancellationToken)
    {
        var versions = await m_packageCache.GetPackageVersions(packageId, cancellationToken);
        if (versions == null)
            return NotFound();
        
        return Ok(
            new PackageVersion
            {
                Versions = versions
            }
        );
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

    [HttpGet("{version}/{fileName}.nuspec")]
    public async Task<IActionResult> GetPackageNuspec([FromRoute(Name = "packageId")] string packageId,
                                                      [FromRoute(Name = "version")] string version,
                                                      [FromRoute(Name = "fileName")] string fileName,
                                                      CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId) ||
            string.IsNullOrWhiteSpace(version) ||
            string.IsNullOrWhiteSpace(fileName))
            return BadRequest();

        packageId = packageId.ToLower();
        version = version.ToLower();
        fileName = fileName.ToLower();

        if (packageId != fileName)
            return BadRequest();

        var contents = await m_packageCache.GetPackageNuspec(packageId, version, cancellationToken);
        if (contents == null)
            return NotFound();

        return File(contents, "text/xml");
    }
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PackageVersion
{
    [JsonPropertyName("versions")]
    public string[] Versions { get; init; } = null!;
}