namespace AutoAI.Agent;

/// <summary>
/// SDK-agnostic description of a tool the agent may call.
/// <see cref="ParametersJsonSchema"/> is a JSON-schema object string,
/// e.g. {"type":"object","properties":{...},"required":[...]}.
/// </summary>
public sealed record ToolSpec(string Name, string Description, string ParametersJsonSchema);
