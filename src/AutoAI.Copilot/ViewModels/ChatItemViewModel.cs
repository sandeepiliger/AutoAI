namespace AutoAI.Copilot.ViewModels;

public enum ChatItemKind
{
    User,
    Assistant,
    ToolCall,
    ToolResult,
    Error,
}

public sealed record ChatItemViewModel(ChatItemKind Kind, string Text)
{
    public DateTime Timestamp { get; } = DateTime.Now;

    public string KindLabel => Kind.ToString();
}
