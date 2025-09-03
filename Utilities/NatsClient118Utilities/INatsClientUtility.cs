using NATS.Client;
using NATS.Client.JetStream;
using NATS.Client.KeyValue;
using Omni_MVC_2.Utilities.NatsClient118Utilities.Models.Request;
using Omni_MVC_2.Utilities.NatsClient118Utilities.Models.Response;

namespace Omni_MVC_2.Utilities.NatsClient118Utilities
{
    public interface INatsClientUtility : IAsyncDisposable
    {
        // Connection Management
        void Connect();
        void Disconnect();
        bool IsHealthy();

        // Publish
        Task<ResponseSendNotification> Publish(string subject, NotificationPayload message);
        Task<PublishAck> PublishAsync(string subject, byte[] data, MsgHeader? headers = null);
        Task<bool> SendNotification(SendNotificationPayload request, string userId, CancellationToken cancellationToken);

        // Stream Management
        StreamInfo CreateOrUpdateStream(StreamConfiguration config);
        bool DeleteStream(string streamName);
        bool PurgeStream(string streamName);
        StreamInfo GetStreamInfo(string streamName);
        IList<string> ListStreams(string subjectFilter = "");

        // Consumer Management
        ConsumerInfo CreateOrUpdateConsumer(string streamName, ConsumerConfiguration config);
        bool DeleteConsumer(string streamName, string consumerName);
        ConsumerInfo GetConsumerInfo(string streamName, string consumerName);
        IList<ConsumerInfo> ListConsumers(string streamName);

        // Message Inspection
        MessageInfo GetMessageBySequence(string streamName, ulong sequence);
        MessageInfo GetFirstMessage(string streamName, string subject);
        MessageInfo GetLastMessage(string streamName, string subject);

        // Subscription
        void Subscribe(string subject, string durableName, EventHandler<MsgHandlerEventArgs> messageHandler);
        Task SubscribeAsync(string subject, Action<string> onMessageReceived);
        IJetStreamPullSubscription CreatePullSubscription(string streamName, ConsumerConfiguration config);

        // Fetch a batch of messages from a pull subscription
        static IEnumerable<Msg> FetchBatch(JetStreamPullSubscription sub, int batchSize, int timeout) => throw new NotImplementedException();

        // Dead Letter Queue (DLQ) Management
        void EnsureDlqStreamExists(string dlqStreamName = "DLQStream");
        void SubscribeWithDlq(string subject, string durableName);

        // Mirror Management
        StreamInfo CreateMirrorStream(string mirrorStreamName, string sourceStreamName, string? subjectFilter = null);

        // Store Access (Key-Value)
        IKeyValue CreateKeyValueBucket(KeyValueConfiguration config);
    }
}