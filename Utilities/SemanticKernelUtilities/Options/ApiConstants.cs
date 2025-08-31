namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.Options
{
    public class ApiConstants
    {
        public const string groqApiKey = "";
        public const string groqModelName = "llama-3.3-70b-versatile";
        public const string userId = "7299";
        public const string threadId = "1";
        public const int defaultConversationBufferWindow = 6;
        public const int summarizeThreshold = 2;
        public const int summarizeEveryAssistantReplyCounts = 5;
        public const string defaultSystemPrompt = "You are a helpful assistant.";
        public const int maxTokens = 1000;
        public const double temperature = 0.55;
        public const double topP = 0.8;
        public const double presencePenalty = 0.25;
        public const double frequencePenalty = 0.25;
    }
}