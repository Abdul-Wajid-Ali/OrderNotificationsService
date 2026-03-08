using Microsoft.EntityFrameworkCore;
using OrderNotificationsService.Features.Notifications.GetUserNotifications;
using OrderNotificationsService.Features.Notifications.ProcessOrderStatusChanged;
using OrderNotificationsService.Features.Orders.CreateOrder;
using OrderNotificationsService.Features.Orders.UpdateOrderStatus;
using OrderNotificationsService.Infrastructure.BackgroundServices;
using OrderNotificationsService.Infrastructure.Persistence;

// Configure application builder and services
var builder = WebApplication.CreateBuilder(args);

// Register MVC controllers
builder.Services.AddControllers();

// Enable OpenAPI/Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure EF Core with SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Register application handlers
builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddScoped<UpdateOrderStatusHandler>();
builder.Services.AddScoped<OrderStatusChangedHandler>();
builder.Services.AddScoped<GetUserNotificationsHandler>();

// Background worker that processes outbox events and creates notifications
builder.Services.AddHostedService<OutboxProcessor>();

// Build the application pipeline
var app = builder.Build();

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

// Start the application
await app.RunAsync();