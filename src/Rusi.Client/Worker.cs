using System;
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

            await foreach (var ss in subscription.ResponseStream.ReadAllAsync(stoppingToken))
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                var metadata = await subscription.ResponseHeadersAsync;

                var extractedSpanContext = _tracer.Extract(BuiltinFormats.TextMap,
                    new TextMapExtractAdapter(ss.Metadata));

                using var scope = _tracer.BuildSpan("client publish operation")
                    .AddReference(References.FollowsFrom, extractedSpanContext)
                    .WithTag(OpenTracing.Tag.Tags.Component, "client publisher")
                    .WithTag(OpenTracing.Tag.Tags.SpanKind, OpenTracing.Tag.Tags.SpanKindProducer)
                    .StartActive(true);

                _logger.LogInformation(ss.Data.ToStringUtf8());
            }

        }
    }
}