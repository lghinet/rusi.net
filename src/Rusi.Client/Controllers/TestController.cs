using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenTracing;
using OpenTracing.Propagation;
using Proto.V1;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;
        private readonly Rusi.RusiClient _client;
        private readonly ITracer _tracer;

        public TestController(ILogger<TestController> logger, Rusi.RusiClient client, ITracer tracer)
        {
            _logger = logger;
            _client = client;
            _tracer = tracer;
        }

        [HttpGet]
        public async Task<OrderCreated> Get()
        {
            var cmd = new OrderCreated(1);
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

            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":2}") });
            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":3}") });
            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":4}") });
            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":5}") });
            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":6}") });
            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":7}") });
            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":8}") });
            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":9}") });
            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":10}") });


            //await _busPublisher.PublishAsync(new OrderCreated(1232, Summaries), new MessagingPublisherOptions()
            //{
            //    TopicName = "dapr_test_topic"
            //}, HttpContext.RequestAborted);

            return cmd;
        }

        public record OrderCreated(int OrderId);
    }
}