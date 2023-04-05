using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Westermo.Nuget.ReadThroughCache.Configuration;

namespace Westermo.Nuget.ReadThroughCache.Services.V2;

public interface IRemotePackageRepositoryCollection : IEnumerable<IRemotePackageRepository>
{
}

public class RemotePackageRepositoryCollection : IRemotePackageRepositoryCollection
{
    private readonly ILogger<RemotePackageRepository> m_logger;
    private readonly ImmutableArray<IRemotePackageRepository> m_remotePackageRepositories;
    
    public RemotePackageRepositoryCollection(IConfiguration configuration,
                                             IClock clock,
                                             IHttpClient httpClient,
                                             ILogger<RemotePackageRepository> logger)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (clock == null) throw new ArgumentNullException(nameof(clock));
        if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
        m_logger = logger;

        var configItems = new List<RemotePackageRepositoryConfigurationItem>();
        configuration.Bind("PackageRepositories", configItems);

        m_remotePackageRepositories = configItems.Where(item => item.Version == 2)
                                                 .Select(item => new RemotePackageRepository(
                                                             httpClient,
                                                             item.Name,
                                                             new Uri(item.ServiceIndex, UriKind.Absolute),
                                                             item.PreferredPackagePrefixesAsRegex,
                                                             item.DeniedPackagePrefixesAsRegex,
                                                             m_logger
                                                         )
                                                  )
                                                 .Cast<IRemotePackageRepository>()
                                                 .ToImmutableArray();
    }
    
    public IEnumerator<IRemotePackageRepository> GetEnumerator()
    {
        return ((IEnumerable<IRemotePackageRepository>) m_remotePackageRepositories).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}