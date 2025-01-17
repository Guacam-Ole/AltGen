using AltGen;

internal class Program
{
    private static void Main(string[] args)
    {
        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<AltGen.Config.Secrets>(File.ReadAllText("secrets.json")) ?? throw new Exception("Cannot read config");

        string? lastCheckedId = null;
        var mastodon = new Mastodon(config);
        int errorCount = 0;
        Console.WriteLine("Application Started");
        while (true)
        {
            try
            {
                lastCheckedId = mastodon.GetNewPosts(lastCheckedId).Result;
                errorCount = 0;
            }
            catch (Exception ex)
            {
                errorCount++;
                Console.WriteLine(ex);
                Console.WriteLine($"ErrorCount: {errorCount}");
                if (errorCount >= 5)
                {
                    Console.WriteLine("Giving up");
                    return;
                }
            }
            Thread.Sleep(10000 + errorCount * 60000);
        }
    }
}