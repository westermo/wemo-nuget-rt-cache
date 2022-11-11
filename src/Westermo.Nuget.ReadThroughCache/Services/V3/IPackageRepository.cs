using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Westermo.Nuget.ReadThroughCache.Services.V3;

public interface IPackageRepository
{
    Task<string[]?> GetPackageVersions(string packageId, CancellationToken cancellationToken);
    Task<Stream?> GetPackageContent(string packageId, string packageVersion, CancellationToken cancellationToken);
    Task<Stream?> GetPackageNuspec(string packageId, string packageVersion, CancellationToken cancellationToken);
}