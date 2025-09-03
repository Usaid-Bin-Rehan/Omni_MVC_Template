namespace Omni_MVC_2.Utilities.NatsClient118Utilities.Models.Request
{
    public class SendNotificationPayload
    {
        public string? Message { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? At { get; set; }
        public string? SendToUserType { get; set; }
        public string? TargetNamespace { get; set; }
        public string? Tag { get; set; }
        public string? UserId { get; set; }
        public string? SenderUserId { get; set; }
        public string? TopicName { get; set; }
        public string? SenderUserType { get; set; }
        public string? RecieverUserId { get; set; }
        public string? RecieverUserType { get; set; }
    }
}