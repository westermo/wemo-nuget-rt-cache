using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Westermo.Nuget.ReadThroughCache.Services.V2;

public interface IRemotePackageRepository
{
    string Name { get; }
    Uri ServiceIndex { get; }
    Regex[] PreferredPackagePrefixes { get; } 
    Regex[] DeniedPackagePrefixes { get; } 
    
    Task<XDocument?> GetPackageInformation(string packageId, string? semVerLevel, CancellationToken cancellationToken);
}