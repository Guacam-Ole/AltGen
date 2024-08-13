using AltGen.Config;

using Mastonet;
using Mastonet.Entities;

using System.Net;
using System.Text.RegularExpressions;

namespace AltGen
{
    public class Mastodon
    {
        private readonly Secrets _secrets;

        public Mastodon(Secrets secrets)
        {
            _secrets = secrets;
        }

        public static MastodonClient Login(string instance, string accessToken)
        {
            return new MastodonClient(instance, accessToken);
        }

        public async Task<string?> GetNewPosts(string? sinceId)
        {
            var client = Login(_secrets.Mastodon.Instance, _secrets.Mastodon.AccessToken);
            var whoami = await client.GetCurrentUser();
            var newStatuses = await client.GetAccountStatuses(whoami.Id, new ArrayOptions { SinceId = sinceId, Limit = 10 }, true);
            foreach (var status in newStatuses.OrderBy(q => q.CreatedAt))
            {
                sinceId = status.Id;
                var missingAltTags = status.MediaAttachments.Where(q => string.IsNullOrWhiteSpace(q.Description));
                if (!missingAltTags.Any()) continue;
                await FixAltTags(client, status);
            }

            return sinceId;
        }

        private static string StripHtml(string content)
        {
            content = content.Replace("</p>", " \n\n");
            content = content.Replace("<br />", " \n");
            content = Regex.Replace(content, "<[a-zA-Z/].*?>", String.Empty);
            content = WebUtility.HtmlDecode(content);
            return content;
        }

        private static string FixMentions(Status status)
        {
            foreach (var mention in status.Mentions)
            {
                if (mention.UserName == mention.AccountName) continue; // Same Instance. Nothing to do
                status.Content = status.Content.Replace($"@{mention.UserName}", $"@{mention.AccountName}");
            }
            return status.Content;
        }

        private async Task FixAltTags(MastodonClient client, Status status)
        {
            bool hasChanges = false;
            var aiGen = new OpenAIAltGen(_secrets.OpenAiKey);
            if (!status.MediaAttachments.All(q => q.Url.EndsWith(".jpg") || q.Url.EndsWith(".png")))
            {
                Console.WriteLine("Sorry. Unexpected image type. Can only work with jpg and png");
                return;
            }

            var newAttachments = new List<Attachment>();
            foreach (var attachment in status.MediaAttachments)
            {
                var imageDescription = attachment.Description;
                if (string.IsNullOrWhiteSpace(imageDescription)) imageDescription = await aiGen.GetImageDescription(attachment.Url);
                if (imageDescription == null)
                {
                    Console.WriteLine("Sorry. Cannot create description");
                    return;
                }

                using (var webClient = new WebClient())
                {
                    var content = webClient.DownloadData(attachment.Url);

                    using var stream = new MemoryStream(content);
                    newAttachments.Add(await client.UploadMedia(stream, description: imageDescription));
                }
                hasChanges = true;
            }

            if (hasChanges)
            {
                status.Content = StripHtml(status.Content);
                var content = FixMentions(status);

                await client.EditStatus(status.Id, content, mediaIds: newAttachments.Select(q => q.Id));
            }
        }
    }
}