using NATS.Client;
using NATS.Client.JetStream;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NATS.Client.KeyValue;
using NATS.Client.Internals;
using Omni_MVC_2.Utilities.NatsClient118Utilities.Models.Request;
using Omni_MVC_2.Utilities.NatsClient118Utilities.Models.Response;

namespace Omni_MVC_2.Utilities.NatsClient118Utilities
{
    public class OmniNatsClientUtility : INatsClientUtility
    {
        private readonly ILogger<OmniNatsClientUtility> _logger;
        private readonly NatsOptions _options;
        private IConnection? _connection;
        private IJetStream? _jetStream;

        public OmniNatsClientUtility(IOptions<NatsOptions> options, ILogger<OmniNatsClientUtility> logger)
        {
            _logger = logger;
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            ArgumentException.ThrowIfNullOrEmpty(_options.Url, nameof(_options.Url));
        }

        #region Connection
        public void Connect()
        {
            if (_connection?.IsClosed() == false)
            {
                _logger.LogInformation("Already connected to NATS.");
                return;
            }
            try
            {
                var opts = ConnectionFactory.GetDefaultOptions();
                opts.Url = _options.Url;
                opts.ReconnectWait = 2000;
                opts.User = _options.User;
                opts.Password = _options.Password;
                opts.MaxReconnect = NATS.Client.Options.ReconnectForever;

                opts.ClosedEventHandler += (_, __) => _logger.LogWarning("NATS connection closed.");
                opts.DisconnectedEventHandler += (_, __) => _logger.LogWarning("NATS disconnected — reconnecting...");
                opts.ReconnectedEventHandler += (_, __) => _logger.LogInformation("NATS reconnected.");

                _connection = new ConnectionFactory().CreateConnection(opts);
                _jetStream = _connection.CreateJetStreamContext();

                _logger.LogInformation("Connected to NATS at {Url}", _options.Url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to NATS.");
                throw;
            }
        }

        public bool IsHealthy()
        {
            try
            {
                return _connection != null && !_connection.IsClosed();
            }
            catch
            {
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                _connection?.Drain();
                _connection?.Close();
                _logger.LogInformation("Disconnected from NATS.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while disconnecting from NATS.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            Disconnect();
            if (_connection is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
            else if (_connection is IDisposable disposable) disposable.Dispose();
            GC.SuppressFinalize(this);
        }
        #endregion Connection

        #region Stream Management
        public static StreamConfiguration BuildStreamConfiguration(string streamName, List<string> subjects, StorageType storageType = StorageType.File, RetentionPolicy retention = RetentionPolicy.Limits, DiscardPolicy discard = DiscardPolicy.Old, int maxMsgs = -1, long maxBytes = -1, int replicas = 1, TimeSpan? maxAge = null, bool allowDirect = true, bool denyDelete = false, bool allowRollup = false)
        {
            var builder = StreamConfiguration.Builder()
                .WithName(streamName)
                .WithStorageType(storageType)
                .WithRetentionPolicy(retention)
                .WithDiscardPolicy(discard)
                .WithSubjects(subjects.ToArray())
                .WithReplicas(replicas)
                .WithAllowDirect(allowDirect)
                .WithDenyDelete(denyDelete)
                .WithAllowRollup(allowRollup);

            if (maxMsgs >= 0) builder.WithMaxMessages(maxMsgs);
            if (maxBytes >= 0) builder.WithMaxBytes(maxBytes);
            if (maxAge.HasValue) builder.WithMaxAge(Duration.OfMillis((long)maxAge.Value.TotalMilliseconds));
            return builder.Build();
        }

        public StreamInfo CreateOrUpdateStream(StreamConfiguration config)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return jsm.AddStream(config);
        }

        public bool DeleteStream(string streamName)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return jsm.DeleteStream(streamName);
        }

        public bool PurgeStream(string streamName)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            jsm.PurgeStream(streamName);
            return true;
        }

        public StreamInfo GetStreamInfo(string streamName)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return jsm.GetStreamInfo(streamName);
        }

        public IList<string> ListStreams(string subjectFilter = "")
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return string.IsNullOrEmpty(subjectFilter) ? jsm.GetStreamNames() : jsm.GetStreamNames(subjectFilter);
        }
        #endregion Stream Management

        #region Consumer Management
        public ConsumerInfo CreateOrUpdateConsumer(string streamName, ConsumerConfiguration config)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return jsm.AddOrUpdateConsumer(streamName, config);
        }

        public bool DeleteConsumer(string streamName, string consumerName)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return jsm.DeleteConsumer(streamName, consumerName);
        }

        public ConsumerInfo GetConsumerInfo(string streamName, string consumerName)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return jsm.GetConsumerInfo(streamName, consumerName);
        }

        public IList<ConsumerInfo> ListConsumers(string streamName)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return jsm.GetConsumers(streamName);
        }
        #endregion Consumer Management

        #region Message Inspection
        public MessageInfo GetMessageBySequence(string streamName, ulong sequence)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return jsm.GetMessage(streamName, sequence);
        }

        public MessageInfo GetFirstMessage(string streamName, string subject)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return jsm.GetFirstMessage(streamName, subject);
        }

        public MessageInfo GetLastMessage(string streamName, string subject)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            return jsm.GetLastMessage(streamName, subject);
        }
        #endregion Message Inspection

        #region Publish
        public async Task<ResponseSendNotification> Publish(string subject, NotificationPayload message)
        {
            subject = $"notifications.{subject}";
            try
            {
                if (_jetStream == null || _connection == null)
                {
                    _logger.LogWarning("JetStream context or connection not initialized. Calling Connect()...");
                    Connect();
                }

                EnsureStreamExists();

                string json = JsonSerializer.Serialize(message);
                Msg msg = new(subject, Encoding.UTF8.GetBytes(json));

                _jetStream!.Publish(msg);
                _logger.LogInformation("Published message to {Subject}: {Payload}", subject, json);

                await Task.CompletedTask;
                return new ResponseSendNotification { Message = $"Notification sent to subject '{subject}' successfully.", IsSuccess = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to {Subject}", subject);
                return new ResponseSendNotification
                {
                    Message = $"Error publishing message: {ex.Message}",
                    IsSuccess = false
                };
            }
        }

        public async Task<PublishAck> PublishAsync(string subject, byte[] data, MsgHeader? headers = null)
        {
            if (_jetStream == null) Connect();
            var msg = new Msg(subject, headers ?? [], data);
            return await _jetStream!.PublishAsync(msg);
        }

        public async Task<bool> SendNotification(SendNotificationPayload request, string userId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(request.RecieverUserId);
            NotificationPayload notification = new()
            {
                Message = request.Message,
                Tag = request.Tag,
                TargetNamespace = request.TargetNamespace,
                Title = request.Title,
            };
            var result = await Publish(request.RecieverUserId, notification);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Message);
            return true;
        }

        #region Publish Helpers
        private void EnsureStreamExists()
        {
            try
            {
                if (_connection == null)
                {
                    _logger.LogWarning("Connection is null during EnsureStreamExistsAsync. Calling Connect()...");
                    Connect();
                }

                var jsm = _connection!.CreateJetStreamManagementContext();

                try
                {
                    jsm.GetStreamInfo("NotificationStream");
                    _logger.LogInformation("Stream 'NotificationStream' already exists.");
                }
                catch (NATSJetStreamException)
                {
                    _logger.LogInformation("Stream 'NotificationStream' not found. Creating it...");
                    var streamConfig = StreamConfiguration.Builder()
                        .WithName("NotificationStream")
                        .WithSubjects("notifications.*")
                        .Build();

                    jsm.AddStream(streamConfig);
                    _logger.LogInformation("Stream 'NotificationStream' created successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure stream existence.");
                throw;
            }
        }
        #endregion Publish Helpers

        #endregion Publish

        #region Subscribe
        public void Subscribe(string subject, string durableName, EventHandler<MsgHandlerEventArgs> messageHandler)
        {
            if (_jetStream == null) throw new InvalidOperationException("JetStream context is not initialized. Call Connect() first.");
            try
            {
                EnsureStreamExists(subject);

                var consumerConfig = ConsumerConfiguration.Builder()
                    .WithDurable(durableName)
                    .WithAckPolicy(AckPolicy.Explicit)
                    .WithFilterSubject(subject)
                    .Build();

                var pushOptions = PushSubscribeOptions.Builder()
                    .WithConfiguration(consumerConfig)
                    .Build();

                var subscription = _jetStream.PushSubscribeAsync(subject, messageHandler, false, pushOptions);
                subscription.Start();
                _logger.LogInformation("Subscribed to subject '{Subject}' with durable name '{Durable}'", subject, durableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to subject '{Subject}' with durable '{Durable}'", subject, durableName);
                throw;
            }
        }

        public async Task SubscribeAsync(string subject, Action<string> onMessageReceived)
        {
            if (_connection == null) throw new InvalidOperationException("Connection is not initialized. Call Connect() first.");

            _connection.SubscribeAsync(subject, (sender, args) =>
            {
                var text = Encoding.UTF8.GetString(args.Message.Data);
                onMessageReceived?.Invoke(text);
            });

            _logger.LogInformation("Subscribed asynchronously to subject '{Subject}'", subject);
            await Task.CompletedTask;
        }

        public IJetStreamPullSubscription CreatePullSubscription(string streamName, ConsumerConfiguration config)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();

            jsm.AddOrUpdateConsumer(streamName, config);

            var js = _connection.CreateJetStreamContext();

            var options = PullSubscribeOptions.Builder()
                                              .WithDurable("my-durable")
                                              .WithConfiguration(config)
                                              .Build();

            return js.PullSubscribe(config.FilterSubject, options);
        }

        public static IEnumerable<Msg> FetchBatch(JetStreamPullSubscription sub, int batchSize, int timeout)
        {
            return sub.Fetch(batchSize, timeout);
        }

        #region Subscribe Helpers
        private void EnsureStreamExists(string subject)
        {
            var jsm = _connection?.CreateJetStreamManagementContext() ?? throw new InvalidOperationException("Connection is not initialized. Call Connect() first.");
            try
            {
                jsm.GetStreamInfo("NotificationStream");
                _logger.LogInformation("Stream 'NotificationStream' already exists.");
            }
            catch (NATSJetStreamException)
            {
                _logger.LogInformation("Stream 'NotificationStream' not found. Creating it...");

                var streamConfig = StreamConfiguration.Builder()
                    .WithName("NotificationStream")
                    .WithSubjects(subject)
                    .Build();

                jsm.AddStream(streamConfig);
                _logger.LogInformation("Stream 'NotificationStream' created successfully.");
            }
        }
        #endregion Subscribe Helpers

        #endregion Subscribe

        #region DLQ Management
        public void EnsureDlqStreamExists(string dlqStreamName = "DLQStream")
        {
            var jsm = _connection!.CreateJetStreamManagementContext();
            try
            {
                jsm.GetStreamInfo(dlqStreamName);
                _logger.LogInformation("DLQ stream '{DLQStream}' already exists.", dlqStreamName);
            }
            catch (NATSJetStreamException)
            {
                _logger.LogInformation("Creating DLQ stream '{DLQStream}'...", dlqStreamName);

                var config = StreamConfiguration.Builder()
                    .WithName(dlqStreamName)
                    .WithSubjects("DLQ.>")
                    .WithStorageType(StorageType.File)
                    .WithRetentionPolicy(RetentionPolicy.WorkQueue)
                    .Build();

                jsm.AddStream(config);
                _logger.LogInformation("DLQ stream '{DLQStream}' created.", dlqStreamName);
            }
        }

        public static ConsumerConfiguration BuildDlqConsumerConfig(string durableName, string subject, string dlqSubject, int maxDeliver = 5)
        {
            var consumerConfig = ConsumerConfiguration.Builder()
                                                      .WithDurable(durableName)
                                                      .WithFilterSubject(subject)
                                                      .WithAckPolicy(AckPolicy.Explicit)
                                                      .WithMaxDeliver(5)
                                                      .WithDeliverPolicy(DeliverPolicy.All)
                                                      .Build();

            return consumerConfig;
        }

        public void SubscribeWithDlq(string subject, string durableName)
        {
            EnsureStreamExists(subject);
            EnsureDlqStreamExists();

            string dlqSubject = $"DLQ.{subject.Replace(".", "_")}";
            var config = BuildDlqConsumerConfig(durableName, subject, dlqSubject);

            CreateOrUpdateConsumer("NotificationStream", config);

            var pushOptions = PushSubscribeOptions.Builder().WithConfiguration(config).Build();

            _jetStream?.PushSubscribeAsync(subject, (sender, args) =>
            {
                var msg = args.Message;
                var data = Encoding.UTF8.GetString(msg.Data);
                try
                {
                    Console.WriteLine($"Received: {data}");
                    msg.Ack();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    msg.Nak();
                }
            }, true);

        }
        #endregion DLQ Management

        #region Mirror Management
        public StreamInfo CreateMirrorStream(string mirrorStreamName, string sourceStreamName, string? subjectFilter = null)
        {
            var jsm = _connection!.CreateJetStreamManagementContext();

            var mirrorBuilder = Mirror.Builder().WithName(sourceStreamName);

            if (!string.IsNullOrEmpty(subjectFilter)) mirrorBuilder.WithFilterSubject(subjectFilter);

            var config = StreamConfiguration.Builder()
                .WithName(mirrorStreamName)
                .WithMirror(mirrorBuilder.Build())
                .Build();

            return jsm.AddStream(config);
        }
        #endregion Mirror Management

        #region Store Access
        public IKeyValue CreateKeyValueBucket(KeyValueConfiguration config)
        {
            var kvm = _connection!.CreateKeyValueManagementContext();
            kvm.Create(config);
            return _connection.CreateKeyValueContext(config.BucketName);
        }
        #endregion Store Access
    }
}