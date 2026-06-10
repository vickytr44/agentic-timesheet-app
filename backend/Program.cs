using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using TimesheetCopilotApp.Backend;
using TimesheetCopilotApp.Backend.Models;
using TimesheetCopilotApp.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 2. Register standard services
builder.Services.AddSingleton<TimesheetService>();
builder.Services.AddSingleton<HandbookService>();
builder.Services.AddHttpClient().AddLogging();
builder.Services.ConfigureHttpJsonOptions(options => 
{
    options.SerializerOptions.TypeInfoResolverChain.Add(TimesheetSerializerContext.Default);
});
builder.Services.AddAGUI();

// 3. Configure IChatClient using Groq / OpenAI
var apiKey = builder.Configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var modelId = builder.Configuration["OpenAI:ModelId"] ?? "llama-3.3-70b-versatile";
var endpoint = builder.Configuration["OpenAI:Endpoint"] ?? "https://api.groq.com/openai/v1/";

IChatClient chatClient;

if (!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_GROQ_API_KEY")
{
    Console.WriteLine($"--> Configuring Groq Chat Client with Endpoint: {endpoint} and Model: {modelId}...");
    var clientOptions = new OpenAI.OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint)
    };
    var openAIClient = new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), clientOptions);
    chatClient = openAIClient.GetChatClient(modelId).AsIChatClient();
}
else
{
    throw new InvalidOperationException("API key for Groq is not configured properly in appsettings.json or environment variables.");
}

builder.Services.AddSingleton(chatClient);

var app = builder.Build();

app.UseCors("AllowReactApp");

app.Use(async (context, next) =>
{
    Console.WriteLine($"--> [{context.Request.Method}] {context.Request.Path}");
    await next();
    Console.WriteLine($"<-- [{context.Request.Method}] {context.Request.Path} : {context.Response.StatusCode}");
});

// 4. Instantiate Agent using Factory
var timesheetService = app.Services.GetRequiredService<TimesheetService>();
var handbookService = app.Services.GetRequiredService<HandbookService>();
var agent = new TimesheetAgentFactory(
    chatClient,
    timesheetService,
    handbookService,
    System.Text.Json.JsonSerializerOptions.Default
).CreateTimesheetAgent();

// 5. Expose the AG-UI Protocol Endpoint
app.MapAGUI("/", agent);

// 6. Expose REST endpoints for direct frontend synchronization
app.MapGet("/api/timesheet", (TimesheetService service) => Results.Ok(service.GetTimesheet()));
app.MapGet("/api/timesheet/summary", (TimesheetService service) => Results.Ok(service.GetSummary()));

app.MapPost("/api/timesheet/entry", (TimesheetService service, TimesheetEntryInput input) =>
{
    try
    {
        var entry = service.AddTimesheetEntry(input.Date, input.Project, input.Hours, input.Description);
        return Results.Ok(entry);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapDelete("/api/timesheet/entry/{id}", (TimesheetService service, string id) =>
{
    var removed = service.RemoveTimesheetEntry(id);
    return removed ? Results.Ok(new { success = true }) : Results.NotFound(new { error = "Entry not found" });
});

app.MapPost("/api/timesheet/submit", (TimesheetService service) =>
{
    var message = service.SubmitTimesheet();
    return Results.Ok(new { message });
});

app.MapPost("/api/timesheet/unlock", (TimesheetService service) =>
{
    var message = service.UnlockTimesheet();
    return Results.Ok(new { message });
});

Console.WriteLine("--> ASP.NET Core Backend is starting on Port 5116...");
app.Run("http://localhost:5116");

// Input model for REST API
public record TimesheetEntryInput(string Date, string Project, double Hours, string Description);

public partial class Program { }
