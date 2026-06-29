using backend.Endpoints;
using backend.Extensions;
using backend.Middleware;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register application services, configuration options, HTTP client, and logging
builder.Services.AddApplicationServices(builder.Configuration);

// Configure and register the LLM ChatClient
builder.Services.AddLlmChatClient(builder.Configuration);

// Register AI Agents and their factories
builder.Services.AddAgentWorkflow();

builder.Services.AddAGUI();

var app = builder.Build();

// Enable Global Exception Handling and request logging middlewares
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors(CorsPolicyNames.AllowReactApp);

// Map REST API Endpoints
app.MapTimesheetEndpoints();
app.MapLeaveEndpoints();

// Expose the AG-UI Protocol Endpoint using the workflow agent registered in DI
app.MapAGUI("/", app.Services.GetRequiredService<AIAgent>());

app.Logger.LogInformation("ASP.NET Core Backend is starting on Port 5116...");
app.Run("http://localhost:5116");

public partial class Program { }
