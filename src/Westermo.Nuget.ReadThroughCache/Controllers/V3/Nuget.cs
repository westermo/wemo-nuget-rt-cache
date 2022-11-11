using System.Collections.Generic;
using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Westermo.Nuget.ReadThroughCache.Controllers.V3;

public class Context
{
    [JsonPropertyName("@vocab")]
    public string Vocab { get; init; } = null!;
    
    [JsonPropertyName("comment")]
    public string Comment { get; init; } = null!;
}

public class Resource
{
    [JsonPropertyName("@id")]
    public string Id { get; init; } = null!;
    
    [JsonPropertyName("@type")]
    public string Type { get; init; } = null!;
    
    [JsonPropertyName("comment")]
    public string? Comment { get; init; }
}

public class ServiceIndex
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = null!;
    [JsonPropertyName("resources")]
    public IEnumerable<Resource> Resources { get; init; } = null!;
    [JsonPropertyName("@context")]
    public Context Context { get; init; } = null!;
}

public class VersionCollection
{
    [JsonPropertyName("versions")]
    public string[] Versions { get; init; } = null!;
}