using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;

namespace Westermo.Nuget.ReadThroughCache.Services.V2;

public interface ILocalPackageCache
{
    Task<Stream?> GetPackageInformation(string packageId, string? semVerLevel, CancellationToken cancellationToken);
    Task<Stream?> GetPackageContent(string packageId, string packageVersion, CancellationToken cancellationToken);
    Task SetPackageInformation(string packageId, XDocument packageInformation);
    Task SetPackageContent(string packageId, string packageVersion, Stream content);
    Task<Uri?> FindRemoteDownloadUrl(string packageId, string packageVersion, CancellationToken cancellationToken);
}

public class LocalPackageCache : ILocalPackageCache
{
    private readonly IFileSystem m_fileSystem;
    private readonly string m_packagesDirectory;
    private readonly Uri m_uriBaseForV2Urls;
    
    public LocalPackageCache(IConfiguration configuration,
                             IFileSystem fileSystem)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        m_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

        var packageCacheRootDirectory = configuration["PackageCacheRootDirectory"] ?? throw new ApplicationException("PackageCacheRootDirectory has not been configured");
        m_packagesDirectory = Path.Combine(packageCacheRootDirectory, "v2", "packages");

        m_uriBaseForV2Urls = new Uri(configuration["UriBaseForV2Urls"] ?? throw new ApplicationException("UriBaseForV2Urls has not been configured"), UriKind.Absolute);

        if (!m_fileSystem.DirectoryExists(m_packagesDirectory))
            m_fileSystem.CreateDirectory(m_packagesDirectory);
    }

    private string PackageDirPath(string packageId) => Path.Combine(m_packagesDirectory, packageId.ToLowerInvariant());
    private string InfoFilePath(string packageId) => Path.Combine(PackageDirPath(packageId), "info.xml");
    private string PackageVersionDirPath(string packageId, string version) => Path.Combine(PackageDirPath(packageId), version.ToLowerInvariant());
    private string PackageNupkgFilePath(string packageId, string version) => Path.Combine(PackageVersionDirPath(packageId, version), $"{packageId.ToLowerInvariant()}.{version.ToLowerInvariant()}.nupkg");
    
    public async Task<Stream?> GetPackageInformation(string packageId, string? semVerLevel, CancellationToken cancellationToken)
    {
        if (!m_fileSystem.DirectoryExists(PackageDirPath(packageId)))
            return null;
        
        var infoFile = InfoFilePath(packageId);
        if (!m_fileSystem.FileExists(infoFile))
            return null;

        await using var sourceStream = m_fileSystem.OpenFileStream(infoFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        var doc = await XDocument.LoadAsync(sourceStream, LoadOptions.None, cancellationToken);
        PatchUrls(doc);

        var memoryStream = new MemoryStream();
        try
        {
            await doc.SaveAsync(memoryStream, SaveOptions.None, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            await memoryStream.DisposeAsync();
            throw;
        }
    }

    private static readonly XNamespace s_nsAtom = XNamespace.Get("http://www.w3.org/2005/Atom");
    private static readonly XNamespace s_nsData = XNamespace.Get("http://schemas.microsoft.com/ado/2007/08/dataservices");
    private static readonly XNamespace s_nsMetaData = XNamespace.Get("http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
    
    private void PatchUrls(XDocument doc)
    {
        var feedElement = doc.Element(s_nsAtom + "feed");
        if (feedElement == null)
            throw new ApplicationException("Missing feed element");

        var feedEntries = feedElement.Elements(s_nsAtom + "entry");
        foreach (var feedEntry in feedEntries)
        {
            var content = feedEntry.Element(s_nsAtom + "content");
            var metaData = feedEntry.Element(s_nsMetaData + "properties");
            if (metaData != null)
            {
                var packageId = metaData.Element(s_nsData + "Id");
                var version = metaData.Element(s_nsData + "Version");

                if (!string.IsNullOrWhiteSpace(packageId?.Value) && !string.IsNullOrWhiteSpace(version?.Value))
                {
                    var scrubbedPackageId = HttpUtility.UrlEncode(packageId.Value.ToLowerInvariant());
                    var scrubbedVersion = HttpUtility.UrlEncode(version.Value.ToLowerInvariant());
                    var newSourceUri = new Uri(m_uriBaseForV2Urls, $"package-content/{scrubbedPackageId}/{scrubbedVersion}/{scrubbedPackageId}.{scrubbedVersion}.nupkg");

                    if (content != null)
                    {
                        content.SetAttributeValue("src", newSourceUri);
                    }
                    else
                    {
                        feedEntry.Add(
                            new XElement(
                                s_nsAtom + "content",
                                new XAttribute("type", "application/zip"),
                                new XAttribute("src", newSourceUri)
                            )
                        );
                    }
                }
            }
        }
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

    public async Task SetPackageInformation(string packageId, XDocument packageInformation)
    {
        if (!m_fileSystem.DirectoryExists(PackageDirPath(packageId)))
            m_fileSystem.CreateDirectory(PackageDirPath(packageId));

        var infoFile = InfoFilePath(packageId);
        await using var fileStream = m_fileSystem.OpenFileStream(infoFile, FileMode.Create, FileAccess.Write, FileShare.Read);
        await packageInformation.SaveAsync(fileStream, SaveOptions.None, CancellationToken.None);
    }

    public async Task SetPackageContent(string packageId, string packageVersion, Stream content)
    {
        if (!m_fileSystem.DirectoryExists(PackageVersionDirPath(packageId, packageVersion)))
            m_fileSystem.CreateDirectory(PackageVersionDirPath(packageId, packageVersion));

        var nupkgFilePath = PackageNupkgFilePath(packageId, packageVersion);
        await using var fileStream = m_fileSystem.OpenFileStream(nupkgFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await content.CopyToAsync(fileStream, CancellationToken.None);
    }

    public async Task<Uri?> FindRemoteDownloadUrl(string packageId, string packageVersion, CancellationToken cancellationToken)
    {
        if (!m_fileSystem.DirectoryExists(PackageDirPath(packageId)))
            return null;
        
        var infoFile = InfoFilePath(packageId);
        if (!m_fileSystem.FileExists(infoFile))
            return null;

        await using var sourceStream = m_fileSystem.OpenFileStream(infoFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        var doc = await XDocument.LoadAsync(sourceStream, LoadOptions.None, cancellationToken);
        
        // Now go through the document to find the download URL
        var feedElement = doc.Element(s_nsAtom + "feed");
        if (feedElement == null)
            throw new ApplicationException("Missing feed element");

        var feedEntries = feedElement.Elements(s_nsAtom + "entry");
        foreach (var feedEntry in feedEntries)
        {
            var content = feedEntry.Element(s_nsAtom + "content");
            var metaData = feedEntry.Element(s_nsMetaData + "properties");
            if (metaData != null && content?.Attribute("src") != null)
            {
                var packageIdElement = metaData.Element(s_nsData + "Id");
                var versionElement = metaData.Element(s_nsData + "Version");

                if (string.Equals(packageIdElement?.Value, packageId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(versionElement?.Value, packageVersion, StringComparison.OrdinalIgnoreCase))
                    return new Uri(content.Attribute("src")!.Value, UriKind.Absolute);
            }
        }

        return null;
    }
}