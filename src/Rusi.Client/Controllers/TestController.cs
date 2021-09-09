using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBB.Messaging.Abstractions;
using Newtonsoft.Json;
using OpenTracing;
using OpenTracing.Propagation;
using Proto.V1;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf.Collections;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<TestController> _logger;
        private readonly IMessageBusPublisher _busPublisher;
        private readonly Rusi.RusiClient _client;
        private readonly ITracer _tracer;

        public TestController(ILogger<TestController> logger, IMessageBusPublisher busPublisher,
            Rusi.RusiClient client, ITracer tracer)
        {
            _logger = logger;
            _busPublisher = busPublisher;
            _client = client;
            _tracer = tracer;
        }

        [HttpGet]
        public async Task<string[]> Get()
        {
            var cmd = new OrderCreated(1232, Summaries);
            var publishRequest = new PublishRequest()
            {
                Data = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(cmd)),
                PubsubName = "natsstreaming-pubsub",
                Topic = "TS1858.dapr_test_topic",
                Metadata = { { "test-2", "test-2" } }
            };

            using var scope = _tracer.BuildSpan("client publish operation")
                .WithTag(OpenTracing.Tag.Tags.Component, "client publisher")
                .WithTag(OpenTracing.Tag.Tags.SpanKind, OpenTracing.Tag.Tags.SpanKindProducer)
                .StartActive(true);

            if (_tracer.ActiveSpan != null)
            {
                _tracer.Inject(_tracer.ActiveSpan.Context, BuiltinFormats.TextMap,
                    new TextMapInjectAdapter(publishRequest.Metadata));
            }

            await _client.PublishAsync(publishRequest);



            //await _busPublisher.PublishAsync(new OrderCreated(1232, Summaries), new MessagingPublisherOptions()
            //{
            //    TopicName = "dapr_test_topic"
            //}, HttpContext.RequestAborted);

            return Summaries;
        }

        public record OrderCreated(int OrderId, string[] Summaries);
    }
}