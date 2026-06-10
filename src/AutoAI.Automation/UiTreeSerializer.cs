using System.Text.Json;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace AutoAI.Automation;

/// <summary>
/// Serializes the visible automation tree to compact JSON for the agent.
/// Anonymous layout containers (Pane/Custom/Group without id or name) are flattened
/// into their parent and offscreen elements are skipped, keeping the payload small —
/// this JSON goes straight into the agent's context window.
/// </summary>
public sealed class UiTreeSerializer(int maxDepth = 12, int maxNodes = 400)
{
    private int _nodeCount;

    public string Serialize(Window window)
    {
        _nodeCount = 0;
        var root = new JsonObject
        {
            ["windowTitle"] = window.Title,
            ["tree"] = BuildNode(window, depth: 0),
        };

        // Modal dialogs (including message boxes) are separate top-level windows.
        var modalWindows = new JsonArray();
        foreach (var modal in Safe(() => window.ModalWindows) ?? [])
        {
            modalWindows.Add(new JsonObject
            {
                ["title"] = Safe(() => modal.Title),
                ["tree"] = BuildNode(modal, depth: 0),
            });
        }
        if (modalWindows.Count > 0)
        {
            root["modalWindows"] = modalWindows;
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private JsonObject BuildNode(AutomationElement element, int depth)
    {
        _nodeCount++;

        var node = new JsonObject
        {
            ["controlType"] = SafeControlType(element).ToString(),
        };

        var automationId = Safe(() => element.Properties.AutomationId.ValueOrDefault);
        var name = Safe(() => element.Properties.Name.ValueOrDefault);
        if (!string.IsNullOrEmpty(automationId)) node["automationId"] = automationId;
        if (!string.IsNullOrEmpty(name)) node["name"] = name;
        if (Safe(() => element.Properties.IsEnabled.ValueOrDefault, true) == false) node["isEnabled"] = false;

        var value = TryGetValue(element);
        if (!string.IsNullOrEmpty(value)) node["value"] = value;

        var toggleState = TryGetToggleState(element);
        if (toggleState is not null) node["checked"] = toggleState == ToggleState.On;
        if (Safe(() => element.Patterns.SelectionItem.PatternOrDefault?.IsSelected?.ValueOrDefault) == true)
        {
            node["selected"] = true;
        }

        var children = BuildChildren(element, depth + 1);
        if (children.Count > 0) node["children"] = children;

        return node;
    }

    private JsonArray BuildChildren(AutomationElement element, int depth)
    {
        var result = new JsonArray();
        if (depth > maxDepth || _nodeCount >= maxNodes)
        {
            return result;
        }

        foreach (var child in Safe(element.FindAllChildren) ?? [])
        {
            if (_nodeCount >= maxNodes)
            {
                break;
            }
            if (Safe(() => child.Properties.IsOffscreen.ValueOrDefault, false))
            {
                continue;
            }

            if (IsAnonymousContainer(child))
            {
                // Flatten: hoist the container's children to this level.
                foreach (var hoisted in BuildChildren(child, depth))
                {
                    result.Add(hoisted!.DeepClone());
                }
            }
            else
            {
                result.Add(BuildNode(child, depth));
            }
        }

        return result;
    }

    private static bool IsAnonymousContainer(AutomationElement element)
    {
        var controlType = SafeControlType(element);
        if (controlType is not (ControlType.Pane or ControlType.Custom or ControlType.Group))
        {
            return false;
        }
        var automationId = Safe(() => element.Properties.AutomationId.ValueOrDefault);
        var name = Safe(() => element.Properties.Name.ValueOrDefault);
        return string.IsNullOrEmpty(automationId) && string.IsNullOrEmpty(name);
    }

    internal static string? TryGetValue(AutomationElement element)
    {
        return Safe(() => element.Patterns.Value.PatternOrDefault?.Value?.ValueOrDefault);
    }

    internal static ToggleState? TryGetToggleState(AutomationElement element)
    {
        return Safe(() => element.Patterns.Toggle.PatternOrDefault?.ToggleState?.ValueOrDefault);
    }

    internal static double? TryGetRangeValue(AutomationElement element)
    {
        return Safe(() => element.Patterns.RangeValue.PatternOrDefault?.Value?.ValueOrDefault);
    }

    private static ControlType SafeControlType(AutomationElement element)
        => Safe(() => element.Properties.ControlType.ValueOrDefault, ControlType.Unknown);

    private static T? Safe<T>(Func<T> get, T? fallback = default)
    {
        try
        {
            return get();
        }
        catch
        {
            return fallback;
        }
    }
}
