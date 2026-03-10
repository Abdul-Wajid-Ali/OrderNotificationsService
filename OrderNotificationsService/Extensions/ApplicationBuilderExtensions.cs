using Microsoft.EntityFrameworkCore;
using OrderNotificationsService.Features.Notifications.GetUserNotifications;
using OrderNotificationsService.Features.Notifications.ProcessOrderStatusChanged;
using OrderNotificationsService.Features.Orders.CreateOrder;
using OrderNotificationsService.Features.Orders.UpdateOrderStatus;
using OrderNotificationsService.Infrastructure.BackgroundServices;
using OrderNotificationsService.Infrastructure.Correlation;
using OrderNotificationsService.Infrastructure.Messaging;
using OrderNotificationsService.Infrastructure.Monitoring;
using OrderNotificationsService.Infrastructure.Notifications;
using OrderNotificationsService.Infrastructure.Persistence;
using System.Text.Json.Serialization;

namespace OrderNotificationsService.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static WebApplicationBuilder AddApplicationBuilderConfiguration(this WebApplicationBuilder builder)
        {
            // Register MVC controllers
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                //Configure JSON serialization to handle enums as strings
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // Enable OpenAPI/Swagger for API documentation
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Configure EF Core with SQL Server
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

            // Configuration binding (RabbitMQ + monitoring settings)
            builder.Services.Configure<RabbitMqOptions>(
                builder.Configuration.GetSection(RabbitMqOptions.SectionName));

            builder.Services.Configure<MonitoringOptions>(
               builder.Configuration.GetSection(MonitoringOptions.SectionName));

            // Application handlers (business use cases)
            builder.Services.AddScoped<CreateOrderHandler>();
            builder.Services.AddScoped<UpdateOrderStatusHandler>();
            builder.Services.AddScoped<OrderStatusChangedHandler>();
            builder.Services.AddScoped<GetUserNotificationsHandler>();

            // Notification infrastructure
            builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();

            // Messaging & observability infrastructure
            builder.Services.AddSingleton<ICorrelationContextAccessor, CorrelationContextAccessor>();
            builder.Services.AddSingleton<NotificationPipelineMetrics>();
            builder.Services.AddSingleton<RabbitMqPublisher>();

            // Background workers
            builder.Services.AddHostedService<OutboxProcessor>();
            builder.Services.AddHostedService<OrderStatusConsumer>();

            return builder;
        }

        public static WebApplication UseApplicationPipeline(this WebApplication app)
        {
            // Enable Swagger UI in development
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Correlation ID middleware for request tracing
            app.UseMiddleware<CorrelationIdMiddleware>();

            // Enforce HTTPS
            app.UseHttpsRedirection();

            // Enable authorization middleware
            app.UseAuthorization();

            // Map controller routes
            app.MapControllers();

            return app;
        }
    }
}