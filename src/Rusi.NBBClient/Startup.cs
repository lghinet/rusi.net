using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders.Thrift;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OpenTracing;
using OpenTracing.Noop;
using OpenTracing.Util;
using Proto.V1;
using System;
using System.Reflection;
using MediatR;
using NBB.Messaging.Host;
using NBB.Messaging.OpenTracing.Subscriber;

namespace Rusi.NBBClient
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebApplication1", Version = "v1" });
            });

            services.AddMediatR(Assembly.GetEntryAssembly());
            services
                .AddRusiMessageBus(Configuration);

            services.AddMessagingHost(hostBuilder => hostBuilder
                .Configure(hostConfig =>
                {
                    hostConfig
                        .AddSubscriberServices(subscriberBuilder => subscriberBuilder
                            .FromMediatRHandledCommands().AddAllClasses()
                            .FromMediatRHandledEvents().AddAllClasses())
                        .WithDefaultOptions()
                        .UsePipeline(pipelineBuilder => pipelineBuilder
                            .UseCorrelationMiddleware()
                            .UseExceptionHandlingMiddleware()
                            .UseMiddleware<OpenTracingMiddleware>()
                            // .UseMiddleware<HandleExecutionErrorMiddleware>()
                            .UseDefaultResiliencyMiddleware()
                            .UseMediatRMiddleware());
                })
            );

         
            services.AddOpenTracingCoreServices(builder => builder
                //.AddAspNetCore(x=>x.Hosting.)
                //.AddGenericDiagnostics(x => x.IgnoredListenerNames.Add("Grpc.Net.Client"))
                //.AddHttpHandler()
                .AddLoggerProvider()
            );


            services.AddSingleton<ITracer>(serviceProvider =>
            {
                if (!Configuration.GetValue<bool>("OpenTracing:Jeager:IsEnabled"))
                {
                    return NoopTracerFactory.Create();
                }



                string serviceName = Assembly.GetEntryAssembly().GetName().Name;

                ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                ITracer tracer = new Tracer.Builder(serviceName)
                    .WithLoggerFactory(loggerFactory)
                    .WithSampler(new ConstSampler(true))
                    .WithReporter(new RemoteReporter.Builder()
                        .WithSender(new HttpSender("http://kube-worker1.totalsoft.local:31034/api/traces"))
                        .Build())
                    .Build();



                GlobalTracer.Register(tracer);
                return tracer;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApplication1 v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}