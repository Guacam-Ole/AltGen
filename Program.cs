using AltGen;

internal class Program
{
    private static void Main(string[] args)
    {
        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<AltGen.Config.Secrets>(File.ReadAllText("secrets.json")) ?? throw new Exception("Cannot read config");

        string? lastCheckedId = null;
        var mastodon = new Mastodon(config);
        while (true)
        {
            lastCheckedId = mastodon.GetNewPosts(lastCheckedId).Result;
            Thread.Sleep(10000);
        }
    }
}