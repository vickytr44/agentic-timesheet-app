using backend.Services;
using Backend;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace backend;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by TimesheetAgentFactory")]
internal sealed class TimesheetSharedStateAgent : DelegatingAIAgent
{
    private readonly TimesheetService _timesheetService;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public TimesheetSharedStateAgent(AIAgent innerAgent, TimesheetService timesheetService, JsonSerializerOptions jsonSerializerOptions)
        : base(innerAgent)
    {
        _timesheetService = timesheetService;
        _jsonSerializerOptions = jsonSerializerOptions;
    }

    protected override Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession session, AgentRunOptions options, CancellationToken cancellationToken = default)
    {
        return RunStreamingAsync(messages, session, options, cancellationToken).ToAgentResponseAsync(cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession session,
        AgentRunOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options is not ChatClientAgentRunOptions { ChatOptions.AdditionalProperties: { } properties } chatRunOptions ||
            !properties.TryGetValue("ag_ui_state", out JsonElement state))
        {
            await foreach (var update in InnerAgent.RunStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
            yield break;
        }

        // 1. Sync the incoming frontend state to our local TimesheetService before the agent runs
        try
        {
            var incomingSnapshot = JsonSerializer.Deserialize<TimesheetStateSnapshot>(state.GetRawText(), _jsonSerializerOptions);
            if (incomingSnapshot != null)
            {
                _timesheetService.SyncState(incomingSnapshot.Entries, incomingSnapshot.Status);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"--> Error syncing incoming state: {ex.Message}");
        }

        var firstRunOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = chatRunOptions.ChatOptions.Clone(),
            AllowBackgroundResponses = chatRunOptions.AllowBackgroundResponses,
            ContinuationToken = chatRunOptions.ContinuationToken,
            ChatClientFactory = chatRunOptions.ChatClientFactory,
        };

        // NOTE: We do NOT set ChatOptions.ResponseFormat to ForJsonSchema here because Groq/Llama models 
        // throw an HTTP 400 error when JSON mode is combined with tool/function calling.
        // Instead, we instruct the model via prompt to return raw JSON matching the state schema.

        ChatMessage stateUpdateMessage = new(
            ChatRole.System,
            [
                new TextContent("Here is the current state in JSON format:"),
                new TextContent(state.GetRawText()),
                new TextContent("You must respond with the updated state in JSON format matching this schema:\n" +
                               "{\n" +
                               "  \"entries\": [\n" +
                               "    { \"id\": \"guid\", \"date\": \"YYYY-MM-DD\", \"project\": \"string\", \"hours\": number, \"description\": \"string\" }\n" +
                               "  ],\n" +
                               "  \"status\": \"Draft|Submitted\"\n" +
                               "}\n" +
                               "Do not output any introductory or explanatory text. Output ONLY the JSON object. If you need to make changes, execute the appropriate tools first, and then return the updated state in JSON.")
            ]);

        var firstRunMessages = messages.Append(stateUpdateMessage);

        var allUpdates = new List<AgentResponseUpdate>();
        await foreach (var update in InnerAgent.RunStreamingAsync(firstRunMessages, session, firstRunOptions, cancellationToken).ConfigureAwait(false))
        {
            allUpdates.Add(update);

            // Yield ALL updates (tool calls AND text chunks) so the connection
            // stays alive during slow LLM generation.  Text content here is the
            // intermediate JSON state the LLM streams — the client will ignore it
            // because the authoritative DataContent state update comes next.
            yield return update;
        }

        var response = allUpdates.ToAgentResponse();
        bool stateChanged = false;

        if (response.TryDeserialize(_jsonSerializerOptions, out JsonElement stateSnapshot))
        {
            // Sync the updated state from the LLM output back to our local service
            try
            {
                var snapshot = JsonSerializer.Deserialize<TimesheetStateSnapshot>(stateSnapshot.GetRawText(), _jsonSerializerOptions);
                if (snapshot != null)
                {
                    stateChanged = TimesheetStateSnapshotChanged(state, stateSnapshot);
                    _timesheetService.SyncState(snapshot.Entries, snapshot.Status);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--> Error syncing response state: {ex.Message}");
            }

            byte[] stateBytes = JsonSerializer.SerializeToUtf8Bytes(
                stateSnapshot,
                _jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)));
            yield return new AgentResponseUpdate
            {
                Contents = [new DataContent(stateBytes, "application/json")]
            };
        }
        else
        {
            yield break;
        }

        // Skip a second LLM round-trip for narration — build a concise summary
        // directly from the current service state instead.  This eliminates the
        // second slow LLM call and prevents BodyTimeoutError on the proxy stream.
        if (stateChanged)
        {
            var entryCount = _timesheetService.GetTimesheet().Count;
            var status = _timesheetService.GetStatus();
            var summaryText = $"Your timesheet has been updated: {entryCount} entr{(entryCount == 1 ? "y" : "ies")} logged, status is {status}.";

            yield return new AgentResponseUpdate
            {
                Contents = [new TextContent(summaryText)]
            };
        }
    }

    private static bool TimesheetStateSnapshotChanged(JsonElement oldState, JsonElement newState)
    {
        // Check if status changed
        string oldStatus = oldState.TryGetProperty("status", out var os) ? os.GetString() ?? "" : "";
        string newStatus = newState.TryGetProperty("status", out var ns) ? ns.GetString() ?? "" : "";
        if (oldStatus != newStatus) return true;

        // Check if entries changed
        if (!oldState.TryGetProperty("entries", out var oldEntries) ||
            !newState.TryGetProperty("entries", out var newEntries))
        {
            return true;
        }

        if (oldEntries.ValueKind != JsonValueKind.Array || newEntries.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        if (oldEntries.GetArrayLength() != newEntries.GetArrayLength())
        {
            return true;
        }

        // Deep compare the entries (assuming order is preserved or we can do a simple element comparison)
        var oldArray = oldEntries.EnumerateArray().ToList();
        var newArray = newEntries.EnumerateArray().ToList();

        for (int i = 0; i < oldArray.Count; i++)
        {
            var oldE = oldArray[i];
            var newE = newArray[i];

            // Compare specific properties: id, date, project, hours, description
            if (GetPropString(oldE, "id") != GetPropString(newE, "id") ||
                GetPropString(oldE, "date") != GetPropString(newE, "date") ||
                GetPropString(oldE, "project") != GetPropString(newE, "project") ||
                GetPropDouble(oldE, "hours") != GetPropDouble(newE, "hours") ||
                GetPropString(oldE, "description") != GetPropString(newE, "description"))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetPropString(JsonElement el, string propName)
    {
        return el.TryGetProperty(propName, out var p) ? p.GetString() ?? "" : "";
    }

    private static double GetPropDouble(JsonElement el, string propName)
    {
        if (el.TryGetProperty(propName, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number) return p.GetDouble();
            if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), out double d)) return d;
        }
        return 0;
    }
}

internal static class AgentResponseExtensions
{
    public static bool TryDeserialize(this AgentResponse response, JsonSerializerOptions options, out JsonElement result)
    {
        result = default;
        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Robustly clean up any markdown wrappers if the LLM outputted them (e.g. ```json ... ```)
        text = text.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(7);
        }
        else if (text.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(3);
        }

        if (text.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(0, text.Length - 3);
        }
        text = text.Trim();

        try
        {
            result = JsonSerializer.Deserialize<JsonElement>(text, options);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
