namespace AutoAI.Agent;

public sealed class CopilotOptions
{
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();
    public SampleAppOptions SampleApp { get; set; } = new();
}

public sealed class AzureOpenAIOptions
{
    /// <summary>Azure OpenAI endpoint, e.g. https://&lt;your-resource&gt;.openai.azure.com/.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Azure OpenAI API key (Keys and Endpoint blade of your resource, or the Foundry portal).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Name of your model deployment, e.g. gpt-4o.</summary>
    public string DeploymentName { get; set; } = string.Empty;
}

public sealed class SampleAppOptions
{
    /// <summary>Path to the executable of the app under test.</summary>
    public string Path { get; set; } = string.Empty;
}
