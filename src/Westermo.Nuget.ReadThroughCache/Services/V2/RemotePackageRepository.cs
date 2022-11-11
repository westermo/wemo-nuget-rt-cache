using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;

namespace Westermo.Nuget.ReadThroughCache.Services.V2;

public class RemotePackageRepository : IRemotePackageRepository
{
    private readonly IHttpClient m_httpClient;

    public RemotePackageRepository(IHttpClient httpClient,
                                   string name,
                                   Uri serviceIndex)
    {
        m_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ServiceIndex = serviceIndex ?? throw new ArgumentNullException(nameof(serviceIndex));
    }

    private static readonly XNamespace s_nsAtom = XNamespace.Get("http://www.w3.org/2005/Atom");
    
    public async Task<XDocument?> GetPackageInformation(string packageId, string? semVerLevel, CancellationToken cancellationToken)
    {
        var optionalSemVer = semVerLevel == null ? "" : $"&semVerLevel={HttpUtility.UrlEncode(semVerLevel)}";
        var uri = new Uri(ServiceIndex, $"FindPackagesById()?id='{HttpUtility.UrlEncode(packageId.Trim('\''))}'{optionalSemVer}");
        var response = await m_httpClient.Get(uri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        
        if (response.StatusCode != HttpStatusCode.OK)
            throw new ApplicationException($"Unexpected response HTTP {response.StatusCode:D} from {uri}");
        
        var doc = await XDocument.LoadAsync(await response.Content.ReadAsStreamAsync(cancellationToken), LoadOptions.None, cancellationToken);
        if ((doc.Element(s_nsAtom + "feed")?.Elements(s_nsAtom + "entry").Count() ?? 0) == 0)
            return null; // No entries in the feed
        return doc;
    }

    public string Name { get; }
    public Uri ServiceIndex { get; }
}