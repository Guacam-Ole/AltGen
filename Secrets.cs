using JetBrains.Annotations;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace AltGen.Config
{
    public class Secrets
    {
        public Mastodon Mastodon { get; set; }

        public string OpenAiKey { get; set; }
    }

    [UsedImplicitly]
    public class Mastodon
    {
        public string Instance { get; set; }
        public string AccessToken { get; set; }
    }
}