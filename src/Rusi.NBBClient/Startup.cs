using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using NBB.Messaging.Host;
using OpenTelemetry.Resources;
using OpenTelemetry;
using System;
using System.Reflection;
using OpenTelemetry.Extensions.Propagators;
using OpenTelemetry.Trace;
using NBB.Messaging.OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;

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

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetEntryAssembly()));
            services
                .AddMessageBus()
                .AddRusiTransport(Configuration)
                .UseTopicResolutionBackwardCompatibility(Configuration);

            services.AddMessagingHost(Configuration, hostBuilder => hostBuilder
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
                            .UseDefaultResiliencyMiddleware()
                            .UseMediatRMiddleware());
                })
            );

            AddOpenTelemetry(services, Configuration);

        }

        public void AddOpenTelemetry(IServiceCollection services, IConfiguration configuration)
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();

            void configureResource(ResourceBuilder r) =>
                r.AddService(assembly.Name, serviceVersion: assembly.Version?.ToString(), serviceInstanceId: Environment.MachineName);

            if (configuration.GetValue<bool>("OpenTelemetry:TracingEnabled"))
            {
                Sdk.SetDefaultTextMapPropagator(new JaegerPropagator());

                services.AddOpenTelemetry().WithTracing(builder =>
                    builder
                        .ConfigureResource(configureResource)
                        .SetSampler(new AlwaysOnSampler())
                        .AddMessageBusInstrumentation()
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation(options =>
                        {
                            options.RecordException = true;
                            options.FilterHttpRequestMessage = msg =>
                            {
                                var fromRusi = msg?.RequestUri?.PathAndQuery?.StartsWith("/rusi.proto.runtime") ?? false;
                                return !fromRusi;
                            };
                        })
                        .AddOtlpExporter());

                services.Configure<OtlpExporterOptions>(configuration.GetSection("OpenTelemetry:Otlp"));
            }
 
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