using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Westermo.Nuget.ReadThroughCache.Services.V3;

public interface IPackageCache : IPackageRepository
{
}

public class PackageCache : IPackageCache
{
    private readonly SemaphoreSlim m_lock = new (1, 1);
    private readonly ILocalPackageCache m_localPackageCache;
    private readonly IRemotePackageRepositoryCollection m_remotePackageRepositories;
    private readonly ILogger<PackageCache> m_logger;

    public PackageCache(ILocalPackageCache localPackageCache,
                        IRemotePackageRepositoryCollection remotePackageRepositories,
                        ILogger<PackageCache> logger)
    {
        m_localPackageCache = localPackageCache ?? throw new ArgumentNullException(nameof(localPackageCache));
        m_remotePackageRepositories = remotePackageRepositories ?? throw new ArgumentNullException(nameof(remotePackageRepositories));
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task<T> Locked<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await m_lock.WaitAsync(cancellationToken);

        try
        {
            return await action();
        }
        finally
        {
            m_lock.Release();
        }
    }

    public Task<string[]?> GetPackageVersions(string packageId, CancellationToken cancellationToken)
    {
        return Locked(async () =>
        {
            foreach (var remotePackageRepository in FindRemoteRepositories(packageId))
            {
                var remoteVersions = await remotePackageRepository.GetPackageVersions(packageId, cancellationToken);
                if (remoteVersions != null)
                {
                    m_logger.LogDebug("Got versions {Versions} for {PackageId} from {RemoteRepositoryName} ({RemoteRepositoryServiceIndex})",
                                      string.Join(", ", remoteVersions), packageId, remotePackageRepository.Name, remotePackageRepository.ServiceIndex);
                    await m_localPackageCache.SetRemoteRepository(packageId, remotePackageRepository);
                    await m_localPackageCache.SetPackageVersions(packageId, remoteVersions);
                }
            }

            var versions = await m_localPackageCache.GetPackageVersions(packageId, cancellationToken);

            if (versions == null)
                m_logger.LogError("Failed to get any package versions for {PackageId}", packageId);
            
            return versions; 
        }, cancellationToken);
    }

    public Task<Stream?> GetPackageContent(string packageId, string packageVersion, CancellationToken cancellationToken)
    {
        return Locked(async () =>
        {
            var packageStream = await m_localPackageCache.GetPackageContent(packageId, packageVersion, cancellationToken);

            if (packageStream != null)
            {
                m_logger.LogDebug("Found {PackageId}.{PackageVersion}.nupkg locally", packageId, packageVersion);
                return packageStream;
            }

            m_logger.LogDebug("Did not find {PackageId}.{PackageVersion}.nupkg locally", packageId, packageVersion);

            // Check again - someone may have acquired the package contents already while we were waiting
            // for the write lock!
            packageStream = await m_localPackageCache.GetPackageContent(packageId, packageVersion, cancellationToken);
            if (packageStream != null)
            {
                m_logger.LogDebug("Found {PackageId}.{PackageVersion}.nupkg locally", packageId, packageVersion);
                return packageStream;
            }
            
            m_logger.LogDebug("Did not find {PackageId}.{PackageVersion}.nupkg locally (second retry)", packageId, packageVersion);

            var remotePackageStream = await GetFromFirstRemote(
                packageId,
                async remotePackageRepository =>
                {
                    var stream = await remotePackageRepository.GetPackageContent(packageId, packageVersion, cancellationToken);
                    m_logger.LogDebug(
                        stream != null
                            ? "Found {PackageId}.{PackageVersion}.nupkg in {RemoteRepositoryName} ({RemoteRepositoryServiceIndex})"
                            : "Did not find {PackageId}.{PackageVersion}.nupkg locally in {RemoteRepositoryName} ({RemoteRepositoryServiceIndex}",
                        packageId, packageVersion, remotePackageRepository.Name, remotePackageRepository.ServiceIndex);
                    return stream;
                });
            
            if (remotePackageStream != null)
            {
                m_logger.LogDebug("Updating local package cache for {PackageId}.{PackageVersion}.nupkg", packageId, packageVersion);
                await m_localPackageCache.SetPackageContent(packageId, packageVersion, remotePackageStream);
                packageStream = await m_localPackageCache.GetPackageContent(packageId, packageVersion, cancellationToken);
            }

            return packageStream;
        }, cancellationToken);
    }

    public Task<Stream?> GetPackageNuspec(string packageId, string packageVersion, CancellationToken cancellationToken)
    {
        return Locked(async () =>
        {
            var nuspecStream = await m_localPackageCache.GetPackageNuspec(packageId, packageVersion, cancellationToken);

            if (nuspecStream != null)
            {
                m_logger.LogDebug("Found {PackageId}.{PackageVersion}.nuspec locally", packageId, packageVersion);
                return nuspecStream;
            }
            
            m_logger.LogDebug("Did not find {PackageId}.{PackageVersion}.nuspec locally", packageId, packageVersion);

            // Check again - someone may have acquired the package contents already while we were waiting
            // for the write lock!
            nuspecStream = await m_localPackageCache.GetPackageNuspec(packageId, packageVersion, cancellationToken);
            if (nuspecStream != null)
            {
                m_logger.LogDebug("Found {PackageId}.{PackageVersion}.nuspec locally", packageId, packageVersion);
                return nuspecStream;
            }
            
            m_logger.LogDebug("Did not find {PackageId}.{PackageVersion}.nuspec locally (second retry)", packageId, packageVersion);

            var remoteNuspecStream = await GetFromFirstRemote(
                packageId,
                async remotePackageRepository =>
                {
                    var stream = await remotePackageRepository.GetPackageNuspec(packageId, packageVersion, cancellationToken);
                    m_logger.LogDebug(
                        stream != null
                            ? "Found {PackageId}.{PackageVersion}.nuspec in {RemoteRepositoryName} ({RemoteRepositoryServiceIndex})"
                            : "Did not find {PackageId}.{PackageVersion}.nuspec in {RemoteRepositoryName} ({RemoteRepositoryServiceIndex})",
                        packageId, packageVersion, remotePackageRepository.Name, remotePackageRepository.ServiceIndex);
                    return stream;
                });

            if (remoteNuspecStream != null)
            {
                m_logger.LogDebug("Updating local package cache for {PackageId}.{PackageVersion}.nuspec", packageId, packageVersion);
                await m_localPackageCache.SetPackageNuspec(packageId, packageVersion, remoteNuspecStream);
                nuspecStream = await m_localPackageCache.GetPackageNuspec(packageId, packageVersion, cancellationToken);
            }

            return nuspecStream;
        }, cancellationToken);
    }

    private async Task<T?> GetFromFirstRemote<T>(string packageId, Func<IRemotePackageRepository, Task<T?>> action)
    {
        foreach (var remotePackageRepository in FindRemoteRepositories(packageId))
        {
            m_logger.LogDebug("Testing remote index {Index}", remotePackageRepository.ServiceIndex);
            var value = await action(remotePackageRepository);
            if (value != null)
                return value;
        }
        
        return default;
    }
    
    private IEnumerable<IRemotePackageRepository> FindRemoteRepositories(string packageId)
    {
        // Make sure we order the package repositories by the preferred package prefixes!
        foreach (var remotePackageRepository in m_remotePackageRepositories.OrderByDescending(rpp => rpp.PreferredPackagePrefixes.Any(ppp => ppp.IsMatch(packageId))))
        {
            if (remotePackageRepository.DeniedPackagePrefixes.Any(dpp => dpp.IsMatch(packageId)))
            {
                m_logger.LogDebug("Skipping package information for package {PackageId} in {RemoteRepositoryName} ({RemoteRepositoryServiceIndex}), because it's denied for this remote.",
                                  packageId, remotePackageRepository.Name, remotePackageRepository.ServiceIndex);
                continue;
            }

            yield return remotePackageRepository;
        }
    } 
}