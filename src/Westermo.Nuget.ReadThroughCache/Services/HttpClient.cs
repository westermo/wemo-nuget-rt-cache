using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Westermo.Nuget.ReadThroughCache.Services;

public interface IHttpClient
{
    Task<HttpResponseMessage> Get(Uri uri, CancellationToken cancellationToken);
    Task<T> GetAsJson<T>(Uri uri, CancellationToken cancellationToken);
}

public class HttpClient : IHttpClient
{
    private readonly System.Net.Http.HttpClient m_impl = new ();
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web);
    
    public Task<HttpResponseMessage> Get(Uri uri, CancellationToken cancellationToken)
    {
        return m_impl.GetAsync(uri, cancellationToken);
    }
    
    public async Task<T> GetAsJson<T>(Uri uri, CancellationToken cancellationToken)
    {
        var response = await Get(uri, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new ApplicationException($"Unexpected response from {uri}: HTTP {response.StatusCode:D}\r\n{await ReadContentAsText(response, cancellationToken)}");

        return await response.Content.ReadFromJsonAsync<T>(s_jsonSerializerOptions, cancellationToken: cancellationToken)
               ?? throw new ApplicationException($"Failed to deserialize {typeof(T).FullName} from {uri}");
    }
    
    private async Task<string> ReadContentAsText(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buf = new byte[1024]; // Read at most 1KB
        var bufLen = await stream.ReadAsync(buf.AsMemory(), cancellationToken);
        return Encoding.UTF8.GetString(buf, 0, bufLen);
    }
}
