using System.Text.Json;
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Identity;

namespace AutoAI.Agent;

/// <summary>
/// One chat session against an existing Azure AI Foundry agent. Keeps a single
/// persistent thread so multi-turn context works, supplies the automation tool
/// definitions per-run, and executes the agent's tool calls locally through
/// <see cref="IToolExecutor"/>.
/// </summary>
public sealed class FoundryAgentSession
{
    private const int MaxToolIterationsPerTurn = 25;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly PersistentAgentsClient _client;
    private readonly FoundryOptions _options;
    private readonly IToolExecutor _executor;
    private readonly List<ToolDefinition> _toolDefinitions;

    private PersistentAgentThread? _thread;

    public FoundryAgentSession(FoundryOptions options, IToolExecutor executor)
    {
        _options = options;
        _executor = executor;

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(options.TenantId))
        {
            credentialOptions.TenantId = options.TenantId;
        }

        _client = new PersistentAgentsClient(options.ProjectEndpoint, new DefaultAzureCredential(credentialOptions));

        _toolDefinitions = executor.Tools
            .Select(t => (ToolDefinition)new FunctionToolDefinition(
                name: t.Name,
                description: t.Description,
                parameters: BinaryData.FromString(t.ParametersJsonSchema)))
            .ToList();
    }

    /// <summary>
    /// Verifies endpoint, credentials and agent id up front so configuration
    /// problems surface as one clear message instead of failing mid-chat.
    /// </summary>
    public async Task<string> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            PersistentAgent agent = await _client.Administration.GetAgentAsync(_options.AgentId, ct);
            return $"Connected to agent '{agent.Name}' ({agent.Id}), model: {agent.Model}.";
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403)
        {
            throw new InvalidOperationException(
                "Authentication to Azure AI Foundry failed. Run 'az login' and make sure your " +
                "account has the 'Azure AI User' role on the project. " + ex.Message, ex);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException(
                $"Agent '{_options.AgentId}' was not found at '{_options.ProjectEndpoint}'. " +
                "Check Foundry:AgentId and Foundry:ProjectEndpoint in appsettings.json.", ex);
        }
    }

    /// <summary>
    /// Sends one user instruction and runs the agent until it produces a final answer,
    /// executing any requested automation tools along the way.
    /// </summary>
    public async Task<string> SendMessageAsync(
        string userText,
        IProgress<ToolLogEntry>? toolLog = null,
        CancellationToken ct = default)
    {
        _thread ??= await _client.Threads.CreateThreadAsync(cancellationToken: ct);

        await _client.Messages.CreateMessageAsync(_thread.Id, MessageRole.User, userText, cancellationToken: ct);

        ThreadRun run = await _client.Runs.CreateRunAsync(
            _thread.Id,
            _options.AgentId,
            overrideTools: _toolDefinitions,
            additionalInstructions: SystemPrompt.Addendum,
            cancellationToken: ct);

        var toolIterations = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(PollInterval, ct);
            run = await _client.Runs.GetRunAsync(_thread.Id, run.Id, ct);

            if (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
            {
                continue;
            }

            if (run.Status == RunStatus.RequiresAction &&
                run.RequiredAction is SubmitToolOutputsAction submitAction)
            {
                if (++toolIterations > MaxToolIterationsPerTurn)
                {
                    await _client.Runs.CancelRunAsync(_thread.Id, run.Id, ct);
                    return $"Stopped: the agent exceeded {MaxToolIterationsPerTurn} tool iterations in a single turn.";
                }

                var outputs = new List<ToolOutput>();
                foreach (RequiredToolCall toolCall in submitAction.ToolCalls)
                {
                    if (toolCall is RequiredFunctionToolCall functionCall)
                    {
                        outputs.Add(new ToolOutput(toolCall.Id, await ExecuteToolAsync(functionCall, toolLog, ct)));
                    }
                }

                run = await _client.Runs.SubmitToolOutputsToRunAsync(run, outputs, cancellationToken: ct);
                continue;
            }

            break;
        }

        if (run.Status == RunStatus.Completed)
        {
            return await GetLatestAssistantMessageAsync(ct);
        }

        return $"Run ended with status '{run.Status}'. {run.LastError?.Message}".Trim();
    }

    private async Task<string> ExecuteToolAsync(
        RequiredFunctionToolCall functionCall,
        IProgress<ToolLogEntry>? toolLog,
        CancellationToken ct)
    {
        toolLog?.Report(new ToolLogEntry(ToolLogKind.Call, functionCall.Name, functionCall.Arguments));

        string result;
        try
        {
            result = await _executor.ExecuteAsync(functionCall.Name, functionCall.Arguments, ct);
        }
        catch (Exception ex)
        {
            // The executor catches its own failures; this is a last-resort guard so an
            // unexpected crash still flows back to the agent instead of killing the run.
            result = JsonSerializer.Serialize(new { success = false, error = $"{ex.GetType().Name}: {ex.Message}" });
        }

        toolLog?.Report(new ToolLogEntry(ToolLogKind.Result, functionCall.Name, result));
        return result;
    }

    private async Task<string> GetLatestAssistantMessageAsync(CancellationToken ct)
    {
        AsyncPageable<PersistentThreadMessage> messages = _client.Messages.GetMessagesAsync(
            _thread!.Id, order: ListSortOrder.Descending, cancellationToken: ct);

        await foreach (PersistentThreadMessage message in messages)
        {
            if (message.Role != MessageRole.Agent)
            {
                continue;
            }

            var text = string.Concat(message.ContentItems
                .OfType<MessageTextContent>()
                .Select(c => c.Text));
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return "(The agent completed the run without a text reply.)";
    }
}
