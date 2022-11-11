using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Westermo.Nuget.ReadThroughCache.Services.V2;

namespace Westermo.Nuget.ReadThroughCache.Controllers.V2;

[ApiController]
[Route("/v2/FindPackagesById()")]
public class FindPackagesController : ControllerBase
{
    private readonly IPackageCache m_packageCache;

    public FindPackagesController(IPackageCache packageCache)
    {
        m_packageCache = packageCache ?? throw new ArgumentNullException(nameof(packageCache));
    }

    [HttpGet]
    public async Task<IActionResult> FindPackagesById([FromQuery(Name = "id")] string packageId, 
                                                      [FromQuery(Name = "semVerLevel")] string? semVerLevel,
                                                      CancellationToken cancellationToken)
    {
        var stream = await m_packageCache.GetPackageInformation(packageId.Trim('\''), semVerLevel, cancellationToken);
        if (stream == null)
            return File(s_noSuchPackage, "application/atom+xml;type=feed;charset=utf-8");

        return File(stream, "application/atom+xml;type=feed;charset=utf-8");
    }

    private static readonly byte[] s_noSuchPackage = new UTF8Encoding(false).GetBytes(
        @"<?xml version=""1.0"" encoding=""utf-8""?>
        <feed xml:base=""https://nuget.devexpress.com/bRbjzcXlzCFnP9BRjCZhggmsg5kqXxhZdRyVHKbARKCY8yxxIF/api"" xmlns=""http://www.w3.org/2005/Atom"" xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices"" xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"" xmlns:georss=""http://www.georss.org/georss"" xmlns:gml=""http://www.opengis.net/gml"">
            <id>http://schemas.datacontract.org/2004/07/</id>
            <title />
            <updated>2022-11-11T12:03:26Z</updated>
            <link rel=""self"" href=""https://nuget.devexpress.com/bRbjzcXlzCFnP9BRjCZhggmsg5kqXxhZdRyVHKbARKCY8yxxIF/api/Packages"" />
            <author>
                <name />
        </author>
        </feed>
    ");
}