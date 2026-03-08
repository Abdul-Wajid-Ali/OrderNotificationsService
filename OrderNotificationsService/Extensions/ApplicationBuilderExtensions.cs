using Microsoft.EntityFrameworkCore;
using OrderNotificationsService.Features.Notifications.GetUserNotifications;
using OrderNotificationsService.Features.Notifications.ProcessOrderStatusChanged;
using OrderNotificationsService.Features.Orders.CreateOrder;
using OrderNotificationsService.Features.Orders.UpdateOrderStatus;
using OrderNotificationsService.Infrastructure.BackgroundServices;
using OrderNotificationsService.Infrastructure.Messaging;
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

            // Configure RabbitMQ options from configuration
            builder.Services.Configure<RabbitMqOptions>(
                builder.Configuration.GetSection(RabbitMqOptions.SectionName));

            // Register application handlers
            builder.Services.AddScoped<CreateOrderHandler>();
            builder.Services.AddScoped<UpdateOrderStatusHandler>();
            builder.Services.AddScoped<OrderStatusChangedHandler>();
            builder.Services.AddScoped<GetUserNotificationsHandler>();

            // Background worker that processes outbox events and creates notifications
            builder.Services.AddHostedService<OutboxProcessor>();

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