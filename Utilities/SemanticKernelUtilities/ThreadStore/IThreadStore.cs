using Microsoft.SemanticKernel.ChatCompletion;

namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.ThreadStore
{
    public interface IThreadStore
    {
        Task<ChatHistory?> LoadHistoryAsync(string userId, string threadId, CancellationToken ct = default);
        Task SaveHistoryAsync(string userId, string threadId, ChatHistory history, CancellationToken ct = default);
        Task SaveCheckpointAsync(string userId, string threadId, ChatHistory history, string? label = null, CancellationToken ct = default);
        Task<ChatHistory?> LoadCheckpointAsync(string userId, string threadId, string label, CancellationToken ct = default);
        string GetSummaryPath(string userId, string threadId);
    }
}
