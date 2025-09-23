using AltGen.Config;
using Mastonet;
using Mastonet.Entities;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AltGen
{
    public class Mastodon
    {
        private readonly Secrets _secrets;
        private readonly OpenAiAltGen _altGen;
        private readonly ILogger<Mastodon> _logger;
        private string? _lastImageChecked = null;

        public Mastodon(Secrets secrets, OpenAiAltGen altGen, ILogger<Mastodon> logger)
        {
            _secrets = secrets;
            _altGen = altGen;
            _logger = logger;
        }

        private static MastodonClient Login(string instance, string accessToken)
        {
            return new MastodonClient(instance, accessToken);
        }

        public async Task<string?> GetNewPosts(string? sinceId)
        {
            try
            {
                var client = Login(_secrets.Mastodon.Instance, _secrets.Mastodon.AccessToken);
                var whoami = await client.GetCurrentUser();
                var newStatuses = await client.GetAccountStatuses(whoami.Id,
                    new ArrayOptions { SinceId = sinceId, Limit = 10 }, true);
                foreach (var status in newStatuses.OrderBy(q => q.CreatedAt))
                {
                    sinceId = status.Id;
                    var missingAltTags = status.MediaAttachments.Where(q => string.IsNullOrWhiteSpace(q.Description));
                    if (!missingAltTags.Any()) continue;
                    _logger.LogDebug("Received Status without ALT. Try to add ALT-Tag.Contents: {Content}",
                        status.Content);

                    for (int i = 0; i < 10; i++)
                    {
                        var success = await FixAltTags(client, status);
                        if (success) break;
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                    }
                }

                return sinceId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when trying to receive new Posts. Will try again later");
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

        private async Task<bool> FixAltTags(MastodonClient client, Status status)
        {
            string[] supportedExtensions = [".jpeg", ".jpg", ".png", ".ping"];
            var hasChanges = false;


            var newAttachments = new List<Attachment>();
            foreach (var attachment in status.MediaAttachments)
            {
                if (_lastImageChecked != null && _lastImageChecked == attachment.Id)
                {
                    _logger.LogDebug("Image '{Url}' with id '{Id}' already checked. Will ignore it", attachment.Url,
                        attachment.Id);
                    continue;
                }

                _lastImageChecked = attachment.Id;
                var imageDescription = attachment.Description;
                var imageFile = attachment.Url;
                if (!supportedExtensions.Any(q => attachment.Url.EndsWith(q)))
                {
                    _logger.LogWarning(
                        "'{Url}' does not end with an expected extension. Will try with preview Image instead",
                        attachment.Url);
                    imageFile = attachment.PreviewUrl;
                }

                if (string.IsNullOrWhiteSpace(imageDescription))
                    imageDescription = await _altGen.GetImageDescription(imageFile);
                if (imageDescription == null)
                {
                    _logger.LogWarning("Sorry. Cannot create description");
                    return false;
                }

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(attachment.Url);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsByteArrayAsync();

                    using var stream = new MemoryStream(content);
                    newAttachments.Add(await client.UploadMedia(stream, description: imageDescription));
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }

                hasChanges = true;
            }

            if (hasChanges)
            {
                status.Content = StripHtml(status.Content);
                var content = FixMentions(status);
                _logger.LogInformation("New Content with length '{Length}':'{Content}'", content.Length, content);
                if (content.Length > 500) content = content[..500];
                await client.EditStatus(status.Id, content, mediaIds: newAttachments.Select(q => q.Id));
            }

            return true;
        }
    }
}