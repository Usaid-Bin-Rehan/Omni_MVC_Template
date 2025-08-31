using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.ThreadStore
{
    public sealed class FileThreadStore : IThreadStore
    {
        private readonly string _root;
        private static readonly JsonSerializerOptions _json = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public FileThreadStore(string root)
        {
            _root = root;
            Directory.CreateDirectory(_root);
        }

        public async Task<ChatHistory?> LoadHistoryAsync(string userId, string threadId, CancellationToken ct = default)
        {
            string? path = GetHistoryPath(userId, threadId);
            if (!File.Exists(path)) return null;
            ChatThreadDto dto = JsonSerializer.Deserialize<ChatThreadDto>(await File.ReadAllTextAsync(path, ct))!;
            return dto.ToChatHistory();
        }

        public async Task SaveHistoryAsync(string userId, string threadId, ChatHistory history, CancellationToken ct = default)
        {
            ChatThreadDto dto = ChatThreadDto.FromChatHistory(history);
            string? path = GetHistoryPath(userId, threadId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(dto, _json), ct);
        }

        public async Task SaveCheckpointAsync(string userId, string threadId, ChatHistory history, string? label = null, CancellationToken ct = default)
        {
            ChatThreadDto dto = ChatThreadDto.FromChatHistory(history);
            var (dir, _) = GetThreadDir(userId, threadId);
            string checkpointsDir = Path.Combine(dir, "checkpoints");
            Directory.CreateDirectory(checkpointsDir);
            string stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
            string name = string.IsNullOrWhiteSpace(label) ? stamp : SanitizeFile(label);
            string path = Path.Combine(checkpointsDir, $"{name}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(dto, _json), ct);
        }

        public async Task<ChatHistory?> LoadCheckpointAsync(string userId, string threadId, string label, CancellationToken ct = default)
        {
            var (dir, _) = GetThreadDir(userId, threadId);
            string path = Path.Combine(dir, "checkpoints", $"{SanitizeFile(label)}.json");
            if (!File.Exists(path)) return null;
            ChatThreadDto dto = JsonSerializer.Deserialize<ChatThreadDto>(await File.ReadAllTextAsync(path, ct))!;
            return dto.ToChatHistory();
        }

        public (string dir, string historyPath) GetThreadDir(string userId, string threadId)
        {
            string dir = Path.Combine(_root, SanitizeFile(userId), SanitizeFile(threadId));
            return (dir, Path.Combine(dir, "history.json"));
        }

        private string GetHistoryPath(string userId, string threadId) => GetThreadDir(userId, threadId).historyPath;

        private static string SanitizeFile(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        public string GetSummaryPath(string userId, string threadId)
        {
            var (dir, _) = GetThreadDir(userId, threadId);
            return Path.Combine(dir, "summary.json");
        }
    }

}
