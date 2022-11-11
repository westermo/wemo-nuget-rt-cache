using System;

namespace Westermo.Nuget.ReadThroughCache.Services.V3;

public interface IRemotePackageRepository : IPackageRepository
{
    string Name { get; }
    Uri ServiceIndex { get; }
}