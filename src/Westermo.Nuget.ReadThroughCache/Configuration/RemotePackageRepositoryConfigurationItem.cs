using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Westermo.Nuget.ReadThroughCache.Configuration;

public class RemotePackageRepositoryConfigurationItem
{
    private readonly string[] m_preferredPackagePrefixes = Array.Empty<string>();
    private readonly string[] m_deniedPackagePrefixes = Array.Empty<string>();

    public string Name { get; init; } = null!;
    public string ServiceIndex { get; init; } = null!;
    public int? Version { get; init; } = 3;

    public string[] PreferredPackagePrefixes
    {
        get => m_preferredPackagePrefixes;
        init
        {
            PreferredPackagePrefixesAsRegex = value.Select(v => new Regex(v, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase))
                                                   .ToArray();
            m_preferredPackagePrefixes = value;
        }
    }

    public string[] DeniedPackagePrefixes
    {
        get => m_deniedPackagePrefixes;
        init
        {
            DeniedPackagePrefixesAsRegex = value.Select(v => new Regex(v, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase))
                                                .ToArray();
            m_deniedPackagePrefixes = value;
        }
    }

    public Regex[] PreferredPackagePrefixesAsRegex { get; private set; } = Array.Empty<Regex>();
    public Regex[] DeniedPackagePrefixesAsRegex { get; private set; } = Array.Empty<Regex>();
}