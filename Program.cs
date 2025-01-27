using AltGen;

internal class Program
{
    private static void Main(string[] args)
    {
        var config =
            Newtonsoft.Json.JsonConvert.DeserializeObject<AltGen.Config.Secrets>(File.ReadAllText("secrets.json")) ??
            throw new Exception("Cannot read config");

        string? lastCheckedId = null;
        var mastodon = new Mastodon(config);
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
                    Console.WriteLine($"Too many requests. will wait{10 * errorCount} minutes ");
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
}