namespace Westermo.Nuget.ReadThroughCache.Configuration;

public class RemotePackageRepositoryConfigurationItem
{
    public string Name { get; init; } = null!;
    public string ServiceIndex { get; init; } = null!;
    public int? Version { get; init; } = 3;
}