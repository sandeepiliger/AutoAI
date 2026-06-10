namespace AutoAI.Agent;

public enum ToolLogKind
{
    Call,
    Result,
}

/// <summary>Progress report for a single tool call, streamed to the chat UI as it happens.</summary>
public sealed record ToolLogEntry(ToolLogKind Kind, string ToolName, string PayloadJson);
