using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Westermo.Nuget.ReadThroughCache.Services.V2;

public interface IPackageCache
{
    Task<Stream?> GetPackageInformation(string packageId, string? semVerLevel, CancellationToken cancellationToken);
    Task<Stream?> GetPackageContent(string packageId, string packageVersion, CancellationToken cancellationToken);
}

public class PackageCache : IPackageCache
{
    private readonly SemaphoreSlim m_lock = new (1, 1);
    private readonly ILocalPackageCache m_localPackageCache;
    private readonly IRemotePackageRepositoryCollection m_remotePackageRepositories;
    private readonly ILogger<PackageCache> m_logger;
    private readonly IHttpClient m_httpClient;

    public PackageCache(ILocalPackageCache localPackageCache,
                        IRemotePackageRepositoryCollection remotePackageRepositories,
                        ILogger<PackageCache> logger,
                        IHttpClient httpClient)
    {
        m_localPackageCache = localPackageCache ?? throw new ArgumentNullException(nameof(localPackageCache));
        m_remotePackageRepositories = remotePackageRepositories ?? throw new ArgumentNullException(nameof(remotePackageRepositories));
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
        m_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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

    public Task<Stream?> GetPackageInformation(string packageId, string? semVerLevel, CancellationToken cancellationToken)
    {
        return Locked(async () =>
        {
            foreach (var remotePackageRepository in FindRemoteRepositories(packageId))
            {
                var remotePackages = await remotePackageRepository.GetPackageInformation(packageId, semVerLevel, cancellationToken);
                if (remotePackages != null)
                {
                    m_logger.LogDebug("Got package information for {PackageId} from {RemoteRepositoryName} ({RemoteRepositoryServiceIndex})",
                                      packageId, remotePackageRepository.Name, remotePackageRepository.ServiceIndex);
                    await m_localPackageCache.SetPackageInformation(packageId, remotePackages);
                }
                else
                {
                    m_logger.LogDebug("Did not get package information for {PackageId} from {RemoteRepositoryName} ({RemoteRepositoryServiceIndex})",
                                      packageId, remotePackageRepository.Name, remotePackageRepository.ServiceIndex);
                }
            }

            var packages = await m_localPackageCache.GetPackageInformation(packageId, semVerLevel, cancellationToken);

            if (packages == null)
                m_logger.LogError("Failed to get any package versions for {PackageId}", packageId);
            
            return packages; 
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

            var downloadUri = await m_localPackageCache.FindRemoteDownloadUrl(packageId, packageVersion, cancellationToken);
            if (downloadUri == null)
            {
                m_logger.LogWarning("Did not find a remote download url for {PackageId}.{PackageVersion}", packageId, packageVersion);
                return null;
            } 

            var response = await m_httpClient.Get(downloadUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                m_logger.LogWarning("Package {PackageId}.{PackageVersion} was not found at {Url}", packageId, packageVersion, downloadUri);
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                m_logger.LogWarning("Accessing URL {Url} for package {PackageId}.{PackageVersion} resulted in an unexpected response: HTTP {Response}", downloadUri, packageId, packageVersion, response.StatusCode.ToString("D"));
                return null;
            }

            m_logger.LogDebug("Updating local package cache for {PackageId}.{PackageVersion}.nupkg", packageId, packageVersion);
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await m_localPackageCache.SetPackageContent(packageId, packageVersion, stream);

            return await m_localPackageCache.GetPackageContent(packageId, packageVersion, cancellationToken);
        }, cancellationToken);
    }
}