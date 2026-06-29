using backend.Configuration;
using backend.Services;
using Microsoft.Extensions.AI;

namespace backend.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to organize
/// service registration into logical, testable units.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers CORS, HTTP client, JSON serialization, and all business services.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // CORS
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyNames.AllowReactApp, policy =>
            {
                policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        // Strongly-typed options
        services.Configure<HandbookOptions>(configuration.GetSection(HandbookOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<OpenAIOptions>(configuration.GetSection(OpenAIOptions.SectionName));

        // Infrastructure
        services.AddHttpClient();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Add(TimesheetSerializerContext.Default);
        });

        // Business services — registered as interfaces for testability
        services.AddSingleton<ITimesheetService, TimesheetService>();
        services.AddSingleton<IHandbookService, HandbookService>();
        services.AddSingleton<ILeaveService, LeaveService>();

        return services;
    }

    /// <summary>
    /// Configures and registers the <see cref="IChatClient"/> singleton
    /// based on the "LlmProvider" configuration value (Ollama or Groq/OpenAI).
    /// </summary>
    public static IServiceCollection AddLlmChatClient(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["LlmProvider"] ?? "Groq";
        IChatClient chatClient;

        if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            var options = configuration.GetSection(OllamaOptions.SectionName).Get<OllamaOptions>() ?? new OllamaOptions();
            var clientOptions = new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(options.Endpoint)
            };
            var openAIClient = new OpenAI.OpenAIClient(
                new System.ClientModel.ApiKeyCredential("ollama"), clientOptions);
            chatClient = openAIClient.GetChatClient(options.ModelId).AsIChatClient();
        }
        else
        {
            var options = configuration.GetSection(OpenAIOptions.SectionName).Get<OpenAIOptions>() ?? new OpenAIOptions();

            if (string.IsNullOrEmpty(options.ApiKey) || options.ApiKey == "YOUR_GROQ_API_KEY")
            {
                throw new InvalidOperationException(
                    "API key for the LLM provider is not configured. " +
                    "Set 'OpenAI:ApiKey' in appsettings.json, user-secrets, or environment variables.");
            }

            var clientOptions = new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(options.Endpoint)
            };
            var openAIClient = new OpenAI.OpenAIClient(
                new System.ClientModel.ApiKeyCredential(options.ApiKey), clientOptions);
            chatClient = openAIClient.GetChatClient(options.ModelId).AsIChatClient();
        }

        services.AddSingleton(chatClient);
        return services;
    }

    /// <summary>
    /// Registers the <see cref="TimesheetAgentFactory"/> and its <see cref="Microsoft.Agents.AI.AIAgent"/>
    /// product into the DI container.
    /// </summary>
    public static IServiceCollection AddAgentWorkflow(this IServiceCollection services)
    {
        services.AddSingleton(sp => new TimesheetAgentFactory(
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ITimesheetService>(),
            sp.GetRequiredService<IHandbookService>(),
            sp.GetRequiredService<ILeaveService>(),
            System.Text.Json.JsonSerializerOptions.Default,
            sp.GetRequiredService<ILogger<TimesheetAgentFactory>>()
        ));

        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<TimesheetAgentFactory>();
            return factory.CreateTimesheetAgent();
        });

        return services;
    }
}

/// <summary>
/// Well-known CORS policy names used throughout the application.
/// </summary>
public static class CorsPolicyNames
{
    public const string AllowReactApp = "AllowReactApp";
}
