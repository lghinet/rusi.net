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

namespace Rusi.NBBClient.Controllers
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
        private readonly ITracer _tracer;

        public TestController(ILogger<TestController> logger, IMessageBusPublisher busPublisher, ITracer tracer)
        {
            _logger = logger;
            _busPublisher = busPublisher;
            _tracer = tracer;
        }

        [HttpGet]
        public async Task<string[]> Get()
        {
            var cmd = new CreateOrder(1232, Summaries);


            //using var scope = _tracer.BuildSpan("client publish operation")
            //    .WithTag(OpenTracing.Tag.Tags.Component, "client publisher")
            //    .WithTag(OpenTracing.Tag.Tags.SpanKind, OpenTracing.Tag.Tags.SpanKindProducer)
            //    .StartActive(true);

            //if (_tracer.ActiveSpan != null)
            //{
            //    _tracer.Inject(_tracer.ActiveSpan.Context, BuiltinFormats.TextMap,
            //        new TextMapInjectAdapter(publishRequest.Metadata));
            //}


            await _busPublisher.PublishAsync(cmd, HttpContext.RequestAborted);

            return Summaries;
        }

    }
}