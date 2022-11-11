using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Westermo.Nuget.ReadThroughCache.Services.V2;

public interface IRemotePackageRepository
{
    string Name { get; }
    Uri ServiceIndex { get; }
    
    Task<XDocument?> GetPackageInformation(string packageId, string? semVerLevel, CancellationToken cancellationToken);
}