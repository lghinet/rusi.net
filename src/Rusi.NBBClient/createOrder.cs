using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Rusi.NBBClient
{

    public record CreateOrder(int OrderId, string[] Summaries) : IRequest;

    public class CreateOrderHandler : IRequestHandler<CreateOrder>
    {
        private readonly ILogger<CreateOrderHandler> _logger;

        public CreateOrderHandler(ILogger<CreateOrderHandler> logger)
        {
            _logger = logger;
        }

        public async Task<Unit> Handle(CreateOrder request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            _logger.LogInformation("new order request" + request.OrderId);
            return Unit.Value;
        }
    }
}