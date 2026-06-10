namespace AutoAI.Agent;

public sealed class CopilotOptions
{
    public FoundryOptions Foundry { get; set; } = new();
    public SampleAppOptions SampleApp { get; set; } = new();
}

public sealed class FoundryOptions
{
    /// <summary>Azure AI Foundry project endpoint, e.g. https://&lt;resource&gt;.services.ai.azure.com/api/projects/&lt;project&gt;.</summary>
    public string ProjectEndpoint { get; set; } = string.Empty;

    /// <summary>Id of the existing agent, e.g. asst_xxxxxxxx.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Optional Entra tenant to authenticate against.</summary>
    public string? TenantId { get; set; }
}

public sealed class SampleAppOptions
{
    /// <summary>Path to the executable of the app under test.</summary>
    public string Path { get; set; } = string.Empty;
}
