using Microsoft.SemanticKernel.ChatCompletion;

namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.AIChatService
{
    public interface IAIChatService
    {
        ChatHistory GetHistory();
        void SaveHistory(ChatHistory history);
        void ResetConversation(string? systemPrompt = null);
        string? GetCachedSummary();
        Task<string> AskAsync(string userInput, CancellationToken ct = default);
        IAsyncEnumerable<string> AskStreamingAsync(string userInput, CancellationToken ct = default);
        string ExportToMarkdown();
    }
}