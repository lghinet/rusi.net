using System;
using System.Collections.Generic;
using System.Diagnostics;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Proto.V1;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;
        private readonly Rusi.RusiClient _client;
        private static readonly ActivitySource ActivitySource = new ("MessageSender");
        private static readonly TextMapPropagator Propagator = new JaegerPropagator();

        public TestController(ILogger<TestController> logger, Rusi.RusiClient client)
        {
            _logger = logger;
            _client = client;
        }

        [HttpGet]
        public async Task<OrderCreated> Get()
        {
            var cmd = new OrderCreated(1);
            var publishRequest = new PublishRequest()
            {
                Data = ByteString.CopyFromUtf8(JsonSerializer.Serialize(cmd)),
                PubsubName = "natsstreaming-pubsub",
                Topic = "TS1858.dapr_test_topic",
                Metadata = { { "test-2", "test-2" } }
            };

            using var activity = ActivitySource.StartActivity("client publish operation", ActivityKind.Producer);

            ActivityContext contextToInject = default;
            if (activity != null)
            {
                contextToInject = activity.Context;
            }
            else if (Activity.Current != null)
            {
                contextToInject = Activity.Current.Context;
            }

            // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
            Propagator.Inject(new PropagationContext(contextToInject, Baggage.Current), publishRequest.Metadata,
                InjectTraceContextIntoBasicProperties);

            /*
            using var scope = _tracer.BuildSpan("client publish operation")
                .WithTag(OpenTracing.Tag.Tags.Component, "client publisher")
                .WithTag(OpenTracing.Tag.Tags.SpanKind, OpenTracing.Tag.Tags.SpanKindProducer)
                .StartActive(true);

            if (_tracer.ActiveSpan != null)
            {
                _tracer.Inject(_tracer.ActiveSpan.Context, BuiltinFormats.TextMap,
                    new TextMapInjectAdapter(publishRequest.Metadata));
            }
            */
            await _client.PublishAsync(publishRequest);

            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":2}") });
            await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":3}") });
            //await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":4}") });
            //await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":5}") });
            //await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":6}") });
            //await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":7}") });
            //await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":8}") });
            //await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":9}") });
            //await _client.PublishAsync(new PublishRequest(publishRequest) { Data = ByteString.CopyFromUtf8("{\"OrderId\":10}") });

            return cmd;
        }

        public record OrderCreated(int OrderId);

        private void InjectTraceContextIntoBasicProperties(MapField<string,string> props, string key, string value)
        {
            try
            {
                if (props == null)
                {
                    props = new MapField<string, string>();
                }

                props[key] = value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inject trace context.");
            }
        }
    }
}