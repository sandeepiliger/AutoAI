using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace AutoAI.Automation;

/// <summary>Locates elements under the main window by automationId / name / controlType with retry.</summary>
public sealed class ElementFinder(AutomationSession session)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Finds the first matching descendant or throws <see cref="ElementNotFoundException"/>.</summary>
    public AutomationElement Find(string? automationId, string? name, string? controlType, TimeSpan? timeout = null)
    {
        var element = TryFind(automationId, name, controlType, timeout ?? DefaultTimeout);
        return element ?? throw new ElementNotFoundException(Describe(automationId, name, controlType));
    }

    /// <summary>Finds the first matching descendant or returns null after the timeout.</summary>
    public AutomationElement? TryFind(string? automationId, string? name, string? controlType, TimeSpan timeout)
    {
        var condition = BuildCondition(automationId, name, controlType);
        return Retry.WhileNull(
            () =>
            {
                // Modal dialogs are searched before the main window (see GetSearchRoots).
                foreach (var root in session.GetSearchRoots())
                {
                    var found = root.FindFirstDescendant(condition);
                    if (found is not null)
                    {
                        return found;
                    }
                }
                return null;
            },
            timeout: timeout,
            interval: RetryInterval,
            throwOnTimeout: false,
            ignoreException: true).Result;
    }

    public static string Describe(string? automationId, string? name, string? controlType)
    {
        var parts = new List<string>();
        if (automationId is not null) parts.Add($"automationId='{automationId}'");
        if (name is not null) parts.Add($"name='{name}'");
        if (controlType is not null) parts.Add($"controlType='{controlType}'");
        return parts.Count > 0 ? string.Join(", ", parts) : "(no criteria)";
    }

    private ConditionBase BuildCondition(string? automationId, string? name, string? controlType)
    {
        var cf = session.Automation.ConditionFactory;
        ConditionBase? condition = null;

        if (!string.IsNullOrEmpty(automationId))
        {
            condition = Combine(condition, cf.ByAutomationId(automationId));
        }
        if (!string.IsNullOrEmpty(name))
        {
            condition = Combine(condition, cf.ByName(name));
        }
        if (!string.IsNullOrEmpty(controlType))
        {
            if (!Enum.TryParse<ControlType>(controlType, ignoreCase: true, out var parsed))
            {
                throw new ArgumentException(
                    $"Unknown controlType '{controlType}'. Use UIA control type names like Button, Edit, DataGrid, Tab, TabItem, Text, Window.");
            }
            condition = Combine(condition, cf.ByControlType(parsed));
        }

        return condition ?? throw new ArgumentException(
            "At least one of automationId, name or controlType must be provided.");
    }

    private static ConditionBase Combine(ConditionBase? current, ConditionBase next)
        => current is null ? next : current.And(next);
}
