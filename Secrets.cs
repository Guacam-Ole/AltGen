namespace AltGen.Config
{
    public class Secrets
    {
        public Mastodon Mastodon { get; set; }

        public string OpenAiKey { get; set; }
    }

    public class Mastodon
    {
        public string Instance { get; set; }
        public string AccessToken { get; set; }
    }
}