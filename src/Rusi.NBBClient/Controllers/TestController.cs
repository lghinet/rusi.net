using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBB.Messaging.Abstractions;
using System.Threading.Tasks;

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

        public TestController(ILogger<TestController> logger, IMessageBusPublisher busPublisher)
        {
            _logger = logger;
            _busPublisher = busPublisher;
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