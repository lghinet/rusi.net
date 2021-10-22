using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System;
using OpenTelemetry.Resources;
using Proto.V1;

namespace WebApplication1
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


            services.AddHostedService<Worker>();
            services.AddGrpcClient<Rusi.RusiClient>(o =>
            {
                o.Address = new Uri("http://localhost:50003");
                o.ChannelOptionsActions.Add(options =>
                {
                    options.MaxRetryAttempts = 200;
                    options.ServiceConfig = new ServiceConfig
                    {
                        MethodConfigs =
                        {
                            new MethodConfig()
                            {
                                Names = { MethodName.Default },
                                RetryPolicy = new RetryPolicy()
                                {
                                    MaxAttempts = 200,
                                    InitialBackoff = TimeSpan.FromSeconds(10),
                                    MaxBackoff = TimeSpan.FromMinutes(30),
                                    BackoffMultiplier = 1.5,
                                    RetryableStatusCodes = { Grpc.Core.StatusCode.Unavailable, Grpc.Core.StatusCode.Aborted }
                                }
                            }
                        }
                    };
                });
            });

            services.AddOpenTelemetryTracing(builder => builder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Rusi.Client"))
                //.AddGrpcClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddSource("MessageSender")
                .AddSource("MessageReceiver")
                //.AddZipkinExporter(b =>
                //{
                //    b.Endpoint = new Uri($"http://kube-worker1.totalsoft.local31578/api/v2/spans");
                //}));
                .AddJaegerExporter(jaegerOptions =>
                {
                    //no collector support http://kube-worker1.totalsoft.local:31034/api/traces
                    jaegerOptions.AgentHost = "kube-worker1.totalsoft.local";
                    jaegerOptions.AgentPort = 31700;
                }));


            services.AddOpenTelemetryMetrics(builder => { builder.AddPrometheusExporter(); });
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

            app.UseOpenTelemetryPrometheusScrapingEndpoint();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}