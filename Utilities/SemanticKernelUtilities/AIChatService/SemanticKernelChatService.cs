using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.Options;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.Summarizer;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.AIChatService
{
    public class SemanticKernelChatService : IAIChatService
    {
        private readonly Kernel _kernel;
        private readonly ISummarizer _summarizer;
        private const string SessionKey = "ChatHistory";
        private const string SummaryKey = "ChatSummary";
        private readonly SemanticKernelOptions _options;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SemanticKernelChatService> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

        private ISession Session => _httpContextAccessor.HttpContext!.Session;

        public SemanticKernelChatService(Kernel kernel, ISummarizer summarizer, IHttpContextAccessor httpContextAccessor, IOptions<SemanticKernelOptions> opts, ILogger<SemanticKernelChatService> logger)
        {
            _summarizer = summarizer;
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = opts?.Value ?? new SemanticKernelOptions();
        }

        public ChatHistory GetHistory()
        {
            try
            {
                var raw = Session.GetString(SessionKey);
                if (string.IsNullOrEmpty(raw))
                {
                    var history = NewHistoryWithSystem();
                    SaveHistory(history);
                    return history;
                }

                var payload = _options.UseCompressionForSession ? DecompressString(raw) : raw;
                var historyObj = JsonSerializer.Deserialize<ChatHistory>(payload, _jsonOptions);
                if (historyObj == null)
                {
                    _logger.LogWarning("Session history deserialized to null -> resetting conversation.");
                    var history = NewHistoryWithSystem();
                    SaveHistory(history);
                    return history;
                }
                return historyObj;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read chat history from session. Resetting conversation.");
                var history = NewHistoryWithSystem();
                SaveHistory(history);
                return history;
            }
        }

        public void SaveHistory(ChatHistory history)
        {
            try
            {
                TrimHistory(history, _options.MaxHistoryMessages);
                var json = JsonSerializer.Serialize(history, _jsonOptions);
                var toStore = _options.UseCompressionForSession ? CompressString(json) : json;
                Session.SetString(SessionKey, toStore);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to save chat history to session due to {ex.Message}");
            }
        }

        public void ResetConversation(string? systemPrompt = null)
        {
            var history = NewHistoryWithSystem(systemPrompt ?? ApiConstants.defaultSystemPrompt);
            SaveHistory(history);
            Session.Remove(SummaryKey);
        }

        public string? GetCachedSummary()
        {
            try
            {
                return Session.GetString(SummaryKey);
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> AskAsync(string userInput, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userInput)) throw new ArgumentException("userInput cannot be empty", nameof(userInput));

            var history = GetHistory();
            history.AddUserMessage(SanitizeForStorage(userInput));

            if (ShouldSummarize(history))
            {
                await SummarizeConversationAsync(history, ct).ConfigureAwait(false);
                history = GetHistory();
            }

            var prompt = BuildPrompt(history);
            var chatSvc = _kernel.GetRequiredService<IChatCompletionService>();
            var exec = new OpenAIPromptExecutionSettings
            {
                MaxTokens = _options.MaxTokens,
                Temperature = 0.4f,
                TopP = 0.8f
            };

            ChatMessageContent reply;
            try
            {
                reply = await chatSvc.GetChatMessageContentAsync(prompt, exec, _kernel, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM request failed.");
                history.AddAssistantMessage("⚠️ Sorry, I couldn't process that right now. Try again.");
                SaveHistory(history);
                throw;
            }

            var assistantText = (reply?.Content ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(assistantText)) assistantText = "No response from model.";

            history.AddAssistantMessage(assistantText);
            SaveHistory(history);
            return assistantText;
        }

        public async IAsyncEnumerable<string> AskStreamingAsync(string userInput, [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userInput)) yield break;

            var history = GetHistory();
            history.AddUserMessage(SanitizeForStorage(userInput));
            SaveHistory(history);

            var prompt = BuildPrompt(history);
            var chatSvc = _kernel.GetRequiredService<IChatCompletionService>();

            var exec = new OpenAIPromptExecutionSettings
            {
                MaxTokens = _options.MaxTokens,
                Temperature = 0.4f,
                TopP = 0.8f
            };
            var sb = new StringBuilder();

            await foreach (var chunk in chatSvc.GetStreamingChatMessageContentsAsync(prompt, exec, _kernel, ct))
            {
                if (ct.IsCancellationRequested) yield break;
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    sb.Append(chunk.Content);
                    yield return chunk.Content;
                }
            }

            var assistantText = sb.ToString().Trim();
            if (string.IsNullOrEmpty(assistantText)) assistantText = "No response from model (stream ended empty).";

            history.AddAssistantMessage(assistantText);
            SaveHistory(history);
        }

        public string ExportToMarkdown()
        {
            var history = GetHistory();
            var md = new StringBuilder();
            foreach (var msg in history)
            {
                md.AppendLine($"### {msg.Role.ToString().ToUpperInvariant()}");
                md.AppendLine(msg.Content ?? string.Empty);
                md.AppendLine();
            }
            return md.ToString();
        }


        private static ChatHistory NewHistoryWithSystem(string? systemPrompt = null)
        {
            var history = new ChatHistory();
            history.AddSystemMessage(systemPrompt ?? ApiConstants.defaultSystemPrompt);
            return history;
        }

        private static void TrimHistory(ChatHistory history, int maxMessages)
        {
            if (history.Count <= maxMessages) return;

            // keep the system messages + last N messages
            var systemMsgs = history.Where(m => m.Role == AuthorRole.System).ToList();
            var keep = history.Where(m => m.Role != AuthorRole.System).TakeLast(maxMessages - systemMsgs.Count).ToList();

            var newHist = new ChatHistory();
            foreach (var s in systemMsgs) newHist.AddMessage(s.Role, s.Content ?? string.Empty);
            foreach (var k in keep) newHist.AddMessage(k.Role, k.Content ?? string.Empty);

            history.Clear();
            foreach (var m in newHist) history.AddMessage(m.Role, m.Content ?? string.Empty);
        }

        private ChatHistory BuildPrompt(ChatHistory history)
        {
            var prompt = new ChatHistory();
            prompt.AddSystemMessage(ApiConstants.defaultSystemPrompt);

            var recent = _summarizer.FilterLastMessages(history, _options.ConversationWindow);
            foreach (var msg in recent) prompt.AddMessage(msg.Role, msg.Content ?? string.Empty);

            return prompt;
        }

        private bool ShouldSummarize(ChatHistory history)
        {
            try
            {
                if (history.Count >= _options.SummarizeThreshold) return true;
                if (string.IsNullOrWhiteSpace(GetCachedSummary()) && history.Count > _options.SummarizeThreshold / 2) return true;
            }
            catch { }
            return false;
        }

        private async Task<string> SummarizeConversationAsync(ChatHistory history, CancellationToken ct = default)
        {
            try
            {
                var chatSvc = _kernel.GetRequiredService<IChatCompletionService>();

                var recent = _summarizer.FilterLastMessages(history, Math.Min(history.Count, 40)); // last 40 msgs
                var toSummarize = new ChatHistory();
                var existingSummary = GetCachedSummary();
                if (!string.IsNullOrEmpty(existingSummary))
                {
                    toSummarize.AddSystemMessage($"Existing summary:\n{existingSummary}\nUpdate the summary with the new messages below.");
                }
                else
                {
                    toSummarize.AddSystemMessage("Create a concise summary of the conversation so far. Keep it short and factual (bulleted list preferred).");
                }

                toSummarize.AddSystemMessage("Summarize the following new messages:");
                foreach (var m in recent) toSummarize.AddMessage(m.Role, m.Content ?? string.Empty);

                var exec = new OpenAIPromptExecutionSettings { MaxTokens = Math.Min(700, _options.MaxTokens), Temperature = 0.2f, TopP = 0.6f };

                var reply = await chatSvc.GetChatMessageContentAsync(toSummarize, exec, _kernel, ct).ConfigureAwait(false);
                var newSummary = reply?.Content?.Trim() ?? existingSummary ?? string.Empty;

                Session.SetString(SummaryKey, newSummary);
                _logger.LogInformation("Conversation summarized; length {Len}.", newSummary.Length);
                return newSummary;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Summarization failed; leaving previous summary unchanged.");
                return GetCachedSummary() ?? string.Empty;
            }
        }

        private static string SanitizeForStorage(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var t = input.Trim();
            if (t.Length > 32_000) t = t.Substring(0, 32_000); // keep session sizes finite
            return t.Replace("\r\n", "\n");
        }

        private static string CompressString(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(bytes, 0, bytes.Length);
            output.Position = 0;
            return Convert.ToBase64String(output.ToArray());
        }

        private static string DecompressString(string base64)
        {
            var bytes = Convert.FromBase64String(base64);
            using var input = new MemoryStream(bytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
