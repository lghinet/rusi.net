// Copyright (c) TotalSoft.
// This source code is licensed under the MIT license.

using NBB.Messaging.InProcessMessaging.Internal;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Rusi.Net
{
    public class Storage : IStorage
    {
        private readonly ConcurrentDictionary<string, (ConcurrentQueue<byte[]> Queue, Func<byte[], Task> Handler)> _queues = new();
        //private readonly HashSet<string> _subscriptions = new();

        public void Enqueue(byte[] msg, string topic)
        {
            var (q, _) = _queues.GetOrAdd(topic, (new(), _ => Task.CompletedTask));
            q.Enqueue(msg);
            //lock (_subscriptions)
            //{
            //    if (_subscriptions.Contains(topic))
            //    {
            //        AwakeBroker(topic);
            //    }
            //}

        }

        public async Task AddSubscription(string topic, Func<byte[], Task> handler,
            CancellationToken cancellationToken = default)
        {
            //lock (_subscriptions)
            //{
            //    if (_subscriptions.Contains(topic))
            //        throw new Exception("Already subscribed to topic " + topic);

            //    _subscriptions.Add(topic);
            //}

            //await Task.Yield();
            var _ = Task.Run(async () => { await StartBroker(topic, handler, cancellationToken); }, cancellationToken);
            return;
        }

        private async Task StartBroker(string topic, Func<byte[], Task> handler,
            CancellationToken cancellationToken = default)
        {
            //var ev = _brokersAutoReset.GetOrAdd(topic, new AutoResetEvent(false));
            var spin = new SpinWait();
            var (q, h) = _queues.AddOrUpdate(topic, topic => (new(), handler), (topic, value) => (value.Queue, msg => Task.WhenAll(value.Handler(msg), handler(msg))));
            await Task.Yield();
            while (!cancellationToken.IsCancellationRequested)
            {
                //if (q.IsEmpty)
                //    ev.WaitOne();
                //spin.SpinOnce();
                if (q.TryDequeue(out var msg))
                {
                    await handler(msg);
                }
                await Task.Yield();
            }
        }

        //private void AwakeBroker(string topic)
        //{
        //    var ev = _brokersAutoReset.GetOrAdd(topic, new AutoResetEvent(false));
        //    ev?.Set();
        //}
    }
}