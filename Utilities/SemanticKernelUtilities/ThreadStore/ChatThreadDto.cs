using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.ThreadStore
{
    public class ChatThreadDto
    {
        public List<ChatMessageDto> Messages { get; set; } = [];

        public static ChatThreadDto FromChatHistory(ChatHistory history)
        {
            ChatThreadDto dto = new();
            foreach (ChatMessageContent msg in history)
            {
                dto.Messages.Add(new ChatMessageDto
                {
                    Role = msg.Role.Label,
                    Content = msg.Content,
                    CreatedUtc = DateTimeOffset.UtcNow,
                });
            }
            return dto;
        }

        public ChatHistory ToChatHistory()
        {
            ChatHistory history = [];
            foreach (var message in Messages)
            {
                switch (message.Role?.ToLowerInvariant())
                {
                    case "system":
                        history.AddSystemMessage(message.Content ?? string.Empty);
                        break;
                    case "user":
                        history.AddUserMessage(message.Content ?? string.Empty);
                        break;
                    case "assistant":
                        history.AddAssistantMessage(message.Content ?? string.Empty);
                        break;
                    default:
                        history.AddMessage(AuthorRole.Assistant, message.Content ?? string.Empty);
                        break;
                }
            }
            return history;
        }
    }
}