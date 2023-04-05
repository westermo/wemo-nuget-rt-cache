using System;
using System.Text.RegularExpressions;

namespace Westermo.Nuget.ReadThroughCache.Services.V3;

public interface IRemotePackageRepository : IPackageRepository
{
    string Name { get; }
    Uri ServiceIndex { get; }
    Regex[] PreferredPackagePrefixes { get; } 
    Regex[] DeniedPackagePrefixes { get; }
}