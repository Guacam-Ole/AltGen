using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace AltGen
{
    public class OpenAIAltGen
    {
        private readonly string _openAiKey;

        public OpenAIAltGen(string openAiKey)
        {
            _openAiKey = openAiKey;
        }

        public async Task<string?> GetImageDescription(string filePath)
        {
            var service = Login();
            var descriptionResult = await service.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage> {
                    ChatMessage.FromSystem("You are an image analyzer assistant that speaks German. Never use more than 1000 Characters for your reply"),
                    ChatMessage.FromUser(new List<MessageContent>
                    {
                        MessageContent.TextContent("Was ist in dem Bild?"),
                        MessageContent.ImageUrlContent(filePath)
                    })
                },
                Model = Models.Gpt_4o,
                Temperature = 0.2f,
                MaxTokens = 400
            });

            if (descriptionResult.Successful)
            {
                var content = descriptionResult.Choices.First().Message.Content;
                var cost = descriptionResult.Usage.TotalTokens;
                Console.WriteLine($"{cost}:{content}");
                return content;
            }
            Console.WriteLine($"Could not receive description: '{descriptionResult.Error}'");
            return null;
        }

        private OpenAIService Login()
        {
            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = _openAiKey
            });
            return openAiService;
        }
    }
}