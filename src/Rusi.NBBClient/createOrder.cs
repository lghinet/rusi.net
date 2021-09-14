using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Rusi.NBBClient
{

    public record CreateOrder(int OrderId, string[] Summaries) : IRequest<Unit>;

    public class CreateOrderHandler : IRequestHandler<CreateOrder>
    {
        public Task<Unit> Handle(CreateOrder request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }
       
    }
}
