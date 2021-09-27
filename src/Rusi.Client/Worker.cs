using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            using var subscription = _client.Subscribe(new SubscribeRequest()
            {
                PubsubName = "natsstreaming-pubsub",
                Topic = "TS1858.dapr_test_topic"
            });

            //var gg = subscription.GetStatus();
            try
            {
                await foreach (var ss in subscription.ResponseStream.ReadAllAsync(stoppingToken))
                {
                    using var scope = CreateSpan(ss.Metadata);

                    _logger.LogInformation(ss.Data.ToStringUtf8());

                    // Simulate work
                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                }

            }
            catch (RpcException rpc) when (rpc.StatusCode == StatusCode.Unavailable)
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