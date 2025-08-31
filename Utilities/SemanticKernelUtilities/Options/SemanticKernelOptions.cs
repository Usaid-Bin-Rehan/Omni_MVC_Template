namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.Options
{
    public class SemanticKernelOptions
    {
        /// <summary> Trim older messages </summary>
        public int MaxHistoryMessages { get; set; } = 200;
        /// <summary> how many last messages to send to model </summary>
        public int ConversationWindow { get; set; } = 6;
        /// <summary> when to summarize </summary>
        public int SummarizeThreshold { get; set; } = 80;
        public int MaxTokens { get; set; } = ApiConstants.maxTokens;
        public bool UseCompressionForSession { get; set; } = true;
    }
}