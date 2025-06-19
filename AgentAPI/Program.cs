using CommonServices.Services;
using CommonServices.Agents;
using OpenTelemetry.Resources;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["ApplicationInsights:ConnectionString"] 
    ?? throw new InvalidOperationException("Application Insights connection string not configured");

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService("TelemetryApplicationInsightsQuickstart");

AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

using var traceProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource("Microsoft.SemanticKernel*")
    .AddAzureMonitorTraceExporter(options => options.ConnectionString = connectionString)
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter("Microsoft.SemanticKernel*")
    .AddAzureMonitorMetricExporter(options => options.ConnectionString = connectionString)
    .Build();

// Configure OpenTelemetry logging with ASP.NET Core's built-in logging
builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(resourceBuilder);
    options.AddAzureMonitorLogExporter(options => options.ConnectionString = connectionString);
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure SignalR with proper settings
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

// Register TutorAgent as a singleton
builder.Services.AddSingleton<TutorAgent>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var modelId = configuration["OpenAI:ModelId"] ?? "gpt-4o-mini";
    var openAiApiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured");
    var braveApiKey = configuration["Brave:ApiKey"] ?? throw new InvalidOperationException("Brave API key not configured");
    var mem0ApiKey = configuration["Mem0:ApiKey"] ?? throw new InvalidOperationException("Mem0 API key not configured");

    var agent = new TutorAgent(modelId, openAiApiKey, braveApiKey, mem0ApiKey);
    // Initialize the agent - we'll do this async in the hub
    return agent;
});

builder.Services.AddSingleton<AzureAgent>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var foundryEndpoint = configuration["AIFoundry:Endpoint"] ?? throw new InvalidOperationException("Azure AI Foundry endpoint not configured");
    var modelId = configuration["OpenAI:ModelId"] ?? "gpt-4o-mini";

    var azureAgent = new AzureAgent(modelId, foundryEndpoint);

    return azureAgent;
});

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
                // Frontend origins
                "http://localhost:3000", "https://localhost:3000", 
                "http://127.0.0.1:3000", "https://127.0.0.1:3000",
                "http://localhost:8080", "https://localhost:8080",
                "http://127.0.0.1:8080", "https://127.0.0.1:8080",
                // API origins (for self-referencing)
                "http://localhost:5038", "https://localhost:7095",
                "http://127.0.0.1:5038", "https://127.0.0.1:7095")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseHttpsRedirection();
app.UseRouting();

// CORS must come after UseRouting but before endpoints
app.UseCors("AllowAll");

// Map controllers and SignalR hub with CORS
app.MapControllers();
app.MapHub<ChatHub>("/chathub").RequireCors("AllowAll");
app.Run();
