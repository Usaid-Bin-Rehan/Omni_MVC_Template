using Microsoft.SemanticKernel.ChatCompletion;

namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.Summarizer
{
    public interface ISummarizer
    {
        Task<string> SummarizeAsync(string userId, string threadId, ChatHistory fullHistory);
        ChatHistory FilterLastMessages(ChatHistory history, int count);
    }
}
