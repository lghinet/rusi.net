using System.Text;
using System.Threading.Tasks;
using Jaeger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBB.Core.Pipeline;
using NBB.Messaging.Abstractions;
using NBB.Messaging.InProcessMessaging.Internal;
using Rusi.Net.Services;

namespace Rusi.Net
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc();

            services
                .AddMessageBus()
                //.AddInProcessTransport();
                .AddNatsTransport(Configuration);

            //services.AddSingleton<IStorage, Storage>();

            var pb = new PipelineBuilder<MessagingContext>();
            pb.Use((context, token, next) =>
            {
                var lf = context.Services.GetRequiredService<ILoggerFactory>();
                var logger = lf.CreateLogger("uppercase middleware");
                logger.LogInformation("uppercase middleware");
                context.MessagingEnvelope.SetHeader("uppercase", "uppercase");
                return next();
            });

            services.AddSingleton(pb.Pipeline);

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

          
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<RusiService>();

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}
