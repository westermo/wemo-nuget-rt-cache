using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Westermo.Nuget.ReadThroughCache.Services.V3;

public interface ILocalPackageCache : IPackageRepository
{
    Task SetPackageVersions(string packageId, string[] localVersions);
    Task SetPackageContent(string packageId, string packageVersion, Stream content);
    Task SetPackageNuspec(string packageId, string packageVersion, Stream content);
    Task SetRemoteRepository(string packageId, IRemotePackageRepository remotePackageRepository);
}

public class LocalPackageCache : ILocalPackageCache
{
    private readonly IFileSystem m_fileSystem;
    private readonly IJsonSerializer m_jsonSerializer;
    private readonly string m_packagesDirectory;
    
    public LocalPackageCache(IConfiguration configuration,
                             IFileSystem fileSystem,
                             IJsonSerializer jsonSerializer)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        m_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        m_jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));

        var packageCacheRootDirectory = configuration["PackageCacheRootDirectory"] ?? throw new ApplicationException("PackageCacheRootDirectory has not been configured");
        m_packagesDirectory = Path.Combine(packageCacheRootDirectory, "v3", "packages");

        if (!m_fileSystem.DirectoryExists(m_packagesDirectory))
            m_fileSystem.CreateDirectory(m_packagesDirectory);
    }

    private string PackageDirPath(string packageId) => Path.Combine(m_packagesDirectory, packageId);
    private string VersionsFilePath(string packageId) => Path.Combine(PackageDirPath(packageId), "versions.json");
    private string SourceFilePath(string packageId) => Path.Combine(PackageDirPath(packageId), "source.json");
    private string PackageVersionDirPath(string packageId, string version) => Path.Combine(PackageDirPath(packageId), version);
    private string PackageNupkgFilePath(string packageId, string version) => Path.Combine(PackageVersionDirPath(packageId, version), $"{packageId}.{version}.nupkg");
    private string PackageNuspecFilePath(string packageId, string version) => Path.Combine(PackageVersionDirPath(packageId, version), $"{packageId}.{version}.nuspec");
    
    public async Task<string[]?> GetPackageVersions(string packageId, CancellationToken cancellationToken)
    {
        if (!m_fileSystem.DirectoryExists(PackageDirPath(packageId)))
            return null;
        
        var filePath = VersionsFilePath(packageId);
        if (!m_fileSystem.FileExists(filePath))
            return null;

        return await m_jsonSerializer.Deserialize<string[]>(filePath, cancellationToken)
                                     .ConfigureAwait(false);
    }

    public Task<Stream?> GetPackageContent(string packageId, string packageVersion, CancellationToken cancellationToken)
    {
        if (!m_fileSystem.DirectoryExists(PackageDirPath(packageId)))
            return Task.FromResult<Stream?>(null);
        
        if (!m_fileSystem.DirectoryExists(PackageVersionDirPath(packageId, packageVersion)))
            return Task.FromResult<Stream?>(null);

        var nupkgFilePath = PackageNupkgFilePath(packageId, packageVersion);
        if (!m_fileSystem.FileExists(nupkgFilePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(m_fileSystem.OpenFileStream(nupkgFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public Task<Stream?> GetPackageNuspec(string packageId, string packageVersion, CancellationToken cancellationToken)
    {
        if (!m_fileSystem.DirectoryExists(PackageDirPath(packageId)))
            return Task.FromResult<Stream?>(null);
        
        if (!m_fileSystem.DirectoryExists(PackageVersionDirPath(packageId, packageVersion)))
            return Task.FromResult<Stream?>(null);

        var nuspecFilePath = PackageNuspecFilePath(packageId, packageVersion);
        if (!m_fileSystem.FileExists(nuspecFilePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(m_fileSystem.OpenFileStream(nuspecFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public async Task SetPackageVersions(string packageId, string[] localVersions)
    {
        var packageDirPath = PackageDirPath(packageId);
        if (!m_fileSystem.DirectoryExists(packageDirPath))
            m_fileSystem.CreateDirectory(packageDirPath);

        await m_jsonSerializer.Serialize(VersionsFilePath(packageId), localVersions, CancellationToken.None);
    }

    public async Task SetPackageContent(string packageId, string packageVersion, Stream content)
    {
        if (!m_fileSystem.DirectoryExists(PackageVersionDirPath(packageId, packageVersion)))
            m_fileSystem.CreateDirectory(PackageVersionDirPath(packageId, packageVersion));

        var nupkgFilePath = PackageNupkgFilePath(packageId, packageVersion);
        await using var fileStream = m_fileSystem.OpenFileStream(nupkgFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await content.CopyToAsync(fileStream, CancellationToken.None);
    }

    public async Task SetPackageNuspec(string packageId, string packageVersion, Stream content)
    {
        if (!m_fileSystem.DirectoryExists(PackageVersionDirPath(packageId, packageVersion)))
            m_fileSystem.CreateDirectory(PackageVersionDirPath(packageId, packageVersion));

        var nuspecFilePath = PackageNuspecFilePath(packageId, packageVersion);
        await using var fileStream = m_fileSystem.OpenFileStream(nuspecFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await content.CopyToAsync(fileStream, CancellationToken.None);
    }
 
    public async Task SetRemoteRepository(string packageId, IRemotePackageRepository remotePackageRepository)
    {
        if (!m_fileSystem.DirectoryExists(PackageDirPath(packageId)))
            m_fileSystem.CreateDirectory(PackageDirPath(packageId));
        
        await m_jsonSerializer.Serialize(SourceFilePath(packageId), remotePackageRepository.Name, CancellationToken.None);
    }
}