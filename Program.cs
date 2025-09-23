using AltGen;
using AltGen.Config;
using Microsoft.Extensions.DependencyInjection;
using Mastodon = AltGen.Mastodon;

internal class Program
{
    private static void Main(string[] args)
    {
        var serviceProvider = CreateServiceProvider();
        var mastodon = serviceProvider.GetRequiredService<Mastodon>();

        string? lastCheckedId = null;
        var errorCount = 0;
        Console.WriteLine("Application Started");
        while (true)
        {
            try
            {
                lastCheckedId = mastodon.GetNewPosts(lastCheckedId).Result;
                errorCount = 0;
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                errorCount++;
                Console.WriteLine(ex);
                Console.WriteLine($"ErrorCount: {errorCount}");
                if (ex.ToString().Contains("Too many requests"))
                {
                    Console.WriteLine($"Too many requests. will wait {10 * errorCount} minutes ");
                    Thread.Sleep(TimeSpan.FromMinutes(10 * errorCount));
                }
                else
                {
                    Thread.Sleep(TimeSpan.FromMinutes(1) * errorCount);
                }

                if (errorCount < 5) continue;
                Console.WriteLine("Giving up");
                return;
            }
        }
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Secrets>(
            Newtonsoft.Json.JsonConvert.DeserializeObject<AltGen.Config.Secrets>(File.ReadAllText("secrets.json")) ??
            throw new Exception("Cannot read config"));
        services.AddScoped<Mastodon>();
        services.AddScoped<OpenAIAltGen>();
        return services.BuildServiceProvider();
    }
}