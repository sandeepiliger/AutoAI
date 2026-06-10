namespace AutoAI.Automation;

/// <summary>
/// Thrown when an element lookup times out. The message carries the search criteria
/// so the agent can recover (typically by calling get_ui_tree to see what exists).
/// </summary>
public sealed class ElementNotFoundException(string criteria) : Exception(
    $"No element found matching {criteria}. Call get_ui_tree to inspect the current UI.");
