// Configure application builder and services
using OrderNotificationsService.Extensions;

// Create the web application builder and configure services
var builder = WebApplication.CreateBuilder(args)
    .AddApplicationBuilderConfiguration();

// Build the application and configure the middleware pipeline
var app = builder.Build()
    .UseApplicationPipeline();

// Run the application
await app.RunAsync();