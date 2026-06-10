namespace AutoAI.Agent;

/// <summary>
/// Bridge between the Foundry agent loop and the concrete automation layer.
/// Implementations must never throw from <see cref="ExecuteAsync"/>; failures are
/// reported as {"success":false,"error":"..."} JSON so the agent run can continue.
/// </summary>
public interface IToolExecutor
{
    IReadOnlyList<ToolSpec> Tools { get; }

    /// <summary>Executes a tool and returns its result as a JSON string.</summary>
    Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default);
}
