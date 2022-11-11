using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Westermo.Nuget.ReadThroughCache;

public static class Program
{
    public static int Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<Services.V3.IPackageCache, Services.V3.PackageCache>();
        builder.Services.AddSingleton<Services.V3.ILocalPackageCache, Services.V3.LocalPackageCache>();
        builder.Services.AddSingleton<Services.V3.IRemotePackageRepositoryCollection, Services.V3.RemotePackageRepositoryCollection>();
       
        builder.Services.AddSingleton<Services.V2.IPackageCache, Services.V2.PackageCache>();
        builder.Services.AddSingleton<Services.V2.ILocalPackageCache, Services.V2.LocalPackageCache>();
        builder.Services.AddSingleton<Services.V2.IRemotePackageRepositoryCollection, Services.V2.RemotePackageRepositoryCollection>();

        builder.Services.AddSingleton<Services.IJsonSerializer, Services.JsonSerializer>();
        builder.Services.AddSingleton<Services.IFileSystem, Services.FileSystem>();
        builder.Services.AddSingleton<Services.IClock, Services.Clock>();
        builder.Services.AddSingleton<Services.IHttpClient, Services.HttpClient>();
        builder.Services.AddControllers()
               .AddJsonOptions(o => o.JsonSerializerOptions.WriteIndented = true);
        builder.Services.AddHttpContextAccessor();

        var app = builder.Build();

        app.MapControllers();

        app.Run();

        return 0;
    }
}