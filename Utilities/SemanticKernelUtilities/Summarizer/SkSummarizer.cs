using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using System.Text.Json;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.ThreadStore;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.Options;

namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.Summarizer
{
    public class SkSummarizer : ISummarizer
    {
        readonly Kernel _kernel;
        readonly IThreadStore _store;
        readonly IChatCompletionService _chat;
        readonly JsonSerializerOptions jsonSerializerOptions;
        public SkSummarizer(IChatCompletionService chat, Kernel kernel, IThreadStore store)
        {
            _chat = chat;
            _kernel = kernel;
            _store = store;
            jsonSerializerOptions = new() { WriteIndented = true };
        }

        public async Task<string> SummarizeAsync(string userId, string threadId, ChatHistory fullHistory)
        {
            string summaryPath = _store.GetSummaryPath(userId, threadId);
            SummaryState? state = File.Exists(summaryPath) ? JsonSerializer.Deserialize<SummaryState>(await File.ReadAllTextAsync(summaryPath)) : new SummaryState();
            ArgumentNullException.ThrowIfNull(state);

            List<ChatMessageContent> newMessages = fullHistory
                .Skip(state.LastSummarizedIndex)
                .Where(m => m.Role != AuthorRole.System && !(m.Role == AuthorRole.User && m.Content?.TrimStart().StartsWith('/') == true))
                .ToList();

            if (!newMessages.Any()) return state.Summary ?? string.Empty;

            List<ChatMessageContent> recentNewMessages = newMessages.TakeLast(ApiConstants.defaultConversationBufferWindow).ToList();

            ChatHistory toSummarize = [];
            if (!string.IsNullOrWhiteSpace(state.Summary)) toSummarize.AddSystemMessage($"Summary so far:\n{state.Summary}");
            Console.WriteLine("\n--- Summarizer Prompt Start ---");
            Console.WriteLine(state.Summary);
            Console.WriteLine("--- End of Prompt ---\n");

            toSummarize.AddSystemMessage("Summarize the following new messages:");
            foreach (ChatMessageContent msg in recentNewMessages) toSummarize.AddMessage(msg.Role, msg.Content ?? string.Empty);

            ChatMessageContent result = await _chat.GetChatMessageContentAsync(toSummarize, new OpenAIPromptExecutionSettings(), _kernel);
            string newSummary = result.Content?.Trim() ?? state.Summary ?? string.Empty;

            string delta = DiffSummaries(state.Summary ?? string.Empty, newSummary);
            Console.WriteLine("\n🔎 Summary Delta:");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(delta);
            Console.ResetColor();

            SummaryState updated = new() { Summary = newSummary, LastSummarizedIndex = fullHistory.Count };
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
            await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(updated, jsonSerializerOptions));
            Console.WriteLine("\n📌 Summary Updated.");
            return newSummary;
        }

        private static string DiffSummaries(string oldSummary, string newSummary)
        {
            string[]? oldLines = oldSummary.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string[]? newLines = newSummary.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            IEnumerable<string>? additions = newLines.Except(oldLines).Select(x => "+ " + x);
            IEnumerable<string>? deletions = oldLines.Except(newLines).Select(x => "- " + x);

            List<string>? diff = additions.Concat(deletions).ToList();
            return diff.Any() ? string.Join("\n", diff) : "Delta: (No changes)";
        }

        public ChatHistory FilterLastMessages(ChatHistory history, int count)
        {
            return new ChatHistory(history
                    .Where(m => m.Role != AuthorRole.System && !(m.Role == AuthorRole.User && m.Content?.TrimStart().StartsWith("/") == true))
                    .TakeLast(count)
                    .ToList()
            );
        }
    }
}
