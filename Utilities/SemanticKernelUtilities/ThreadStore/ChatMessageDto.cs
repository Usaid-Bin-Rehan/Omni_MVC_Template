namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.ThreadStore
{
    public class ChatMessageDto
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
        public DateTimeOffset? CreatedUtc { get; set; }
    }
}
