using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using NBB.Messaging.Host;
using System.Reflection;

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
                            // .UseMiddleware<HandleExecutionErrorMiddleware>()
                            .UseDefaultResiliencyMiddleware()
                            .UseMediatRMiddleware());
                })
            );
  
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