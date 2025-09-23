using AltGen.Config;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace AltGen
{
    public class OpenAiAltGen
    {
        private const int MaxLength = 500;
        private readonly Secrets _secrets;
        private readonly ILogger<OpenAiAltGen> _logger;

        public OpenAiAltGen(Secrets secrets, ILogger<OpenAiAltGen> logger)
        {
            _secrets = secrets;
            _logger = logger;
        }

        public async Task<string?> GetImageDescription(string filePath)
        {
            var service = Login();
            var descriptionResult = await service.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage>
                {
                    ChatMessage.FromSystem(
                        $"You are an image analyzer assistant that speaks German. Never use more than {MaxLength} Characters for your reply"),
                    ChatMessage.FromUser(new List<MessageContent>
                    {
                        MessageContent.TextContent("Was ist in dem Bild?"),
                        MessageContent.ImageUrlContent(filePath)
                    })
                },
                Model = Models.Gpt_4o_mini,
                Temperature = 0.2f,
                MaxTokens = 400
            });

            if (descriptionResult.Successful)
            {
                var content = descriptionResult.Choices.First().Message.Content;
                var cost = descriptionResult.Usage.TotalTokens;
                if (content?.Length > MaxLength) content = content[..MaxLength];
                _logger.LogDebug("Successfully received a description with '{Cost}' Tokens: {Content}", cost, content);
                return content;
            }

            _logger.LogWarning("Could not receive description: '{Error}'", descriptionResult.Error?.Message);
            return null;
        }

        private OpenAIService Login()
        {
            var openAiService = new OpenAIService(new OpenAiOptions
            {
                ApiKey = _secrets.OpenAiKey
            });
            return openAiService;
        }
    }
}