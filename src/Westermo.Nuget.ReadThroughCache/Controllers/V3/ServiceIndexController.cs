using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Westermo.Nuget.ReadThroughCache.Controllers.V3;

[ApiController]
[Route("/v3")]
public class ServiceIndexController : ControllerBase
{
    private readonly IHttpContextAccessor m_httpContextAccessor;

    public ServiceIndexController(IHttpContextAccessor httpContextAccessor)
    {
        m_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }
    
    [HttpGet("index.json")]
    public IActionResult Index()
    {
        var host = m_httpContextAccessor.HttpContext?.Request.Host;
        var protocol = m_httpContextAccessor.HttpContext?.Request.Scheme;

        var baseUrl = $"{protocol}://{host}/v3/";
        var packageBaseAddress = $"{baseUrl}package-content/";
        var packageMetaDataBaseAddress = $"{baseUrl}package-metadata/";

        
        return Ok(
            new ServiceIndex
            {
                Version = "3.0.0",
                Resources = new []
                {
                    new Resource
                    {
                        Id = packageBaseAddress,
                        Type = "PackageBaseAddress/3.0.0",
                        Comment = "Base URL of where NuGet packages are stored"
                    },
                    new Resource
                    {
                        Id = packageMetaDataBaseAddress,
                        Type = "RegistrationsBaseUrl/3.6.0",
                        Comment = "Base URL of where NuGet package metadata"
                    }
                },
                Context = new Context
                {
                    Vocab = "http://schema.nuget.org/services#",
                    Comment = "http://www.w3.org/2000/01/rdf-schema#comment"
                }
            }
        );
    }
}
