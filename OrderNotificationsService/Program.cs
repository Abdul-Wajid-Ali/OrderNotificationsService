using Microsoft.EntityFrameworkCore;
using OrderNotificationsService.Features.Orders.CreateOrder;
using OrderNotificationsService.Features.Orders.UpdateOrderStatus;
using OrderNotificationsService.Infrastructure.Persistence;

// Initializes the web application builder to configure services and configuration sources.
var builder = WebApplication.CreateBuilder(args);

// Registers controller services into the dependency injection container for API functionality.
builder.Services.AddControllers();

// Configures metadata generation for Swagger/OpenAPI documentation.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registers the Entity Framework DbContext with SQL Server using the defined connection string.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

//Registers the command handlers for creating orders and updating order statuses in the dependency injection container.
builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddScoped<UpdateOrderStatusHandler>();

// Finalizes the service configurations and builds the application instance.
var app = builder.Build();

// Enables the Swagger UI middleware only when the application is running in development mode.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Redirects all HTTP requests to HTTPS for secure communication.
app.UseHttpsRedirection();

// Adds authorization middleware to ensure user permissions are validated.
app.UseAuthorization();

// Maps incoming HTTP requests to the appropriate controller action methods.
app.MapControllers();

// Starts the application and begins listening for incoming requests asynchronously.
await app.RunAsync();