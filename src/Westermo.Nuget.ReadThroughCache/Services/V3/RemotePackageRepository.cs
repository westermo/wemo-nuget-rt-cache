using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using Microsoft.Extensions.Logging;
using Westermo.Nuget.ReadThroughCache.Controllers.V3;

namespace Westermo.Nuget.ReadThroughCache.Services.V3;

public class RemotePackageRepository : IRemotePackageRepository
{
    private record ServiceIndexWithTimestamp(ServiceIndex Value, DateTimeOffset Timestamp);

    private static readonly TimeSpan s_refreshPeriod = TimeSpan.FromDays(1);
    
    private readonly AsyncLazy<ServiceIndexWithTimestamp> m_serviceIndexValues;
    private readonly IClock m_clock;
    private readonly IHttpClient m_httpClient;
    private readonly ILogger<RemotePackageRepository> m_logger;

    public RemotePackageRepository(IClock clock,
                                   IHttpClient httpClient,
                                   ILogger<RemotePackageRepository> logger,
                                   string name,
                                   Regex[] preferredPackagePrefixes,
                                   Regex[] deniedPackagePrefixes,
                                   Uri serviceIndex)
    {
        m_clock = clock ?? throw new ArgumentNullException(nameof(clock));
        m_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        PreferredPackagePrefixes = preferredPackagePrefixes ?? throw new ArgumentNullException(nameof(preferredPackagePrefixes));
        DeniedPackagePrefixes = deniedPackagePrefixes ?? throw new ArgumentNullException(nameof(deniedPackagePrefixes));
        ServiceIndex = serviceIndex ?? throw new ArgumentNullException(nameof(serviceIndex));

        m_serviceIndexValues = new AsyncLazy<ServiceIndexWithTimestamp>(async cancellationToken =>
        {
            var value = await m_httpClient.GetAsJson<ServiceIndex>(serviceIndex, cancellationToken);
            return new ServiceIndexWithTimestamp(value, m_clock.Now);
        }, resettable: true);
    }

    private async Task<ServiceIndex> GetServiceIndex(CancellationToken cancellationToken)
    {
        var value = await m_serviceIndexValues.WithCancellation(cancellationToken);
        if (value.Timestamp + s_refreshPeriod > m_clock.Now)
        {
            m_serviceIndexValues.Reset();
            value = await m_serviceIndexValues.WithCancellation(cancellationToken);
        }

        return value.Value;
    }

    public async Task<string[]?> GetPackageVersions(string packageId, CancellationToken cancellationToken)
    {
        try
        {
            m_logger.LogDebug("About to get remote v3 versions for {PackageId}", packageId);
            var serviceIndex = await GetServiceIndex(cancellationToken);
            var endpoint = serviceIndex.Resources
                                       .FirstOrDefault(r => r.Type == "PackageBaseAddress/3.0.0")
                           ?? throw new ApplicationException($"{ServiceIndex} does not support 'PackageBaseAddress/3.0.0'");
            
            m_logger.LogDebug("Endpoint for package {PackageId} (v3): {Endpoint}", packageId, endpoint.Id);

            var uri = new Uri($"{endpoint.Id.TrimEnd('/')}/{packageId}/index.json", UriKind.Absolute);
            var versionCollection = await m_httpClient.GetAsJson<VersionCollection>(uri, cancellationToken);
            
            m_logger.LogDebug("Versions for package {PackageId} (v3): {Versions}", packageId, string.Join(", ", versionCollection.Versions));
            return versionCollection.Versions;
        }
        catch (Exception ex)
        {
            m_logger.LogError(
                ex,
                "Failed to get package versions from {ServiceIndex}: {ErrorMessage}",
                ServiceIndex,
                ex.Message
            );
            return null;
        }
    }

    public async Task<Stream?> GetPackageContent(string packageId, string packageVersion, CancellationToken cancellationToken)
    {
        try
        {
            m_logger.LogDebug("About to get remote v3 package content for {PackageId}", packageId);
            var serviceIndex = await GetServiceIndex(cancellationToken);
            var endpoint = serviceIndex.Resources
                                       .FirstOrDefault(r => r.Type == "PackageBaseAddress/3.0.0")
                           ?? throw new ApplicationException($"{ServiceIndex} does not support 'PackageBaseAddress/3.0.0'");
            
            m_logger.LogDebug("Endpoint for package {PackageId} (v3): {Endpoint}", packageId, endpoint.Id);

            var uri = new Uri($"{endpoint.Id.TrimEnd('/')}/{packageId}/{packageVersion}/{packageId}.{packageVersion}.nupkg", UriKind.Absolute);
            var response = await m_httpClient.Get(uri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                m_logger.LogDebug("Endpoint for package/content {PackageId} (v3): {Endpoint} - HTTP 404", packageId, uri);
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                m_logger.LogDebug("Endpoint for package/content {PackageId} (v3): {Endpoint} - HTTP {StatusCode}", packageId, uri, response.StatusCode.ToString("D"));
                throw new ApplicationException($"Unexpected response HTTP {response.StatusCode:D} from {uri}");
            }

            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            m_logger.LogError(
                ex,
                "Failed to get package contents from {ServiceIndex}: {ErrorMessage}",
                ServiceIndex,
                ex.Message
            );
            return null;
        }
    }

    public async Task<Stream?> GetPackageNuspec(string packageId, string packageVersion, CancellationToken cancellationToken)
    {
        try
        {
            m_logger.LogDebug("About to get remote v3 package nuspec for {PackageId}", packageId);
            
            var serviceIndex = await GetServiceIndex(cancellationToken);
            var endpoint = serviceIndex.Resources
                                       .FirstOrDefault(r => r.Type == "PackageBaseAddress/3.0.0")
                           ?? throw new ApplicationException($"{ServiceIndex} does not support 'PackageBaseAddress/3.0.0'");

            m_logger.LogDebug("Endpoint for package {PackageId} (v3): {Endpoint}", packageId, endpoint.Id);
            
            var uri = new Uri($"{endpoint.Id.TrimEnd('/')}/{packageId}/{packageVersion}/{packageId}.nuspec", UriKind.Absolute);
            var response = await m_httpClient.Get(uri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                m_logger.LogDebug("Endpoint for package/nuspec {PackageId} (v3): {Endpoint} - HTTP 404", packageId, uri);
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                m_logger.LogDebug("Endpoint for package/nuspec {PackageId} (v3): {Endpoint} - HTTP {StatusCode}", packageId, uri, response.StatusCode.ToString("D"));
                throw new ApplicationException($"Unexpected response HTTP {response.StatusCode:D} from {uri}");
            }

            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            m_logger.LogError(
                ex,
                "Failed to get package nuspec from {ServiceIndex}: {ErrorMessage}",
                ServiceIndex,
                ex.Message
            );
            return null;
        }
    }

    public string Name { get; }
    public Regex[] PreferredPackagePrefixes { get; }
    public Regex[] DeniedPackagePrefixes { get; }
    public Uri ServiceIndex { get; }
}