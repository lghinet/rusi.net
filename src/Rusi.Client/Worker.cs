using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Proto.V1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace WebApplication1
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly Rusi.RusiClient _client;

        private static readonly ActivitySource ActivitySource = new ("MessageReceiver");

        public Worker(ILogger<Worker> logger, Rusi.RusiClient client)
        {
            _logger = logger;
            _client = client;
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
                        using var activity = StartActivity(ss.Metadata);

                        activity?.SetTag("Message received", ss.Data);
                        activity?.SetTag("Message metadata", ss.Metadata);
                        activity?.SetTag("Message id", ss.Id);

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

        private Activity StartActivity(IDictionary<string, string> metadata)
        {
            // Extract the PropagationContext of the upstream parent from the message headers.
            var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, metadata, ExtractTraceContextFromBasicProperties);
            Baggage.Current = parentContext.Baggage;

            return ActivitySource.StartActivity("client receive operation", ActivityKind.Consumer, parentContext.ActivityContext);
        }

        private IEnumerable<string> ExtractTraceContextFromBasicProperties(IDictionary<string, string> props, string key)
        {
            try
            {
                if (props.TryGetValue(key, out var value))
                {
                    return new[] { value };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract trace context");
            }

            return Enumerable.Empty<string>();
        }
    }
}