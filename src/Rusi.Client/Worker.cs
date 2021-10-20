using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Propagation;
using Proto.V1;

namespace WebApplication1
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly Rusi.RusiClient _client;
        private readonly ITracer _tracer;

        public Worker(ILogger<Worker> logger, Rusi.RusiClient client, ITracer tracer)
        {
            _logger = logger;
            _client = client;
            _tracer = tracer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true)
            {
                try
                {
                    // will never be sent to the client.
                    var channel = Channel.CreateUnbounded<AckRequest>(new UnboundedChannelOptions()
                    {
                        SingleReader = true,
                        SingleWriter = false,
                    });

                    using var subscription = _client.Subscribe();
                    await subscription.RequestStream.WriteAsync(
                        new SubscribeRequest()
                        {
                            SubscriptionRequest = new SubscriptionRequest()
                            {
                                PubsubName = "natsstreaming-pubsub",
                                Topic = "TS1858.dapr_test_topic",
                                //Options = new SubscriptionOptions(){DeliverNewMessagesOnly = false}
                            }
                        });

                    _ = Task.Run(async () =>
                    {
                        while (await channel.Reader.WaitToReadAsync(stoppingToken))
                        {
                            if (channel.Reader.TryRead(out var ack))
                            {
                                await subscription?.RequestStream.WriteAsync(new SubscribeRequest()
                                {
                                    AckRequest = ack
                                });
                            }
                        }
                    }, stoppingToken);

                    await foreach (var ss in subscription.ResponseStream.ReadAllAsync(stoppingToken))
                    {
                        using var scope = CreateSpan(ss.Metadata);

                        _logger.LogInformation(ss.Data.ToStringUtf8());

                        // Simulate work
                        _ = Task.Delay(TimeSpan.FromSeconds(3), stoppingToken)
                            .ContinueWith(task =>
                            {
                                var ack = new AckRequest()
                                {
                                    MessageId = ss.Id,
                                };

                             
                                //TODO check if subscription was disposed 
                                if (!channel.Writer.TryWrite(ack))
                                {
                                    throw new InvalidOperationException("Unable to queue ack.");
                                }

                            }, stoppingToken);
                    }

                }
                catch (RpcException rpc)
                {
                    //reconnect stream
                    Console.WriteLine(rpc);
                }

                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        private IScope CreateSpan(IDictionary<string, string> metadata)
        {
            var extractedSpanContext =
                _tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(metadata));

            return _tracer.BuildSpan("client receive operation")
                .AddReference(References.FollowsFrom, extractedSpanContext)
                .WithTag(OpenTracing.Tag.Tags.Component, "client receive")
                .WithTag(OpenTracing.Tag.Tags.SpanKind, OpenTracing.Tag.Tags.SpanKindConsumer)
                .StartActive(true);
        }
    }
}