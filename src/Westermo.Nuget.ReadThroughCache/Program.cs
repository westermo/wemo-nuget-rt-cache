using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Westermo.Nuget.ReadThroughCache.Configuration;

namespace Westermo.Nuget.ReadThroughCache;

public class Program
{
    public static int Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var feedPath = builder.Configuration["FeedFile"];
        if (feedPath == null || !File.Exists(feedPath))
        {
            Console.Error.WriteLine(feedPath == null ? "Missing configuration: FeedFile" : $"{feedPath} does not exist");
            return -1;
        }
        
        builder.Configuration.AddJsonFile(feedPath, optional: false);

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

        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        var configurationOutput = new StringBuilder();
        configurationOutput.AppendLine("Configuration:");
        configurationOutput.AppendLine($"    PackageCacheRootDirectory = {builder.Configuration["PackageCacheRootDirectory"]}");
        configurationOutput.AppendLine($"    UriBaseForV2Urls = {builder.Configuration["UriBaseForV2Urls"]}");
        
        var configItems = new List<RemotePackageRepositoryConfigurationItem>();
        app.Configuration.Bind("PackageRepositories", configItems);
        foreach (var configItem in configItems)
        {
            configurationOutput.AppendLine("    Package Repositories:");
            configurationOutput.AppendLine($"        Name = {configItem.Name}");
            configurationOutput.AppendLine($"        ServiceIndex = {configItem.ServiceIndex}");
            if (configItem.Version != null)
                configurationOutput.AppendLine($"        Version = {configItem.Version}");
            configurationOutput.AppendLine($"        PreferredPackagePrefixes = {string.Join(", ", configItem.PreferredPackagePrefixes)}");
            configurationOutput.AppendLine($"        DeniedPackagePrefixes = {string.Join(", ", configItem.DeniedPackagePrefixes)}");

        }
        
        logger.LogInformation("{ConfigurationOutput}", configurationOutput.ToString());

        app.Run();

        return 0;
    }
}