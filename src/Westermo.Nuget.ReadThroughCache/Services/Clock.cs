using System;

namespace Westermo.Nuget.ReadThroughCache.Services;

public interface IClock
{
    DateTimeOffset Now { get; }
}

public class Clock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}