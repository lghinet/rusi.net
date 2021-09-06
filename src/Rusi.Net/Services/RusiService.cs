using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using NBB.Messaging.Abstractions;
using Proto.V1;
using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBB.Core.Abstractions;
using NBB.Core.Pipeline;

namespace Rusi.Net.Services
{
    public class RusiService : Proto.V1.Rusi.RusiBase
    {
        private readonly ILogger<RusiService> _logger;
        private readonly IMessageBus _messageBus;
        private readonly PipelineDelegate<MessagingContext> _pipeline;
        private readonly IServiceScopeFactory _scopeFactory;

        public RusiService(ILogger<RusiService> logger, IMessageBus messageBus,
            PipelineDelegate<MessagingContext> pipeline, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _messageBus = messageBus;
            _pipeline = pipeline;
            _scopeFactory = scopeFactory;
        }

        public override async Task<Empty> Publish(PublishRequest request, ServerCallContext context)
        {
            _logger.LogDebug("Publishing message");

            var payload = new PayloadWrapper() { Data = request.Data.ToByteArray() };
            await _messageBus.PublishAsync(payload, new MessagingPublisherOptions()
            {
                TopicName = request.Topic,
                EnvelopeCustomizer = envelope =>
                {
                    foreach (var header in request.Metadata)
                    {
                        envelope.Headers.Add(header.Key, header.Value);
                    }
                }
            }, context.CancellationToken);

            _logger.LogDebug("Published message");
            return new Empty();
        }

        public override async Task Subscribe(SubscribeRequest request,
            IServerStreamWriter<ReceivedMessage> responseStream,
            ServerCallContext context)
        {

            _logger.LogDebug("Subscribing to topic " + request.Topic);

            using var sub = await _messageBus.SubscribeAsync<PayloadWrapper>(async envelope =>
                {
                    _logger.LogDebug("Received message");

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        await _pipeline.Invoke(new MessagingContext(envelope, request.Topic, scope.ServiceProvider),
                            context.CancellationToken);
                    }

                    var res = new ReceivedMessage()
                    {
                        Data = ByteString.CopyFrom(envelope.Payload.Data),
                    };
                    foreach (var header in envelope.Headers)
                    {
                        res.Metadata.Add(header.Key, header.Value);
                    }

                    await responseStream.WriteAsync(res);

                },
                new MessagingSubscriberOptions()
                {
                    TopicName = request.Topic,

                }, context.CancellationToken);

            await context.CancellationToken.WhenCanceled();
        }

        class PayloadWrapper
        {
            public byte[] Data { init; get; }
        }
    }
}