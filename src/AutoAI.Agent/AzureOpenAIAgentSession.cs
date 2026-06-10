using System.ClientModel;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AutoAI.Agent;

/// <summary>
/// One chat session against an Azure OpenAI model deployment, authenticated with just
/// an endpoint and an API key. The agent behavior lives entirely client-side: the system
/// prompt, the automation tool definitions, the tool-call execution loop and the
/// conversation history (so multi-turn context works).
/// </summary>
public sealed class AzureOpenAIAgentSession
{
    private const int MaxToolIterationsPerTurn = 25;

    private readonly AzureOpenAIOptions _options;
    private readonly IToolExecutor _executor;
    private readonly List<ChatTool> _tools;
    private readonly List<ChatMessage> _history;

    private ChatClient? _chatClient;

    public AzureOpenAIAgentSession(AzureOpenAIOptions options, IToolExecutor executor)
    {
        _options = options;
        _executor = executor;

        _tools = executor.Tools
            .Select(t => ChatTool.CreateFunctionTool(
                functionName: t.Name,
                functionDescription: t.Description,
                functionParameters: BinaryData.FromString(t.ParametersJsonSchema)))
            .ToList();

        _history = [new SystemChatMessage(SystemPrompt.Text)];
    }

    /// <summary>
    /// Verifies endpoint, key and deployment with a minimal request so configuration
    /// problems surface as one clear message instead of failing mid-chat.
    /// </summary>
    public async Task<string> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            await Client.CompleteChatAsync(
                [new UserChatMessage("ping")],
                new ChatCompletionOptions { MaxOutputTokenCount = 1 },
                ct);
            return $"Connected to Azure OpenAI deployment '{_options.DeploymentName}'.";
        }
        catch (ClientResultException ex) when (ex.Status is 401 or 403)
        {
            throw new InvalidOperationException(
                "Authentication to Azure OpenAI failed. Check AzureOpenAI:ApiKey in appsettings.json " +
                "(Keys and Endpoint blade of your Azure OpenAI resource). " + ex.Message, ex);
        }
        catch (ClientResultException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException(
                $"Deployment '{_options.DeploymentName}' was not found at '{_options.Endpoint}'. " +
                "Check AzureOpenAI:Endpoint and AzureOpenAI:DeploymentName in appsettings.json.", ex);
        }
    }

    /// <summary>
    /// Sends one user instruction and runs the model until it produces a final answer,
    /// executing any requested automation tools along the way.
    /// </summary>
    public async Task<string> SendMessageAsync(
        string userText,
        IProgress<ToolLogEntry>? toolLog = null,
        CancellationToken ct = default)
    {
        _history.Add(new UserChatMessage(userText));

        var options = new ChatCompletionOptions();
        foreach (var tool in _tools)
        {
            options.Tools.Add(tool);
        }

        for (var iteration = 0; iteration < MaxToolIterationsPerTurn; iteration++)
        {
            ct.ThrowIfCancellationRequested();
            ChatCompletion completion = await Client.CompleteChatAsync(_history, options, ct);

            if (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                _history.Add(new AssistantChatMessage(completion));
                foreach (ChatToolCall toolCall in completion.ToolCalls)
                {
                    _history.Add(new ToolChatMessage(toolCall.Id, await ExecuteToolAsync(toolCall, toolLog, ct)));
                }
                continue;
            }

            var text = string.Concat(completion.Content.Select(part => part.Text));
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "(The model completed the turn without a text reply.)";
            }
            _history.Add(new AssistantChatMessage(text));
            return text;
        }

        return $"Stopped: the model exceeded {MaxToolIterationsPerTurn} tool iterations in a single turn.";
    }

    private ChatClient Client
    {
        get
        {
            if (_chatClient is null)
            {
                ValidateOptions(_options);
                var azureClient = new AzureOpenAIClient(
                    new Uri(_options.Endpoint),
                    new AzureKeyCredential(_options.ApiKey));
                _chatClient = azureClient.GetChatClient(_options.DeploymentName);
            }
            return _chatClient;
        }
    }

    private async Task<string> ExecuteToolAsync(
        ChatToolCall toolCall,
        IProgress<ToolLogEntry>? toolLog,
        CancellationToken ct)
    {
        var arguments = toolCall.FunctionArguments.ToString();
        toolLog?.Report(new ToolLogEntry(ToolLogKind.Call, toolCall.FunctionName, arguments));

        string result;
        try
        {
            result = await _executor.ExecuteAsync(toolCall.FunctionName, arguments, ct);
        }
        catch (Exception ex)
        {
            // The executor catches its own failures; this is a last-resort guard so an
            // unexpected crash still flows back to the model instead of killing the turn.
            result = JsonSerializer.Serialize(new { success = false, error = $"{ex.GetType().Name}: {ex.Message}" });
        }

        toolLog?.Report(new ToolLogEntry(ToolLogKind.Result, toolCall.FunctionName, result));
        return result;
    }

    private static void ValidateOptions(AzureOpenAIOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint) || options.Endpoint.Contains('<') ||
            string.IsNullOrWhiteSpace(options.ApiKey) || options.ApiKey.StartsWith('<') ||
            string.IsNullOrWhiteSpace(options.DeploymentName))
        {
            throw new InvalidOperationException(
                "Azure OpenAI is not configured. Set AzureOpenAI:Endpoint, AzureOpenAI:ApiKey and " +
                "AzureOpenAI:DeploymentName in appsettings.json (or appsettings.local.json).");
        }
    }
}
