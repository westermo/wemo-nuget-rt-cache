using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Westermo.Nuget.ReadThroughCache.Services;

public interface IJsonSerializer
{
    Task<T> Deserialize<T>(Stream utf8Stream, CancellationToken cancellationToken);
    Task<T> Deserialize<T>(string path, CancellationToken cancellationToken);
    Task Serialize<T>(string path, T value, CancellationToken cancellationToken);
}

public class JsonSerializer : IJsonSerializer
{
    private readonly IFileSystem m_fileSystem;

    private readonly JsonSerializerOptions m_options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public JsonSerializer(IFileSystem fileSystem)
    {
        m_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }
    
    public async Task<T> Deserialize<T>(Stream utf8Stream, CancellationToken cancellationToken)
    {
       return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(utf8Stream, m_options, cancellationToken) ?? throw new ApplicationException($"Failed to deserialize {typeof(T).FullName}");
    }

    public async Task<T> Deserialize<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = m_fileSystem.OpenFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await Deserialize<T>(stream, cancellationToken);
    }

    public async Task Serialize<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = m_fileSystem.OpenFileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, value, m_options, cancellationToken);
    }
}