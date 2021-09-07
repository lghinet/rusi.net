using FluentAssertions;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.V1;
using Xunit;

namespace Rusi.NetClientTests
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            using var channel = GrpcChannel.ForAddress("http://localhost:50003");
            var client = new Proto.V1.Rusi.RusiClient(channel);
            var cts = new CancellationTokenSource();


            var subscriptions = new Dictionary<int, AsyncServerStreamingCall<ReceivedMessage>>();
            for (int i = 0; i < 100; i++)
            {
                var subscription = client.Subscribe(new SubscribeRequest()
                {
                    PubsubName = "natsstreaming-pubsub",
                    Topic = "test_topic_" + i
                });
                subscriptions.Add(i, subscription);
            }

            var tasks = subscriptions
                .Select(async x =>
                {
                    var result = new List<string>();
                    var nr = 0;
                    await foreach (var ss in x.Value.ResponseStream.ReadAllAsync(cts.Token))
                    {
                        nr++;
                        result.Add(ss.Data.ToStringUtf8());
                        if (nr == 3)
                            break;
                    }

                    result.Count.Should().Be(3);

                        //$"test_topic_{x.Key}_result_1",
                        //$"test_topic_{x.Key}_result_2"
                        //, $"test_topic_{x.Key}_result_3");

                });

            await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

            for (int i = 0; i < 100; i++)
            {
                await client.PublishAsync(new PublishRequest()
                {
                    Data = ByteString.CopyFromUtf8("test_topic_" + i + "_result_1"),
                    PubsubName = "natsstreaming-pubsub",
                    Topic = "test_topic_" + i
                });
                await client.PublishAsync(new PublishRequest()
                {
                    Data = ByteString.CopyFromUtf8("test_topic_" + i + "_result_2"),
                    PubsubName = "natsstreaming-pubsub",
                    Topic = "test_topic_" + i
                });
                await client.PublishAsync(new PublishRequest()
                {
                    Data = ByteString.CopyFromUtf8("test_topic_" + i + "_result_3"),
                    PubsubName = "natsstreaming-pubsub",
                    Topic = "test_topic_" + i
                });
            }

            //await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
            //cts.Cancel();

            await Task.WhenAll(tasks);
        }
    }
}