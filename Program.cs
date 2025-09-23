using System.Reflection;
using AltGen;
using AltGen.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;
using Mastodon = AltGen.Mastodon;

internal class Program
{
    private static void Main(string[] args)
    {
        var serviceProvider = CreateServiceProvider();
        var mastodon = serviceProvider.GetRequiredService<Mastodon>();

       
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Secrets>(
            Newtonsoft.Json.JsonConvert.DeserializeObject<AltGen.Config.Secrets>(File.ReadAllText("secrets.json")) ??
            throw new Exception("Cannot read config"));
        services.AddScoped<Mastodon>();
        services.AddScoped<OpenAiAltGen>();
        
        services.AddLogging(cfg => cfg.SetMinimumLevel(LogLevel.Debug));
        services.AddSerilog(cfg =>
        {
            cfg.MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("job", Assembly.GetEntryAssembly()?.GetName().Name)
                .Enrich.WithProperty("service", Assembly.GetEntryAssembly()?.GetName().Name)
                .Enrich.WithProperty("desktop", Environment.GetEnvironmentVariable("DESKTOP_SESSION"))
                .Enrich.WithProperty("language", Environment.GetEnvironmentVariable("LANGUAGE"))
                .Enrich.WithProperty("lc", Environment.GetEnvironmentVariable("LC_NAME"))
                .Enrich.WithProperty("timezone", Environment.GetEnvironmentVariable("TZ"))
                .Enrich.WithProperty("dotnetVersion", Environment.GetEnvironmentVariable("DOTNET_VERSION"))
                .Enrich.WithProperty("inContainer",
                    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"))
                .WriteTo.GrafanaLoki(Environment.GetEnvironmentVariable("LOKIURL") ?? "http://thebeast:3100",
                    propertiesAsLabels: ["job"]);
            if (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ==
                "Debug")
            {
                cfg.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
            else
            {
                cfg.WriteTo.Console();
            }
        });
        return services.BuildServiceProvider();
    }
}