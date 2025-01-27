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
        private string? _lastImageChecked = null;

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
            try
            {
                var client = Login(_secrets.Mastodon.Instance, _secrets.Mastodon.AccessToken);
                var whoami = await client.GetCurrentUser();
                var newStatuses = await client.GetAccountStatuses(whoami.Id, new ArrayOptions { SinceId = sinceId, Limit = 10 }, true);
                foreach (var status in newStatuses.OrderBy(q => q.CreatedAt))
                {
                    sinceId = status.Id;
                    var missingAltTags = status.MediaAttachments.Where(q => string.IsNullOrWhiteSpace(q.Description));
                    if (!missingAltTags.Any()) continue;
                    await Console.Out.WriteLineAsync($"Received Status without ALT. try to add ALT-Tag.Contents: \n{status.Content}");
                    await FixAltTags(client, status);
                }

                return sinceId;
            }
            catch (Exception e)
            {
                if (e.ToString().Contains("Too many requests"))
                {
                    Console.WriteLine("Too many requests. will wait 10 minutes ");
                    Thread.Sleep(TimeSpan.FromMinutes(10));
                }
                Console.WriteLine(e);
                Console.WriteLine("will try again later");
                throw;
            }
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
            string[] supportedExtensions = [".jpeg", ".jpg", ".png", ".ping"];
            bool hasChanges = false;
            var aiGen = new OpenAIAltGen(_secrets.OpenAiKey);

            var newAttachments = new List<Attachment>();
            foreach (var attachment in status.MediaAttachments)
            {
                if (_lastImageChecked != null && _lastImageChecked == attachment.Id)
                {
                    Console.WriteLine($"Image '{attachment.Url}' with id '{attachment.Id}' already checked. Will ignore it");
                    continue;
                }

                _lastImageChecked = attachment.Id;
                var imageDescription = attachment.Description;
                var imageFile = attachment.Url;
                if (!supportedExtensions.Any(q => attachment.Url.EndsWith(q)))
                {
                    Console.WriteLine($"'{attachment.Url}' does not end with an expected imagetype. Will try with preview Image instead");
                    imageFile = attachment.PreviewUrl;
                }
         
                if (string.IsNullOrWhiteSpace(imageDescription)) imageDescription = await aiGen.GetImageDescription(imageFile);
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
                    Thread.Sleep(TimeSpan.FromSeconds(10));
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